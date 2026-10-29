using System;
using System.Text.RegularExpressions;

namespace OddMaster.Core.Norms
{
    /// <summary>
    /// Диапазон номеров «A - B»: все N, для которых A ≤ N ≤ B.
    /// Верхняя граница без подномеров НЕ включает свои разновидности:
    /// «6.14.2 - 6.16» не содержит 6.16.1 (ADR-0005).
    /// </summary>
    public sealed class SignNumberRange
    {
        private static readonly Regex Separator = new Regex(@"\s*[-–—]\s*", RegexOptions.Compiled);

        /// <summary>Создаёт диапазон из двух номеров.</summary>
        public SignNumberRange(SignNumber from, SignNumber to)
        {
            From = from ?? throw new ArgumentNullException(nameof(from));
            To = to ?? throw new ArgumentNullException(nameof(to));
            if (from.CompareTo(to) > 0)
                throw new FormatException($"Левая граница диапазона «{from}» больше правой «{to}».");
        }

        /// <summary>Нижняя граница.</summary>
        public SignNumber From { get; }

        /// <summary>Верхняя граница.</summary>
        public SignNumber To { get; }

        /// <summary>Пробует разобрать диапазон «A - B» (дефис, en dash или em dash, пробелы необязательны).</summary>
        public static bool TryParse(string text, out SignNumberRange range)
        {
            range = null!;
            var matches = Separator.Matches(text);
            if (matches.Count != 1) return false;
            var left = text.Substring(0, matches[0].Index).Trim();
            var right = text.Substring(matches[0].Index + matches[0].Length).Trim();
            if (!SignNumber.TryParse(left, out var from) || !SignNumber.TryParse(right, out var to)) return false;
            if (from.CompareTo(to) > 0) return false;
            range = new SignNumberRange(from, to);
            return true;
        }

        /// <summary>Проверяет вхождение номера в диапазон.</summary>
        public bool Contains(SignNumber number)
        {
            if (number == null) throw new ArgumentNullException(nameof(number));
            if (number.CompareTo(From) < 0 || number.CompareTo(To) > 0) return false;
            // Разновидности верхней границы лежат вне диапазона.
            if (To.IsStrictPrefixOf(number)) return false;
            return true;
        }

        /// <summary>Текстовое представление диапазона.</summary>
        public override string ToString() => From + " - " + To;
    }
}
