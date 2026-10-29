using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OddMaster.Contracts;
using OddMaster.Core.Ports;
using OddMaster.Host.Emulator;
using Xunit;

namespace OddMaster.Contracts.Tests
{
    /// <summary>Корень репозитория и конфигурация эмулятора: тесты идут и из корня, и из папки проекта.</summary>
    internal static class Repository
    {
        public static string Root()
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "OddMaster.sln")))
                directory = directory.Parent;
            if (directory == null)
                throw new InvalidOperationException("Не найден корень репозитория (OddMaster.sln)");
            return directory.FullName;
        }

        public static EmulatorBudget Budget() =>
            EmulatorBudget.LoadFromFile(Path.Combine(Root(), "config/host/emulator.json"));
    }

    /// <summary>Хост эмулятора под контракт: имя нужно для отчёта ODD_SELFTEST.</summary>
    internal sealed class EmulatorUnderTest : IHostUnderTest
    {
        public EmulatorUnderTest(EmulatorHost host)
        {
            Host = host;
        }

        public EmulatorHost Host { get; }

        public string Name => "emulator";

        public ICadDocument Document => Host.Document;

        public ICadEvents Events => Host.Events;

        public ICadPersistence Persistence => Host.Persistence;

        public ICadInteraction Interaction => Host.Interaction;
    }

    /// <summary>Проверки кейсов на xUnit. В nanoCAD те же кейсы проверяет ODD_SELFTEST своим писателем.</summary>
    internal sealed class XunitCheck : IContractCheck
    {
        public void True(bool condition, string message) => Assert.True(condition, message);

        public void Equal(string expected, string actual, string message) =>
            Assert.True(string.Equals(expected, actual, StringComparison.Ordinal),
                message + ": ожидалось «" + expected + "», получено «" + actual + "»");

        public void Throws<TException>(Action action, string message) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }
            catch (Exception other)
            {
                Assert.Fail(message + ": ожидалось " + typeof(TException).Name + ", получено " + other.GetType().Name);
            }
            Assert.Fail(message + ": ожидалось исключение " + typeof(TException).Name + ", его не было");
        }
    }

    /// <summary>
    /// Мост М1: один набор кейсов, который в nanoCAD выполнит команда ODD_SELFTEST.
    /// Здесь он гоняется против строгого эмулятора — это уровень L1E, а не «работает в САПР».
    /// </summary>
    public class ContractTests
    {
        public static IEnumerable<object[]> CaseIds() =>
            ContractCatalog.Cases.Select(item => new object[] { item.Id });

        [Theory(DisplayName = "M18-K: контрактный кейс зелёный на эмуляторе")]
        [MemberData(nameof(CaseIds))]
        public void ContractCasePassesOnEmulator(string id)
        {
            var contractCase = ContractCatalog.Find(id);
            Assert.True(contractCase != null, "кейс не найден: " + id);
            var host = new EmulatorUnderTest(new EmulatorHost(Repository.Budget()));
            contractCase!.Run(host, new XunitCheck());
        }

        [Fact(DisplayName = "M18-K00: стартовый набор — 12 кейсов по пяти портам")]
        public void CatalogIsComplete()
        {
            var cases = ContractCatalog.Cases;
            Assert.True(cases.Count >= 12, "кейсов меньше 12: " + cases.Count);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in cases) Assert.True(ids.Add(item.Id), "повтор идентификатора: " + item.Id);
            foreach (var port in new[] { "ICadDocument", "ICadTransaction", "ICadEvents", "ICadPersistence" })
                Assert.True(cases.Any(item => item.Port == port), "нет ни одного кейса порта " + port);
        }
    }

    /// <summary>
    /// Правила строгого эмулятора (§2 «LINUX-ПОЛИГОН»): по тесту на каждое из 11 правил.
    /// Эмулятор падает там, где настоящая САПР могла бы стерпеть, — в этом его смысл.
    /// </summary>
    public class EmulatorRulesTests
    {
        private static EmulatorHost NewHost() => new EmulatorHost(Repository.Budget());

        private static CadHandle CreateSign(ICadDocument document, string number)
        {
            var transaction = document.BeginTransaction("PlaceSign");
            var handle = transaction.Create("Sign", "ODD_SIGN", new List<CadProperty>
            {
                new CadProperty("number", number),
            });
            transaction.Commit();
            return handle;
        }

        [Fact(DisplayName = "M18-R01 идентичность: доступ только по дескриптору, чужой дескриптор — ошибка")]
        public void Rule01_Identity()
        {
            var document = NewHost().Document;
            var handle = CreateSign(document, "3.24");
            Assert.Equal("Sign", document.Read(handle).Type);
            Assert.Throws<CadObjectMissingException>(() => document.Read(new CadHandle(9999)));
        }

        [Fact(DisplayName = "M18-R02 транзакции: изменение вне транзакции невозможно, Abort возвращает состояние")]
        public void Rule02_Transactions()
        {
            var document = NewHost().Document;
            CreateSign(document, "3.24");
            var before = document.Snapshot();
            var transaction = document.BeginTransaction("MoveSign");
            transaction.Set(document.Query("Sign")[0], "number", "3.25");
            transaction.Abort();
            Assert.Equal(before, document.Snapshot());
        }

        [Fact(DisplayName = "M18-R03 отмена: одна команда — одна группа, отмена и повтор возвращают снимки")]
        public void Rule03_UndoGroups()
        {
            var document = NewHost().Document;
            var initial = document.Snapshot();
            var transaction = document.BeginTransaction("PlacePair");
            transaction.Create("Sign", "ODD_SIGN", new List<CadProperty> { new CadProperty("number", "3.24") });
            transaction.Create("Sign", "ODD_SIGN", new List<CadProperty> { new CadProperty("number", "3.25") });
            transaction.Commit();
            var after = document.Snapshot();
            document.Undo();
            Assert.Equal(initial, document.Snapshot());
            Assert.False(document.CanUndo, "две вставки в одной транзакции — одна группа отмены");
            document.Redo();
            Assert.Equal(after, document.Snapshot());
        }

        [Fact(DisplayName = "M18-R04 повторный вход: изменение внутри уведомления отвергнуто, пересчёт отложен")]
        public void Rule04_Reentrancy()
        {
            var host = NewHost();
            var document = host.Document;
            var rejected = 0;
            var sink = new LambdaSink(change =>
            {
                try
                {
                    var attempt = document.BeginTransaction("FromEvent");
                    attempt.Create("Sign", "ODD_SIGN", new List<CadProperty>
                    {
                        new CadProperty("number", change.Type),
                    });
                }
                catch (CadMutationRejectedException)
                {
                    rejected++;
                }
            });
            host.Events.Subscribe(sink);
            try
            {
                CreateSign(document, "3.24");
            }
            finally
            {
                host.Events.Unsubscribe(sink);
            }
            Assert.Equal(1, rejected);
            Assert.Single(document.Query("Sign"));
            Assert.True(host.Events.Deferred.Count > 0, "пересчёт ждёт в отложенной очереди");
        }

        [Fact(DisplayName = "M18-R05 слияние: в одной транзакции одно уведомление на объект")]
        public void Rule05_MergedEvents()
        {
            var host = NewHost();
            var changes = new List<CadChange>();
            var sink = new LambdaSink(changes.Add);
            host.Events.Subscribe(sink);
            try
            {
                var transaction = host.Document.BeginTransaction("EditSign");
                var handle = transaction.Create("Sign", "ODD_SIGN",
                    new List<CadProperty> { new CadProperty("number", "3.24") });
                transaction.Set(handle, "x", "1");
                transaction.Set(handle, "x", "2");
                transaction.Commit();
            }
            finally
            {
                host.Events.Unsubscribe(sink);
            }
            Assert.Equal("1", changes.Count.ToString(CultureInfo.InvariantCulture));
        }

        [Fact(DisplayName = "M18-R06 хранение: будущая версия — только чтение, текущая — круговой рейс")]
        public void Rule06_Persistence()
        {
            var host = NewHost();
            CreateSign(host.Document, "3.24");
            var current = host.Persistence.Save();
            Assert.False(host.Persistence.Load(current).IsReadOnly, "текущая версия открывается на запись");

            const string future = "{ \"schemaVersion\": 99, \"objects\": [ { \"handle\": 1, \"type\": \"Sign\", " +
                                  "\"layer\": \"ODD_SIGN\", \"properties\": { \"number\": \"3.24\" } } ] }";
            var result = host.Persistence.Load(future);
            Assert.True(result.IsReadOnly, "неизвестная будущая версия — только чтение");
            Assert.True(result.Warnings.Count > 0, "есть предупреждение о версии");
        }

        [Fact(DisplayName = "M18-R07 культура: снимок документа одинаков в ru-RU и InvariantCulture")]
        public void Rule07_Culture()
        {
            var saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                var invariant = BuildSnapshot();
                CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
                var russian = BuildSnapshot();
                Assert.Equal(invariant, russian);
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        [Fact(DisplayName = "M18-R08 файлы: имя без учёта регистра, CRLF терпится")]
        public void Rule08_PathsAndLineEndings()
        {
            var host = NewHost();
            CreateSign(host.Document, "3.24");
            var directory = Path.Combine(Path.GetTempPath(), "odd-emulator-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, "Document.ODD.json");
                host.Persistence.SaveToFile(path);
                var lower = Path.Combine(directory, "document.odd.json");
                var loaded = host.Persistence.LoadFromFile(lower);
                Assert.Equal("1", loaded.Objects.Count.ToString(CultureInfo.InvariantCulture));

                var crlf = File.ReadAllText(path).Replace("\n", "\r\n");
                File.WriteAllText(path, crlf);
                Assert.Equal("1", host.Persistence.LoadFromFile(path).Objects.Count.ToString(CultureInfo.InvariantCulture));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact(DisplayName = "M18-R09 точность: сравнение длин с допуском из конфигурации, а не ==")]
        public void Rule09_Tolerance()
        {
            var budget = Repository.Budget();
            Assert.True(budget.LengthToleranceMm > 0, "допуск задан в config/host/emulator.json");
            Assert.True(budget.AreEqual(100.0, 100.0 + budget.LengthToleranceMm / 2),
                "разница внутри допуска считается равенством");
            Assert.False(budget.AreEqual(100.0, 100.0 + budget.LengthToleranceMm * 10),
                "разница вне допуска — не равенство");
        }

        [Fact(DisplayName = "M18-R10 детерминизм: один сценарий — один и тот же снимок и файл")]
        public void Rule10_Determinism()
        {
            var first = BuildSnapshot();
            var second = BuildSnapshot();
            Assert.Equal(first, second);
            Assert.Equal(BuildSaved(), BuildSaved());
        }

        [Fact(DisplayName = "M18-R11 бюджет: 1000 объектов одной транзакцией укладываются в бюджет конфигурации")]
        public void Rule11_Budget()
        {
            var budget = Repository.Budget();
            var host = NewHost();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var transaction = host.Document.BeginTransaction("BulkPlace");
            for (var i = 0; i < budget.BudgetObjects; i++)
            {
                transaction.Create("Post", "ODD_POST", new List<CadProperty>
                {
                    new CadProperty("picket", i.ToString(CultureInfo.InvariantCulture)),
                });
            }
            transaction.Commit();
            watch.Stop();
            Assert.Equal(budget.BudgetObjects, host.Document.Query("Post").Count);
            Assert.True(watch.ElapsedMilliseconds <= budget.BudgetMilliseconds,
                "сценарий " + budget + " занял " + watch.ElapsedMilliseconds + " мс");
            Assert.True(host.Document.CanUndo, "вся пачка — одна группа отмены");
            host.Document.Undo();
            Assert.Empty(host.Document.Query("Post"));
        }

        [Fact(DisplayName = "M18-A01 архитектура: ядро не знает ОС и не ссылается на хосты")]
        public void Architecture_CoreDoesNotKnowHosts()
        {
            var root = Path.Combine(Repository.Root(), "src/OddMaster.Core");
            var forbidden = new[] { "OperatingSystem.", "RuntimeInformation.", "#if WINDOWS", "OddMaster.NanoCAD",
                "OddMaster.Host", "Multicad", "Teigha", "System.Windows", "Avalonia" };
            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                         .OrderBy(item => item, StringComparer.Ordinal))
            {
                var text = File.ReadAllText(file).Replace("\r\n", "\n");
                foreach (var token in forbidden)
                {
                    Assert.True(text.IndexOf(token, StringComparison.Ordinal) < 0,
                        "в ядре нельзя упоминать «" + token + "»: " + Path.GetFileName(file));
                }
            }
        }

        private static string BuildSnapshot()
        {
            var host = NewHost();
            CreateSign(host.Document, "3.24");
            CreateSign(host.Document, "8.2.2");
            return host.Document.Snapshot();
        }

        private static string BuildSaved()
        {
            var host = NewHost();
            CreateSign(host.Document, "3.24");
            return host.Persistence.Save();
        }

        private sealed class LambdaSink : ICadEventSink
        {
            private readonly Action<CadChange> _handler;

            public LambdaSink(Action<CadChange> handler)
            {
                _handler = handler;
            }

            public void OnChanged(CadChange change) => _handler(change);
        }
    }
}
