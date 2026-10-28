using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OddMaster.Core.Alignment;
using OddMaster.Core.Catalog;
using OddMaster.Core.Norms;
using OddMaster.Core.Scheme;

namespace OddMaster.Core.Statement
{
    /// <summary>Строка ведомости ТС ОДД. Форма колонок — черновик (GOST-011).</summary>
    public sealed class StatementRow
    {
        /// <summary>Создаёт строку.</summary>
        public StatementRow(int index, SignNumber number, string name, string sizeClass,
            string parameter, int count, string note)
        {
            Index = index; Number = number; Name = name; SizeClass = sizeClass;
            Parameter = parameter; Count = count; Note = note;
        }

        /// <summary>Порядковый номер.</summary>
        public int Index { get; }
        /// <summary>Номер знака.</summary>
        public SignNumber Number { get; }
        /// <summary>Наименование.</summary>
        public string Name { get; }
        /// <summary>Типоразмер.</summary>
        public string SizeClass { get; }
        /// <summary>Значение параметра.</summary>
        public string Parameter { get; }
        /// <summary>Количество.</summary>
        public int Count { get; }
        /// <summary>Примечание.</summary>
        public string Note { get; }
    }

    /// <summary>Перечень по опорам: опора, пикет, сторона, смещение, знаки сверху вниз.</summary>
    public sealed class PostStatementRow
    {
        /// <summary>Создаёт строку.</summary>
        public PostStatementRow(string postId, string stationText, string side, double offsetMeters, string signs)
        {
            PostId = postId; StationText = stationText; Side = side;
            OffsetMeters = offsetMeters; Signs = signs;
        }

        /// <summary>Опора.</summary>
        public string PostId { get; }
        /// <summary>Пикет.</summary>
        public string StationText { get; }
        /// <summary>Сторона.</summary>
        public string Side { get; }
        /// <summary>Смещение, м.</summary>
        public double OffsetMeters { get; }
        /// <summary>Знаки сверху вниз.</summary>
        public string Signs { get; }
    }

    /// <summary>Ведомость ТС ОДД: группировка по номер+типоразмер+параметр, натуральная сортировка (ADR-0011).</summary>
    public sealed class TsoddStatement
    {
        private static readonly string[] Headers =
        {
            "№", "Номер знака по ГОСТ Р 52290-2024", "Наименование", "Типоразмер", "Параметр", "Количество", "Примечание"
        };

        private TsoddStatement(List<StatementRow> rows, List<PostStatementRow> posts)
        {
            Rows = rows; Posts = posts;
        }

        /// <summary>Строки ведомости.</summary>
        public IReadOnlyList<StatementRow> Rows { get; }
        /// <summary>Перечень по опорам.</summary>
        public IReadOnlyList<PostStatementRow> Posts { get; }

        /// <summary>Строит ведомость из схемы и каталога.</summary>
        public static TsoddStatement Build(SchemeModel scheme, SignCatalog catalog, StationFormat? format = null)
        {
            if (scheme == null) throw new ArgumentNullException(nameof(scheme));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var groups = new Dictionary<string, (SignInstance Sign, int Count)>(StringComparer.Ordinal);
            foreach (var post in scheme.Posts)
                foreach (var sign in post.Mounts)
                {
                    var key = sign.StatementKey;
                    if (groups.TryGetValue(key, out var existing))
                        groups[key] = (existing.Sign, existing.Count + 1);
                    else groups[key] = (sign, 1);
                }

            var ordered = groups.Values
                .OrderBy(g => g.Sign.Number)
                .ThenBy(g => g.Sign.SizeClass, StringComparer.Ordinal)
                .ThenBy(g => g.Sign.ParameterDisplay, StringComparer.Ordinal)
                .ToList();
            var rows = new List<StatementRow>();
            for (int i = 0; i < ordered.Count; i++)
            {
                var sign = ordered[i].Sign;
                var definition = catalog.Find(sign.Number);
                var name = definition?.Name ?? "Нет в каталоге";
                var note = definition == null
                    ? "Нет в каталоге — требуется сверка"
                    : definition.Verification == Verification.StudentVerified ? "" : "ЧЕРНОВИК: данные не сверены";
                rows.Add(new StatementRow(i + 1, sign.Number, name, sign.SizeClass,
                    sign.ParameterDisplay, ordered[i].Count, note));
            }
            var posts = scheme.Posts.Select(p => new PostStatementRow(p.Id,
                new Station(p.StationMeters).Format(format), p.Side, p.OffsetMeters, p.SignsTopDown())).ToList();
            return new TsoddStatement(rows, posts);
        }

        /// <summary>Табличная модель для будущей таблицы на чертеже.</summary>
        public IReadOnlyList<IReadOnlyList<string>> ToTableModel()
        {
            var result = new List<IReadOnlyList<string>> { Headers.ToList() };
            foreach (var row in Rows)
                result.Add(new List<string>
                {
                    row.Index.ToString(CultureInfo.InvariantCulture), row.Number.ToString(), row.Name,
                    row.SizeClass, row.Parameter, row.Count.ToString(CultureInfo.InvariantCulture), row.Note
                });
            return result;
        }

        /// <summary>CSV для Excel (ADR-0012).</summary>
        public string ToCsv()
        {
            var lines = new List<IEnumerable<string>> { Headers };
            foreach (var row in Rows)
                lines.Add(new[]
                {
                    row.Index.ToString(CultureInfo.InvariantCulture), row.Number.ToString(), row.Name,
                    row.SizeClass, row.Parameter, row.Count.ToString(CultureInfo.InvariantCulture), row.Note
                });
            return CsvWriter.Build(lines);
        }

        /// <summary>Ведомость и перечень по опорам в Markdown.</summary>
        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Ведомость ТС ОДД (ЧЕРНОВИК)");
            sb.AppendLine();
            sb.AppendLine("Форма колонок не сверена с Приказом Минтранса №49 — GOST-011. " +
                "Знаки сгруппированы по номеру, типоразмеру и значению параметра.");
            sb.AppendLine();
            sb.AppendLine("| " + string.Join(" | ", Headers) + " |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var row in Rows)
                sb.AppendLine("| " + row.Index.ToString(CultureInfo.InvariantCulture) + " | " + row.Number
                    + " | " + MarkdownEscape(row.Name) + " | " + row.SizeClass + " | " + row.Parameter
                    + " | " + row.Count.ToString(CultureInfo.InvariantCulture) + " | " + MarkdownEscape(row.Note) + " |");
            sb.AppendLine();
            sb.AppendLine("## Перечень по опорам");
            sb.AppendLine();
            sb.AppendLine("| Опора | Пикет | Сторона | Смещение, м | Знаки сверху вниз |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var post in Posts)
                sb.AppendLine("| " + post.PostId + " | " + post.StationText + " | " + post.Side
                    + " | " + CsvWriter.Number(post.OffsetMeters) + " | " + MarkdownEscape(post.Signs) + " |");
            return sb.ToString().Replace("\r\n", "\n");
        }

        private static string MarkdownEscape(string? value) =>
            (value ?? string.Empty).Replace("|", "\\|");
    }
}
