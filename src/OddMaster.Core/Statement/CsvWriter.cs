using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace OddMaster.Core.Statement
{
    /// <summary>CSV для русского Excel: UTF-8 с BOM, разделитель «;», запятая в дроби, CRLF (ADR-0012).</summary>
    public static class CsvWriter
    {
        /// <summary>Собирает CSV-текст. BOM представлен символом U+FEFF в начале строки.</summary>
        public static string Build(IEnumerable<IEnumerable<string>> rows)
        {
            var sb = new StringBuilder();
            sb.Append('\uFEFF');
            foreach (var row in rows)
                sb.Append(string.Join(";", row.Select(Escape))).Append("\r\n");
            return sb.ToString();
        }

        /// <summary>Экранирует значение: кавычки, «;», переводы строк.</summary>
        public static string Escape(string? value)
        {
            var text = value ?? string.Empty;
            if (text.IndexOfAny(new[] { ';', '"', '\r', '\n' }) < 0) return text;
            return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        /// <summary>Число в инвариантной культуре с заменой точки на запятую (только для CSV).</summary>
        public static string Number(double value, int decimals = 2)
        {
            var text = value.ToString("0." + new string('0', decimals), CultureInfo.InvariantCulture);
            return text.Replace('.', ',');
        }

        /// <summary>Проверяет, что строка начинается с BOM (после записи в UTF-8).</summary>
        public static bool HasBom(string csv) => csv.Length > 0 && csv[0] == '\uFEFF';
    }
}
