using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OddMaster.Core.Norms;
using OddMaster.Core.Statement;

namespace OddMaster.Core.Checks
{
    /// <summary>Отчёт аудитора: отсортированные находки + баннер о черновике.</summary>
    public sealed class AuditReport
    {
        /// <summary>Создаёт отчёт. Находки сортируются по пикету, стороне/смещению и правилу.</summary>
        public AuditReport(string name, IEnumerable<AuditFinding> findings)
        {
            Name = name;
            Findings = (findings ?? Enumerable.Empty<AuditFinding>())
                .OrderBy(f => f.StationMeters)
                .ThenBy(f => f.Location, StringComparer.Ordinal)
                .ThenBy(f => f.RuleId, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Название схемы.</summary>
        public string Name { get; }
        /// <summary>Отсортированные находки.</summary>
        public IReadOnlyList<AuditFinding> Findings { get; }
        /// <summary>Истина, если хотя бы одно правило не сверено.</summary>
        public bool HasUnverified => Findings.Any(f => f.IsDraft);
        /// <summary>Баннер для всех выходных форматов.</summary>
        public string Banner => HasUnverified
            ? "Часть правил не проверена — ЧЕРНОВИК, не применять без проверки."
            : "Все правила проверены.";

        /// <summary>CSV для Excel.</summary>
        public string ToCsv()
        {
            var lines = new List<IEnumerable<string>>
            {
                new[] { "Правило", "Важность", "Пикет, м", "Место", "Сообщение", "Норма", "Проверка", "Маркер" }
            };
            foreach (var f in Findings)
                lines.Add(new[]
                {
                    f.RuleId, f.Severity.ToString(), CsvWriter.Number(f.StationMeters), f.Location,
                    (f.IsDraft ? "[не проверено по ГОСТ] " : "") + f.Message,
                    f.NormRef?.ToString() ?? "", f.Verification.ToString(), f.Marker ?? ""
                });
            return CsvWriter.Build(lines);
        }

        /// <summary>Отчёт в Markdown.</summary>
        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Отчёт проверки схемы: " + Name);
            sb.AppendLine();
            sb.AppendLine("> " + Banner);
            sb.AppendLine();
            sb.AppendLine("Findings: " + Findings.Count.ToString(CultureInfo.InvariantCulture)
                + "; ошибок: " + Findings.Count(f => f.Severity == FindingSeverity.Error)
                + "; предупреждений: " + Findings.Count(f => f.Severity == FindingSeverity.Warning));
            sb.AppendLine();
            sb.AppendLine("| Правило | Важность | Место | Сообщение | Норма | Проверка |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var f in Findings)
                sb.AppendLine("| " + f.RuleId + " | " + f.Severity + " | " + Escape(f.Location) + " | "
                    + (f.IsDraft ? "**[не проверено по ГОСТ]** " : "") + Escape(f.Message) + " | "
                    + Escape(f.NormRef?.ToString() ?? "") + " | " + f.Verification
                    + (f.Marker != null ? " (" + f.Marker + ")" : "") + " |");
            return sb.ToString().Replace("\r\n", "\n");
        }

        private static string Escape(string? value) =>
            (value ?? string.Empty).Replace("|", "\\|", StringComparison.Ordinal);
    }
}
