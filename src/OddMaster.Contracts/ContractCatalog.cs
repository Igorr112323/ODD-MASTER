using System;
using System.Collections.Generic;
using OddMaster.Core.Ports;

namespace OddMaster.Contracts
{
    /// <summary>
    /// Стартовый набор контрактных кейсов (мост М1, §3 «LINUX-ПОЛИГОН»). Первая партия — F1:
    /// документ, транзакции, отмена/повтор, события, хранение. Ручки, ось, ведомость, контроль
    /// и актуализатор добавятся в F2…F4 — теми же кейсами, без отдельного набора для nanoCAD.
    /// </summary>
    public static class ContractCatalog
    {
        private const string SignType = "Sign";

        /// <summary>Все кейсы в порядке номеров.</summary>
        public static IReadOnlyList<ContractCase> Cases { get; } = new List<ContractCase>
        {
            new ContractCase("C-DOC-001", "ICadDocument",
                "Создать → найти → удалить по дескриптору", DocumentCreateFindDelete),
            new ContractCase("C-DOC-002", "ICadDocument",
                "Дескриптор после удаления → ошибка", DocumentDeletedHandle),
            new ContractCase("C-TRX-001", "ICadTransaction",
                "Изменение вне транзакции → ошибка", TransactionFinishedIsRejected),
            new ContractCase("C-TRX-002", "ICadTransaction",
                "Abort восстанавливает состояние", TransactionAbortRestores),
            new ContractCase("C-UND-001", "ICadDocument",
                "Команда → отмена → снимок равен исходному", UndoRestoresInitial),
            new ContractCase("C-UND-002", "ICadDocument",
                "Повтор после отмены", RedoRestoresResult),
            new ContractCase("C-EVT-001", "ICadEvents",
                "Одно «изменён» на объект за транзакцию", EventsAreMerged),
            new ContractCase("C-EVT-002", "ICadEvents",
                "Повторный вход отвергнут", ReentrancyIsRejected),
            new ContractCase("C-EVT-003", "ICadEvents",
                "Отложенный пересчёт после фиксации", DeferredAfterCommit),
            new ContractCase("C-PER-001", "ICadPersistence",
                "«Сохранить → открыть» даёт равенство", PersistenceRoundTrip),
            new ContractCase("C-PER-002", "ICadPersistence",
                "Миграция старой версии данных", PersistenceMigration),
            new ContractCase("C-PER-003", "ICadPersistence",
                "Кэш графики без модуля", PersistenceWithoutModule),
        };

        /// <summary>Кейс по идентификатору или null.</summary>
        public static ContractCase? Find(string id)
        {
            foreach (var item in Cases)
            {
                if (string.Equals(item.Id, id, StringComparison.Ordinal)) return item;
            }
            return null;
        }

        private static CadHandle CreateSign(ICadDocument document, string number, double x)
        {
            var transaction = document.BeginTransaction("PlaceSign");
            var handle = transaction.Create(SignType, "ODD_SIGN", new List<CadProperty>
            {
                new CadProperty("number", number),
                new CadProperty("x", x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)),
            });
            transaction.Commit();
            return handle;
        }

        private static void DocumentCreateFindDelete(IHostUnderTest host, IContractCheck check)
        {
            var document = host.Document;
            var handle = CreateSign(document, "3.24", 100);
            check.True(!handle.IsEmpty, "дескриптор не пуст");
            check.Equal("1", document.Query(SignType).Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "в документе ровно один знак");
            var snapshot = document.Read(handle);
            check.Equal(SignType, snapshot.Type, "тип объекта Sign");
            check.Equal("ODD_SIGN", snapshot.Layer, "слой объекта сохранён");
            check.Equal("3.24", snapshot.Get("number") ?? "—", "номер знака прочитан");

            var remove = document.BeginTransaction("EraseSign");
            remove.Delete(handle);
            remove.Commit();
            check.Equal("0", document.Query(SignType).Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "после удаления знаков нет");
        }

