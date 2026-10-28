using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OddMaster.Core.Catalog
{
    /// <summary>Строка полной номенклатуры из CSV (Q-008/P2).</summary>
    public sealed class NomenclatureRow
    {
        /// <summary>Создаёт строку.</summary>
        public NomenclatureRow(string number, string name, int group, string status)
        {
            Number = number; Name = name; Group = group; Status = status;
        }

        /// <summary>Номер знака.</summary>
        public string Number { get; }
        /// <summary>Наименование.</summary>
        public string Name { get; }
        /// <summary>Группа.</summary>
        public int Group { get; }
        /// <summary>Статус проверки.</summary>
        public string Status { get; }
    }

    /// <summary>Импортёр CSV-номенклатуры: полный список готовит куратор, агент не выдумывает названия.</summary>
    public static class NomenclatureImporter
    {
        /// <summary>Разбирает CSV с разделителем «;». Первая строка — заголовок.</summary>
        public static List<NomenclatureRow> Parse(string csv)
        {
            if (csv == null) throw new ArgumentNullException(nameof(csv));
            var text = csv.Length > 0 && csv[0] == '\uFEFF' ? csv.Substring(1) : csv;
            var rows = new List<NomenclatureRow>();
            var lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0) continue;
                var cells = line.Split(';');
                if (cells.Length < 4) throw new FormatException("Строка " + (i + 1) + ": нужно 4 колонки через «;».");
                if (!int.TryParse(cells[2].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var group))
                    throw new FormatException("Строка " + (i + 1) + ": нечисловая группа.");
                rows.Add(new NomenclatureRow(cells[0].Trim(), cells[1].Trim(), group, cells[3].Trim()));
            }
            return rows;
        }

        /// <summary>Строит JSON-заготовки каталога (без нормативных полей — их добавляет студент).</summary>
        public static string ToCatalogStubs(IReadOnlyList<NomenclatureRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                sb.AppendLine("  { \"number\": \"" + r.Number + "\", \"name\": \"" + r.Name.Replace("\"", "\\\"")
                    + "\", \"group\": " + r.Group.ToString(CultureInfo.InvariantCulture)
                    + ", \"status\": \"" + r.Status + "\" }" + (i < rows.Count - 1 ? "," : ""));
            }
            sb.AppendLine("]");
            return sb.ToString().Replace("\r\n", "\n");
        }
    }
}
