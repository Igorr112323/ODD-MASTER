using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Actualizer
{
    /// <summary>Импорт таблицы различий студента CSV → карта замен (Q-008/G4).</summary>
    public static class DiffMapImporter
    {
        /// <summary>Разбирает CSV с 8 колонками и строит боевую карту замен.</summary>
        public static MigrationMap Import(string csv, string title = "Карта замен 2004 → 2024 (из CSV)")
        {
            if (csv == null) throw new ArgumentNullException(nameof(csv));
            var text = csv.Length > 0 && csv[0] == '\uFEFF' ? csv.Substring(1) : csv;
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var rules = new List<MigrationRule>();
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                var cells = line.Split(';');
                if (cells.Length < 8)
                    throw new FormatException("Строка " + (i + 1) + ": ожидается 8 колонок, получено " + cells.Length);
                var oldNumber = cells[0].Trim();
                var newNumber = cells[2].Trim();
                var categoryRaw = cells[4].Trim();
                if (oldNumber.Length == 0) continue;
                if (string.Equals(categoryRaw, "Unchanged", StringComparison.OrdinalIgnoreCase)) continue;
                var category = ParseCategory(categoryRaw);
                var note = cells[6].Trim();
                var basis = cells[5].Trim();
                var status = cells[7].Trim();
                if (status.Length == 0) status = "Unverified";
                var verification = status == "StudentVerified" ? Verification.StudentVerified
                    : status == "CuratorExtracted" ? Verification.CuratorExtracted
                    : status == "Placeholder" ? Verification.Placeholder : Verification.Unverified;
                var oldSet = SignNumberSet.Parse(oldNumber);
                SignNumberSet? newSet = newNumber.Length > 0 && newNumber != "-" && newNumber != "null"
                    ? SignNumberSet.Parse(newNumber) : null;
                rules.Add(new MigrationRule("DIFF-" + i.ToString(CultureInfo.InvariantCulture),
                    oldSet, new List<string>(), newSet, new List<string>(),
                    note.Length > 0 ? note : basis, category,
                    new NormRef("таблица различий студента", basis.Length > 0 ? basis : "основание не указано"),
                    verification, "импортировано из data/diff-2004-2024.csv; категория " + categoryRaw));
            }
            return new MigrationMap("DIFF-2004-2024", title,
                new NormRef("ГОСТ Р 52290-2024", "таблица различий 2004 → 2024 (G4)"),
                Verification.Unverified,
                "Карта замен не сверена студентом по таблице различий — ЧЕРНОВИК.",
                "Импортировано из CSV; строки Unchanged не создают правил.", false,
                rules, new List<string> { "GOST-018" });
        }

        private static MigrationCategory ParseCategory(string raw)
        {
            if (Enum.TryParse<MigrationCategory>(raw, true, out var category)) return category;
            // В шаблоне есть Renamed; в движке она пока равносильна Replaced с пояснением (ADR-0024).
            if (string.Equals(raw, "Renamed", StringComparison.OrdinalIgnoreCase)) return MigrationCategory.Replaced;
            return MigrationCategory.NeedsDecision;
        }
    }
}