        private static void DocumentDeletedHandle(IHostUnderTest host, IContractCheck check)
        {
            var document = host.Document;
            var handle = CreateSign(document, "3.25", 0);
            var remove = document.BeginTransaction("EraseSign");
            remove.Delete(handle);
            remove.Commit();
            check.Throws<CadObjectMissingException>(() => document.Read(handle),
                "чтение удалённого объекта — ошибка, а не пустой снимок");
            check.Throws<CadObjectMissingException>(() =>
                {
                    var again = document.BeginTransaction("EraseSign");
                    again.Delete(handle);
                },
                "повторное удаление того же дескриптора — ошибка");
        }

        private static void TransactionFinishedIsRejected(IHostUnderTest host, IContractCheck check)
        {
            var document = host.Document;
            var transaction = document.BeginTransaction("PlaceSign");
            var handle = transaction.Create(SignType, "ODD_SIGN", new List<CadProperty>
            {
                new CadProperty("number", "5.5"),
            });
            transaction.Commit();
            check.Throws<CadMutationRejectedException>(() => transaction.Set(handle, "number", "5.6"),
                "изменение после Commit запрещено");
            check.Throws<CadMutationRejectedException>(() => transaction.Delete(handle),
                "удаление после Commit запрещено");
            check.Equal("5.5", document.Read(handle).Get("number") ?? "—",
                "отвергнутые изменения не дошли до документа");
        }

        private static void TransactionAbortRestores(IHostUnderTest host, IContractCheck check)
        {
            var document = host.Document;
            CreateSign(document, "3.24", 10);
            var before = document.Snapshot();
            var transaction = document.BeginTransaction("MoveSign");
            var extra = transaction.Create(SignType, "ODD_SIGN", new List<CadProperty>
            {
                new CadProperty("number", "1.1"),
            });
            transaction.Set(document.Query(SignType)[0], "x", "999");
            transaction.Abort();
            check.Equal(before, document.Snapshot(), "после Abort документ совпадает с исходным побайтно");
            check.Throws<CadObjectMissingException>(() => document.Read(extra),
                "объект из отменённой транзакции не существует");
        }

        private static void UndoRestoresInitial(IHostUnderTest host, IContractCheck check)
        {
            var document = host.Document;
            var initial = document.Snapshot();
            CreateSign(document, "8.1.1", 42);
            var changed = document.Snapshot();
            check.True(initial != changed, "команда действительно изменила документ");
            check.True(document.CanUndo, "есть что отменять");
            document.Undo();
            check.Equal(initial, document.Snapshot(), "после отмены снимок равен исходному");
        }

        private static void RedoRestoresResult(IHostUnderTest host, IContractCheck check)
        {
            var document = host.Document;
            CreateSign(document, "8.1.1", 42);
            var changed = document.Snapshot();
            document.Undo();
            check.True(document.CanRedo, "есть что повторять");
            document.Redo();
            check.Equal(changed, document.Snapshot(), "после повтора снимок равен результату команды");
        }

        private static void EventsAreMerged(IHostUnderTest host, IContractCheck check)
        {
            var changes = new List<CadChange>();
            var sink = new RecordingSink(changes);
            host.Events.Subscribe(sink);
            try
            {
                var transaction = host.Document.BeginTransaction("EditSign");
                var handle = transaction.Create(SignType, "ODD_SIGN", new List<CadProperty>
                {
                    new CadProperty("number", "3.24"),
                });
                transaction.Set(handle, "x", "10");
                transaction.Set(handle, "y", "20");
                transaction.Commit();
                check.Equal("1", Count(changes, handle), "на объект пришло ровно одно уведомление");
                check.Equal("Created", changes[0].Kind.ToString(), "вид уведомления — создание");
            }
            finally
            {
                host.Events.Unsubscribe(sink);
            }
        }

