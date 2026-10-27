using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OddMaster.TestKit
{
    /// <summary>Пометка теста. Свой микро-раннер вместо xUnit: NuGet в среде агента недоступен (ADR-0014).</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class OddFactAttribute : Attribute
    {
        /// <summary>Создаёт пометку теста.</summary>
        public OddFactAttribute(string name) { Name = name; }

        /// <summary>Имя теста для отчёта.</summary>
        public string Name { get; }
    }

    /// <summary>Минимальный набор проверок. Любая неудача бросает исключение с русским сообщением.</summary>
    public static class Assert
    {
        /// <summary>Проверяет истинность.</summary>
        public static void True(bool condition, string message = "ожидалось true")
        {
            if (!condition) throw new InvalidOperationException("Проверка не прошла: " + message);
        }

        /// <summary>Проверяет ложность.</summary>
        public static void False(bool condition, string message = "ожидалось false")
        {
            if (condition) throw new InvalidOperationException("Проверка не прошла: " + message);
        }

        /// <summary>Проверяет равенство значений.</summary>
        public static void Equal<T>(T expected, T actual, string message = "")
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(
                    $"Проверка не прошла: ожидалось «{expected}», получено «{actual}». {message}");
        }

        /// <summary>Проверяет равенство double с допуском.</summary>
        public static void Close(double expected, double actual, double tolerance, string message = "")
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new InvalidOperationException(
                    $"Проверка не прошла: ожидалось {expected.ToString("R", CultureInfo.InvariantCulture)}, " +
                    $"получено {actual.ToString("R", CultureInfo.InvariantCulture)}, допуск {tolerance}. {message}");
        }

        /// <summary>Проверяет, что строка содержит подстроку.</summary>
        public static void Contains(string expectedPart, string actual, string message = "")
        {
            if (actual == null || !actual.Contains(expectedPart, StringComparison.Ordinal))
                throw new InvalidOperationException($"Проверка не прошла: строка не содержит «{expectedPart}». {message}");
        }

        /// <summary>Проверяет, что строка НЕ содержит подстроку.</summary>
        public static void NotContains(string forbiddenPart, string actual, string message = "")
        {
            if (actual != null && actual.Contains(forbiddenPart, StringComparison.Ordinal))
                throw new InvalidOperationException($"Проверка не прошла: строка содержит «{forbiddenPart}». {message}");
        }

        /// <summary>Проверяет, что значение не null.</summary>
        public static void NotNull(object? value, string message = "ожидалось не null")
        {
            if (value == null) throw new InvalidOperationException("Проверка не прошла: " + message);
        }

        /// <summary>Проверяет, что действие бросает исключение заданного типа.</summary>
        public static TException Throws<TException>(Action action) where TException : Exception
        {
            try { action(); }
            catch (TException ex) { return ex; }
            catch (Exception other)
            {
                throw new InvalidOperationException(
                    $"Ожидалось исключение {typeof(TException).Name}, получено {other.GetType().Name}");
            }
            throw new InvalidOperationException($"Ожидалось исключение {typeof(TException).Name}, но его не было");
        }
    }

    /// <summary>Поиск корня репозитория: тесты запускаются и из корня, и из папки проекта.</summary>
    public static class Repo
    {
        /// <summary>Возвращает абсолютный путь к корню репозитория (где лежит OddMaster.sln).</summary>
        public static string Root()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "OddMaster.sln")))
                dir = dir.Parent;
            if (dir == null) throw new InvalidOperationException("Не найден корень репозитория (OddMaster.sln)");
            return dir.FullName;
        }

        /// <summary>Читает файл репозитория как текст UTF-8.</summary>
        public static string ReadText(string relativePath) =>
            File.ReadAllText(Path.Combine(Root(), relativePath), Encoding.UTF8);

        /// <summary>Читает файл репозитория как байты.</summary>
        public static byte[] ReadBytes(string relativePath) =>
            File.ReadAllBytes(Path.Combine(Root(), relativePath));

        /// <summary>Проверяет существование файла репозитория.</summary>
        public static bool Exists(string relativePath) =>
            File.Exists(Path.Combine(Root(), relativePath));
    }

    /// <summary>Сверка с эталонным файлом (golden). При расхождении печатает понятный отчёт.</summary>
    public static class Golden
    {
        /// <summary>Сверяет текст с эталоном. Если эталона нет, он создаётся (первый прогон).</summary>
        public static void CheckText(string relativePath, string actual)
        {
            var full = Path.Combine(Repo.Root(), relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full) ?? Repo.Root());
            if (!File.Exists(full))
            {
                File.WriteAllText(full, actual, new UTF8Encoding(false));
                return;
            }
            var expected = File.ReadAllText(full, Encoding.UTF8);
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                var lineExpected = expected.Split('\n');
                var lineActual = actual.Split('\n');
                var first = Enumerable.Range(0, Math.Min(lineExpected.Length, lineActual.Length))
                    .FirstOrDefault(i => !string.Equals(lineExpected[i], lineActual[i], StringComparison.Ordinal));
                throw new InvalidOperationException(
                    $"Golden-файл {relativePath} не совпал (строка {first + 1}). " +
                    $"Ожидалось: «{Trim(lineExpected, first)}». Получено: «{Trim(lineActual, first)}». " +
                    "Если изменение намеренное — обновите эталон и опишите в WORKLOG.");
            }
        }

        private static string Trim(string[] lines, int index) =>
            index < lines.Length ? (lines[index].Length > 160 ? lines[index].Substring(0, 160) + "…" : lines[index]) : "<конец файла>";
    }

    /// <summary>Запускает все методы с OddFact в сборке и печатает результат.</summary>
    public static class TestRunner
    {
        /// <summary>Точка входа тестового проекта. Возвращает 0 при полном успехе.</summary>
        public static int Run(Assembly assembly)
        {
            var methods = assembly.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Where(m => m.GetCustomAttribute<OddFactAttribute>() != null)
                .OrderBy(m => m.DeclaringType!.FullName, StringComparer.Ordinal)
                .ThenBy(m => m.Name, StringComparer.Ordinal)
                .ToList();
            int passed = 0;
            var failures = new List<string>();
            foreach (var method in methods)
            {
                var name = method.GetCustomAttribute<OddFactAttribute>()!.Name;
                try
                {
                    method.Invoke(null, null);
                    passed++;
                    Console.WriteLine("  ПРОЙДЕН  " + name);
                }
                catch (TargetInvocationException ex)
                {
                    failures.Add(name + " -> " + (ex.InnerException?.Message ?? ex.Message));
                    Console.WriteLine("  УПАЛ     " + name + " -> " + (ex.InnerException?.Message ?? ex.Message));
                }
                catch (Exception ex)
                {
                    failures.Add(name + " -> " + ex.Message);
                    Console.WriteLine("  УПАЛ     " + name + " -> " + ex.Message);
                }
            }
            Console.WriteLine($"ИТОГО: {methods.Count} тестов, прошло {passed}, упало {failures.Count}, пропущено 0.");
            return failures.Count == 0 ? 0 : 1;
        }
    }
}
