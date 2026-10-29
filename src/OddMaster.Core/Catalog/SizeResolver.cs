using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Catalog
{
    /// <summary>Одно совпадение «номер знака → строка таблицы → типоразмер».</summary>
    public sealed class SizeMatch
    {
        /// <summary>Создаёт совпадение.</summary>
        public SizeMatch(SizeTable table, SizeRow row, SizeSpec spec)
        {
            Table = table; Row = row; Spec = spec;
        }

        /// <summary>Таблица прил. Д.</summary>
        public SizeTable Table { get; }
        /// <summary>Строка таблицы.</summary>
        public SizeRow Row { get; }
        /// <summary>Размеры выбранного типоразмера.</summary>
        public SizeSpec Spec { get; }
    }

    /// <summary>Результат разрешения размеров: совпадения и находки (не исключения).</summary>
    public sealed class SizeResolution
    {
        /// <summary>Создаёт результат.</summary>
        public SizeResolution(List<SizeMatch> matches, List<Finding> findings, List<string> missingTables)
        {
            Matches = matches; Findings = findings; MissingTables = missingTables;
        }

        /// <summary>Найденные совпадения.</summary>
        public IReadOnlyList<SizeMatch> Matches { get; }
        /// <summary>Находки (нет типоразмера, несколько строк, пустой профиль).</summary>
        public IReadOnlyList<Finding> Findings { get; }
        /// <summary>Таблицы, где номер найден, но типоразмера нет.</summary>
        public IReadOnlyList<string> MissingTables { get; }
        /// <summary>Истина, если ровно одно совпадение.</summary>
        public bool IsUnambiguous => Matches.Count == 1;

        /// <summary>Идентификатор таблицы единственного совпадения.</summary>
        public string? TableId => Matches.Count == 1 ? Matches[0].Table.Id : null;
    }

    /// <summary>Разрешает номер знака в строку таблицы размеров и типоразмер из профиля.</summary>
    public static class SizeResolver
    {
        /// <summary>Ищет все строки таблиц, содержащие номер, и выбирает запрошенный типоразмер.</summary>
        public static SizeResolution Resolve(SizeProfile profile, SignNumber number, string sizeClass)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (number == null) throw new ArgumentNullException(nameof(number));
            if (sizeClass == null) throw new ArgumentNullException(nameof(sizeClass));
            var matches = new List<SizeMatch>();
            var findings = new List<Finding>();
            var missing = new List<string>();

            if (!profile.HasSizeClass(sizeClass))
                findings.Add(Finding.Error("R005",
                    $"Типоразмер «{sizeClass}» отсутствует в профиле {profile.Id}.",
                    number.ToString(), profile.NormRef, profile.Verification));

            if (profile.Tables.Count == 0)
            {
                findings.Add(Finding.Warning("R007",
                    $"Профиль {profile.Id} не содержит таблиц размеров — генерация невозможна.",
                    number.ToString(), profile.NormRef, profile.Verification, "GOST-015"));
                return new SizeResolution(matches, findings, missing);
            }

            foreach (var table in profile.Tables)
            {
                foreach (var row in table.Rows)
                {
                    if (!row.Signs.Contains(number)) continue;
                    if (row.Sizes.TryGetValue(sizeClass, out var spec))
                        matches.Add(new SizeMatch(table, row, spec));
                    else
                        missing.Add(table.Id);
                }
            }

            if (matches.Count == 0)
            {
                var where = missing.Count > 0 ? string.Join(", ", missing.Distinct()) : "ни одной таблицы";
                findings.Add(Finding.Error("R005",
                    $"Типоразмер «{sizeClass}» для знака {number} не предусмотрен ({where}).",
                    number.ToString(), profile.NormRef, profile.Verification));
            }
            else if (matches.Count > 1)
            {
                findings.Add(Finding.Warning("R007",
                    $"Знак {number} попал в несколько строк таблиц размеров: " +
                    string.Join(", ", matches.Select(m => m.Table.Id + "/строка " + m.Row.Index.ToString(CultureInfo.InvariantCulture))),
                    number.ToString(), profile.NormRef, profile.Verification, "GOST-010"));
            }
            return new SizeResolution(matches, findings, missing);
        }

        /// <summary>Строки таблиц, в которые попадает номер, без учёта типоразмера.</summary>
        public static List<string> FindRowLocations(SizeProfile profile, SignNumber number)
        {
            var result = new List<string>();
            foreach (var table in profile.Tables)
                foreach (var row in table.Rows)
                    if (row.Signs.Contains(number))
                        result.Add(table.Id + "/" + row.Index.ToString(CultureInfo.InvariantCulture));
            return result;
        }
    }
}