        private static void ReentrancyIsRejected(IHostUnderTest host, IContractCheck check)
        {
            var document = host.Document;
            var rejected = 0;
            var sink = new DelegateSink(change =>
            {
                var attempt = document.BeginTransaction("FromNotification");
                try
                {
                    attempt.Create(SignType, "ODD_SIGN", new List<CadProperty>
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
                CreateSign(document, "3.24", 0);
            }
            finally
            {
                host.Events.Unsubscribe(sink);
            }
            check.Equal("1", rejected.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "изменение внутри уведомления отвергнуто");
            check.Equal("1", document.Query(SignType).Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "отвергнутая попытка не создала объект");
        }

        private static void DeferredAfterCommit(IHostUnderTest host, IContractCheck check)
        {
            var events = host.Events;
            events.ClearDeferred();
            var handle = CreateSign(host.Document, "3.24", 0);
            var deferred = events.Deferred;
            check.True(deferred.Count > 0, "после фиксации есть отложенная очередь пересчёта");
            var found = false;
            foreach (var change in deferred)
            {
                if (change.Handle.Equals(handle)) found = true;
            }
            check.True(found, "в очереди есть изменение созданного объекта");
            events.ClearDeferred();
            check.Equal("0", events.Deferred.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "очередь очищается после пересчёта");
        }

        private static void PersistenceRoundTrip(IHostUnderTest host, IContractCheck check)
        {
            CreateSign(host.Document, "3.24", 120.5);
            CreateSign(host.Document, "8.2.2", -40);
            var before = host.Document.Snapshot();
            var json = host.Persistence.Save();
            var loaded = host.Persistence.Load(json);
            check.Equal("0", loaded.Warnings.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "загрузка текущей версии без предупреждений");
            check.Equal(before, host.Document.Snapshot(), "«сохранить → открыть» даёт тот же документ");
        }

        private static void PersistenceMigration(IHostUnderTest host, IContractCheck check)
        {
            const string old = "{\n  \"schemaVersion\": 1,\n  \"objects\": [\n    {\n      \"handle\": 1,\n" +
                               "      \"type\": \"Sign\",\n      \"properties\": { \"number\": \"3.1\" }\n    }\n  ]\n}";
            var loaded = host.Persistence.Load(old);
            check.Equal("1", loaded.Warnings.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "миграция сообщает о себе предупреждением");
            check.True(loaded.Warnings[0].Contains("миграция", StringComparison.OrdinalIgnoreCase),
                "предупреждение говорит о миграции");
            check.Equal("0", loaded.Objects[0].Layer, "слой по умолчанию 0");
            check.Equal("3.1", loaded.Objects[0].Get("number") ?? "—", "свойство сохранено миграцией");
        }

        private static void PersistenceWithoutModule(IHostUnderTest host, IContractCheck check)
        {
            CreateSign(host.Document, "3.24", 0);
            var json = host.Persistence.Save();
            var loaded = host.Persistence.Load(json, moduleAvailable: false);
            check.True(loaded.IsReadOnly, "без модуля документ открыт только для чтения");
            check.Equal("0", loaded.Objects[0].Properties.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "свойства недоступны: показан кэш графики");
            check.Equal(SignType, loaded.Objects[0].Type, "тип из кэша доступен");
        }

        private static string Count(List<CadChange> changes, CadHandle handle)
        {
            var count = 0;
            foreach (var change in changes)
            {
                if (change.Handle.Equals(handle)) count++;
            }
            return count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private sealed class RecordingSink : ICadEventSink
        {
            private readonly List<CadChange> _changes;

            public RecordingSink(List<CadChange> changes)
            {
                _changes = changes;
            }

            public void OnChanged(CadChange change) => _changes.Add(change);
        }

        private sealed class DelegateSink : ICadEventSink
        {
            private readonly Action<CadChange> _handler;

            public DelegateSink(Action<CadChange> handler)
            {
                _handler = handler;
            }

            public void OnChanged(CadChange change) => _handler(change);
        }
    }
}
