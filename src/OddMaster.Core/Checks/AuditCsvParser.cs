using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Checks
{
    /// <summary>Разбор CSV-фикстуры проверки: BOM, «;», запятая в дроби, кавычки.</summary>
    public static class AuditCsvParser
    {
        /// <summary>Разбирает CSV в записи схемы. Первая строка — заголовок.</summary>
        public static AuditRequest Parse(string csv, string name)
        {
            if (csv == null) throw new ArgumentNullException(nameof(csv));
            var text = csv.Length > 0 && csv[0] == '\uFEFF' ? csv.Substring(1) : csv;
            var rows = SplitRows(text);
            const string expectedHeader = "id;number;sizeClass;parameter;value;station;offset;side";
            if (rows.Count == 0 || string.Join(";", rows[0].Select(x => x.Trim())) != expectedHeader)
                throw new OddMaster.Core.Config.ConfigException("fixture.csv", "/header",
                    "неожиданный заголовок CSV", "используйте заголовок «" + expectedHeader + "»");
            var entries = new List<SchemeEntry>();
            for (int i = 1; i < rows.Count; i++)
            {
                var cells = rows[i];
                if (cells.Count < 8 || string.IsNullOrWhiteSpace(cells[0])) continue;
                SignNumber number;
                try { number = SignNumber.Parse(cells[1].Trim()); }
                catch (FormatException ex)
                {
                    throw new OddMaster.Core.Config.ConfigException("fixture.csv", "/row" + i.ToString(CultureInfo.InvariantCulture) + "/number",
                        ex.Message, "укажите номер вида 3.24 или 8.2.1");
                }
                var parameterKind = string.IsNullOrWhiteSpace(cells[3]) ? null : cells[3].Trim();
                decimal? parameterValue = string.IsNullOrWhiteSpace(cells[4])
                    ? (decimal?)null : ParseDecimal(cells[4]);
                entries.Add(new SchemeEntry(cells[0].Trim(), number, cells[2].Trim(),
                    parameterKind, parameterValue, ParseDouble(cells[5]), ParseDouble(cells[6]),
                    cells[7].Trim()));
            }
            return new AuditRequest(name, entries);
        }

        private static List<List<string>> SplitRows(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new System.Text.StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else cell.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ';') { row.Add(cell.ToString()); cell.Clear(); }
                else if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(cell.ToString()); cell.Clear();
                    if (row.Count > 0) rows.Add(row);
                    row = new List<string>();
                }
                else cell.Append(c);
            }
            if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
            return rows;
        }

        private static double ParseDouble(string value) =>
            double.Parse(value.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);

        private static decimal ParseDecimal(string value) =>
            decimal.Parse(value.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
