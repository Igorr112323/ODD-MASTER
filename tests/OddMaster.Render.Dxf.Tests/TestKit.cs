using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace OddMaster.TestKit
{
    /// <summary>Корень репозитория и детерминированное чтение файлов для тестов стенда.</summary>
    public static class Repo
    {
        /// <summary>Абсолютный путь к корню репозитория (где лежит OddMaster.sln).</summary>
        public static string Root()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "OddMaster.sln")))
                dir = dir.Parent;
            if (dir == null) throw new InvalidOperationException("Не найден корень репозитория (OddMaster.sln)");
            return dir.FullName;
        }

        /// <summary>Читает файл репозитория как UTF-8 с нормализованными переводами строк.</summary>
        public static string ReadText(string relativePath) =>
            File.ReadAllText(Path.Combine(Root(), relativePath), Encoding.UTF8).Replace("\r\n", "\n");

        /// <summary>Проверяет существование файла репозитория.</summary>
        public static bool Exists(string relativePath) => File.Exists(Path.Combine(Root(), relativePath));
    }

    /// <summary>Тонкая обёртка над Xunit.Assert с русскими сообщениями (CR-03).</summary>
    public static class TestAssert
    {
        /// <summary>Проверяет истинность.</summary>
        public static void True(bool condition, string message = "ожидалось true")
        {
            Assert.True(condition, "Проверка не прошла: " + message);
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

    /// <summary>Разбор DXF на пары групповых кодов — то, что делает любой сторонний читатель.</summary>
    public static class DxfPairs
    {
        /// <summary>Читает пары «код → значение» из текста DXF.</summary>
        public static List<KeyValuePair<int, string>> Parse(string content)
        {
            var lines = content.Replace("\r\n", "\n").Split('\n');
            var result = new List<KeyValuePair<int, string>>();
            for (var i = 0; i + 1 < lines.Length; i += 2)
            {
                var codeText = lines[i].Trim();
                if (codeText.Length == 0) continue;
                var code = int.Parse(codeText, CultureInfo.InvariantCulture);
                result.Add(new KeyValuePair<int, string>(code, lines[i + 1]));
            }
            return result;
        }

        /// <summary>Имена всех сущностей в секции ENTITIES.</summary>
        public static List<string> EntityTypes(string content)
        {
            var pairs = Parse(content);
            var inEntities = false;
            var result = new List<string>();
            for (var i = 0; i < pairs.Count; i++)
            {
                if (pairs[i].Key == 0 && pairs[i].Value == "SECTION" && i + 1 < pairs.Count
                    && pairs[i + 1].Key == 2 && pairs[i + 1].Value == "ENTITIES")
                {
                    inEntities = true;
                    continue;
                }
                if (!inEntities) continue;
                if (pairs[i].Key == 0 && pairs[i].Value == "ENDSEC") break;
                if (pairs[i].Key == 0) result.Add(pairs[i].Value);
            }
            return result;
        }

        /// <summary>Имена слоёв из таблицы LAYER.</summary>
        public static List<string> LayerNames(string content)
        {
            var pairs = Parse(content);
            var result = new List<string>();
            var inLayerTable = false;
            for (var i = 0; i < pairs.Count; i++)
            {
                if (pairs[i].Key == 2 && pairs[i].Value == "LAYER" && i >= 1 && pairs[i - 1].Key == 0
                    && pairs[i - 1].Value == "TABLE")
                {
                    inLayerTable = true;
                    continue;
                }
                if (inLayerTable && pairs[i].Key == 0 && pairs[i].Value == "ENDTAB") break;
                if (inLayerTable && pairs[i].Key == 2) result.Add(pairs[i].Value);
            }
            return result;
        }

        /// <summary>Слои, на которые ссылаются сущности (код 8), кроме объявлений таблиц.</summary>
        public static List<string> UsedLayers(string content)
        {
            var pairs = Parse(content);
            var inEntities = false;
            var result = new List<string>();
            for (var i = 0; i < pairs.Count; i++)
            {
                if (pairs[i].Key == 0 && pairs[i].Value == "SECTION" && i + 1 < pairs.Count
                    && pairs[i + 1].Key == 2 && pairs[i + 1].Value == "ENTITIES")
                {
                    inEntities = true;
                    continue;
                }
                if (!inEntities) continue;
                if (pairs[i].Key == 0 && pairs[i].Value == "ENDSEC") break;
                if (pairs[i].Key == 8) result.Add(pairs[i].Value);
            }
            return result;
        }

        /// <summary>Высоты текста (код 40) только из секции ENTITIES: в таблице LTYPE тоже есть код 40.</summary>
        public static List<double> TextHeights(string content)
        {
            var pairs = Parse(content);
            var result = new List<double>();
            var inEntities = false;
            foreach (var pair in pairs)
            {
                if (pair.Key == 0 && pair.Value == "SECTION") { inEntities = false; continue; }
                if (pair.Key == 2 && pair.Value == "ENTITIES") { inEntities = true; continue; }
                if (!inEntities) continue;
                if (pair.Key == 0 && pair.Value == "ENDSEC") { inEntities = false; continue; }
                if (pair.Key == 40)
                    result.Add(double.Parse(pair.Value, CultureInfo.InvariantCulture));
            }
            return result;
        }

        /// <summary>
        /// Все значения текста (код 1) ТОЛЬКО из секции ENTITIES: код 1 встречается ещё
        /// в HEADER ($ACADVER = AC1009), и считать его надписью нельзя.
        /// </summary>
        public static List<string> Texts(string content)
        {
            var pairs = Parse(content);
            var result = new List<string>();
            var inEntities = false;
            foreach (var pair in pairs)
            {
                if (pair.Key == 0 && pair.Value == "SECTION") { inEntities = false; continue; }
                if (pair.Key == 2 && pair.Value == "ENTITIES") { inEntities = true; continue; }
                if (!inEntities) continue;
                if (pair.Key == 0 && pair.Value == "ENDSEC") { inEntities = false; continue; }
                if (pair.Key == 1) result.Add(pair.Value);
            }
            return result;
        }

        /// <summary>Значение переменной заголовка по имени (например, $ACADVER).</summary>
        public static string? HeaderVariable(string content, string name)
        {
            var pairs = Parse(content);
            for (var i = 0; i + 1 < pairs.Count; i++)
                if (pairs[i].Key == 9 && pairs[i].Value == name) return pairs[i + 1].Value;
            return null;
        }
    }
}
