using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace OddMaster.TestKit
{
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
            File.ReadAllText(Path.Combine(Root(), relativePath), Encoding.UTF8)
                .Replace("\r\n", "\n");

        /// <summary>Читает файл репозитория как байты.</summary>
        public static byte[] ReadBytes(string relativePath) =>
            File.ReadAllBytes(Path.Combine(Root(), relativePath));

        /// <summary>Проверяет существование файла репозитория.</summary>
        public static bool Exists(string relativePath) =>
            File.Exists(Path.Combine(Root(), relativePath));
    }

    /// <summary>
    /// Сверка с эталонным файлом (golden, CR-10). Эталон обновляется только вместе
    /// с docs/visual/REVIEW-NN.md через ODD_UPDATE_GOLDEN=1.
    /// Git хранит golden с LF, поэтому сравниваем после нормализации переводов строк.
    /// </summary>
    public static class Golden
    {
        /// <summary>Сверяет текст с эталоном. Отсутствие эталона — ошибка, а не тихое создание.</summary>
        public static void CheckText(string relativePath, string actual)
        {
            var full = Path.Combine(Repo.Root(), relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full) ?? Repo.Root());
            var normalized = Normalize(actual);
            if (!File.Exists(full))
            {
                if (Environment.GetEnvironmentVariable("ODD_UPDATE_GOLDEN") == "1")
                {
                    File.WriteAllBytes(full, new UTF8Encoding(false).GetBytes(normalized));
                    return;
                }
                throw new InvalidOperationException(
                    "Отсутствует golden-файл " + relativePath +
                    ". Создайте эталон командой ODD_UPDATE_GOLDEN=1 dotnet test и приложите docs/visual/REVIEW-NN.md.");
            }
            var expected = Normalize(new UTF8Encoding(false).GetString(File.ReadAllBytes(full)));
            if (!string.Equals(expected, normalized, StringComparison.Ordinal))
            {
                var lineExpected = expected.Split('\n');
                var lineActual = normalized.Split('\n');
                var first = Enumerable.Range(0, Math.Min(lineExpected.Length, lineActual.Length))
                    .FirstOrDefault(i => !string.Equals(lineExpected[i], lineActual[i], StringComparison.Ordinal));
                throw new InvalidOperationException(
                    $"Golden-файл {relativePath} не совпал (строка {first + 1}). " +
                    $"Ожидалось: «{Trim(lineExpected, first)}». Получено: «{Trim(lineActual, first)}». " +
                    "Эталон обновляется только вместе с docs/visual/REVIEW-NN.md.");
            }
        }

        private static string Normalize(string text) =>
            text.Replace("\r\n", "\n").Replace("\r", "\n");

        private static string Trim(string[] lines, int index) =>
            index < lines.Length ? (lines[index].Length > 160 ? lines[index].Substring(0, 160) + "…" : lines[index]) : "<конец файла>";
    }

    /// <summary>
    /// Тонкая обёртка над Xunit.Assert: сохраняет русские сообщения прогона №1 и
    /// методы Close/NotContains, которых нет в xUnit. Все проверки завершаются
    /// настоящим Xunit.Assert — самописного раннера больше нет (CR-03).
    /// </summary>
    public static class TestAssert
    {
        /// <summary>Проверяет истинность.</summary>
        public static void True(bool condition, string message = "ожидалось true")
        {
            Assert.True(condition, "Проверка не прошла: " + message);
        }

        /// <summary>Проверяет ложность.</summary>
        public static void False(bool condition, string message = "ожидалось false")
        {
            Assert.False(condition, "Проверка не прошла: " + message);
        }

        /// <summary>Проверяет равенство значений.</summary>
        public static void Equal<T>(T expected, T actual, string message = "")
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                Assert.Fail($"Проверка не прошла: ожидалось «{expected}», получено «{actual}». {message}");
        }

        /// <summary>Проверяет равенство double с допуском.</summary>
        public static void Close(double expected, double actual, double tolerance, string message = "")
        {
            Assert.InRange(actual, expected - tolerance, expected + tolerance);
        }

        /// <summary>Проверяет, что строка содержит подстроку.</summary>
        public static void Contains(string expectedPart, string actual, string message = "")
        {
            if (actual == null || !actual.Contains(expectedPart, StringComparison.Ordinal))
                Assert.Fail($"Проверка не прошла: строка не содержит «{expectedPart}». {message}");
        }

        /// <summary>Проверяет, что строка НЕ содержит подстроку.</summary>
        public static void NotContains(string forbiddenPart, string actual, string message = "")
        {
            if (actual != null && actual.Contains(forbiddenPart, StringComparison.Ordinal))
                Assert.Fail($"Проверка не прошла: строка содержит «{forbiddenPart}». {message}");
        }

        /// <summary>Проверяет, что значение не null.</summary>
        public static void NotNull(object? value, string message = "ожидалось не null")
        {
            if (value == null) Assert.Fail("Проверка не прошла: " + message);
        }

        /// <summary>Проверяет, что действие бросает исключение заданного типа.</summary>
        public static TException Throws<TException>(Action action) where TException : Exception =>
            Assert.Throws<TException>(action);
    }
}
