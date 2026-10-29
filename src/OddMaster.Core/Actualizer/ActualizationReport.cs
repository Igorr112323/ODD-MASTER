using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OddMaster.Core.Statement;

namespace OddMaster.Core.Actualizer
{
    /// <summary>Один элемент старой схемы после актуализации.</summary>
    public sealed class ActualizationItem
    {
        /// <summary>Создаёт элемент отчёта.</summary>
        public ActualizationItem(string elementId, string blockName, string? recognizedNumber,
            double confidence, string patternId, MigrationCategory category, string? resultSigns,
            string? ruleId, string note)
        {
            ElementId = elementId; BlockName = blockName; RecognizedNumber = recognizedNumber;
            Confidence = confidence; PatternId = patternId; Category = category;
            ResultSigns = resultSigns; RuleId = ruleId; Note = note;
        }

        /// <summary>Идентификатор элемента.</summary>
        public string ElementId { get; }
        /// <summary>Имя блока в старом DWG.</summary>
        public string BlockName { get; }
        /// <summary>Распознанный номер.</summary>
        public string? RecognizedNumber { get; }
        /// <summary>Уверенность распознавания.</summary>
        public double Confidence { get; }
        /// <summary>Шаблон, которым распознано.</summary>
        public string PatternId { get; }
        /// <summary>Категория решения.</summary>
        public MigrationCategory Category { get; }
        /// <summary>Новый номер(а).</summary>
        public string? ResultSigns { get; }
        /// <summary>Правило карты.</summary>
        public string? RuleId { get; }
        /// <summary>Пояснение.</summary>
        public string Note { get; }
    }

    /// <summary>Отчёт Актуализатора: шесть категорий, баннер и форматы CSV/MD.</summary>
    public sealed class ActualizationReport
    {
        /// <summary>Создаёт отчёт.</summary>
        public ActualizationReport(string mapTitle, bool isDemo, string banner,
            IEnumerable<ActualizationItem> items)
        {
            MapTitle = mapTitle; IsDemo = isDemo; Banner = banner;
            Items = (items ?? Enumerable.Empty<ActualizationItem>())
                .OrderBy(i => i.ElementId, StringComparer.Ordinal).ToList();
        }

        /// <summary>Название карты.</summary>
        public string MapTitle { get; }
        /// <summary>Истина для синтетической ДЕМО-карты.</summary>
        public bool IsDemo { get; }
        /// <summary>Баннер предупреждения.</summary>
        public string Banner { get; }
        /// <summary>Элементы в устойчивом порядке.</summary>
        public IReadOnlyList<ActualizationItem> Items { get; }

        /// <summary>Счётчики по категориям.</summary>
        public IReadOnlyDictionary<MigrationCategory, int> Counts
        {
            get
            {
                var result = new Dictionary<MigrationCategory, int>();
                foreach (var category in Enum.GetValues(typeof(MigrationCategory)).Cast<MigrationCategory>())
                    result[category] = Items.Count(i => i.Category == category);
                return result;
            }
        }

        /// <summary>CSV для Excel.</summary>
        public string ToCsv()
        {
            var lines = new List<IEnumerable<string>>
            {
                new[] { "Элемент", "Блок", "Распознано", "Уверенность", "Шаблон", "Категория", "Результат", "Правило", "Пояснение" }
            };
            foreach (var i in Items)
                lines.Add(new[]
                {
                    i.ElementId, i.BlockName, i.RecognizedNumber ?? "",
                    CsvWriter.Number(i.Confidence), i.PatternId, i.Category.ToString(),
                    i.ResultSigns ?? "", i.RuleId ?? "", i.Note
                });
            return CsvWriter.Build(lines);
        }

        /// <summary>Отчёт в Markdown.</summary>
        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Актуализация схемы 2004 → 2024" + (IsDemo ? " (ДЕМО)" : ""));
            sb.AppendLine();
            sb.AppendLine("> " + Banner);
            sb.AppendLine();
            sb.AppendLine("Карта: " + MapTitle + ".");
            sb.AppendLine();
            sb.AppendLine("| Категория | Количество |");
            sb.AppendLine("|---|---|");
            foreach (var category in Enum.GetValues(typeof(MigrationCategory)).Cast<MigrationCategory>())
                sb.AppendLine("| " + category + " | " + Counts[category].ToString(CultureInfo.InvariantCulture) + " |");
            sb.AppendLine();
            sb.AppendLine("| Элемент | Блок | Распознано | Уверенность | Категория | Результат | Пояснение |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var i in Items)
                sb.AppendLine("| " + i.ElementId + " | " + Escape(i.BlockName) + " | "
                    + (i.RecognizedNumber ?? "—") + " | " + CsvWriter.Number(i.Confidence) + " | "
                    + i.Category + " | " + (i.ResultSigns ?? "—") + " | " + Escape(i.Note) + " |");
            return sb.ToString().Replace("\r\n", "\n");
        }

        private static string Escape(string? value) =>
            (value ?? string.Empty).Replace("|", "\\|");
    }
}
