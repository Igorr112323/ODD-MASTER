using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OddMaster.Core.Alignment;
using OddMaster.Core.Statement;

namespace OddMaster.Core.Scheme
{
    /// <summary>Строка журнала координат: опора, пикет, сторона, смещение, X, Y, знаки сверху вниз.</summary>
    public sealed class Placement
    {
        /// <summary>Создаёт строку журнала.</summary>
        public Placement(string postId, double stationMeters, double offsetMeters, string side,
            double x, double y, string signsTopDown, bool outside, StationFormat? format = null)
        {
            PostId = postId; StationMeters = stationMeters; OffsetMeters = offsetMeters;
            Side = side; X = x; Y = y; SignsTopDown = signsTopDown; Outside = outside;
            StationText = new Station(stationMeters).Format(format);
        }

        /// <summary>Идентификатор опоры.</summary>
        public string PostId { get; }
        /// <summary>Пикет, м.</summary>
        public double StationMeters { get; }
        /// <summary>Отформатированный пикет.</summary>
        public string StationText { get; }
        /// <summary>Смещение, м.</summary>
        public double OffsetMeters { get; }
        /// <summary>Сторона.</summary>
        public string Side { get; }
        /// <summary>Координата X, м.</summary>
        public double X { get; }
        /// <summary>Координата Y, м.</summary>
        public double Y { get; }
        /// <summary>Знаки сверху вниз.</summary>
        public string SignsTopDown { get; }
        /// <summary>Опора за пределами оси.</summary>
        public bool Outside { get; }
    }

    /// <summary>Журнал координат дислокации знаков.</summary>
    public sealed class CoordinateLog
    {
        /// <summary>Создаёт журнал.</summary>
        public CoordinateLog(IEnumerable<Placement> rows)
        {
            Rows = rows == null ? new List<Placement>() : new List<Placement>(rows);
        }

        /// <summary>Строки в порядке опор.</summary>
        public IReadOnlyList<Placement> Rows { get; }

        /// <summary>CSV для Excel (BOM, «;», запятая в дроби).</summary>
        public string ToCsv()
        {
            var rows = new List<IEnumerable<string>>
            {
                new[] { "Опора", "Пикет", "Сторона", "Смещение, м", "X, м", "Y, м", "Знаки сверху вниз", "За пределами оси" }
            };
            foreach (var r in Rows)
                rows.Add(new[]
                {
                    r.PostId, r.StationText, r.Side, CsvWriter.Number(r.OffsetMeters),
                    CsvWriter.Number(r.X), CsvWriter.Number(r.Y), r.SignsTopDown,
                    r.Outside ? "да" : "нет"
                });
            return CsvWriter.Build(rows);
        }

        /// <summary>Markdown-таблица журнала.</summary>
        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("| Опора | Пикет | Сторона | Смещение, м | X, м | Y, м | Знаки сверху вниз |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var r in Rows)
                sb.AppendLine("| " + r.PostId + " | " + r.StationText + " | " + r.Side + " | "
                    + CsvWriter.Number(r.OffsetMeters) + " | " + CsvWriter.Number(r.X) + " | "
                    + CsvWriter.Number(r.Y) + " | " + r.SignsTopDown + " |");
            return sb.ToString().Replace("\r\n", "\n");
        }

        /// <summary>Детерминированный diff «до/после» для реактора.</summary>
        public string Diff(CoordinateLog other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            var before = Rows.ToDictionary(r => r.PostId, StringComparer.Ordinal);
            var after = other.Rows.ToDictionary(r => r.PostId, StringComparer.Ordinal);
            var lines = new List<string>();
            foreach (var id in before.Keys.Concat(after.Keys).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
            {
                if (!before.TryGetValue(id, out var b)) { lines.Add("+ " + id + ": " + Describe(after[id])); continue; }
                if (!after.TryGetValue(id, out var a)) { lines.Add("- " + id + ": " + Describe(b)); continue; }
                var changes = new List<string>();
                AddChange(changes, "X", b.X, a.X);
                AddChange(changes, "Y", b.Y, a.Y);
                if (!string.Equals(b.StationText, a.StationText, StringComparison.Ordinal))
                    changes.Add("пикет " + b.StationText + " → " + a.StationText);
                if (changes.Count > 0)
                    lines.Add("~ " + id + ": " + string.Join("; ", changes));
            }
            return string.Join("\n", lines) + (lines.Count > 0 ? "\n" : "");
        }

        private static void AddChange(List<string> changes, string name, double before, double after)
        {
            if (Math.Abs(before - after) < 1e-9) return;
            changes.Add(name + " " + before.ToString("0.##", CultureInfo.InvariantCulture)
                + " → " + after.ToString("0.##", CultureInfo.InvariantCulture));
        }

        private static string Describe(Placement p) =>
            p.StationText + "; X=" + p.X.ToString("0.##", CultureInfo.InvariantCulture)
            + "; Y=" + p.Y.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
