using System;
using System.Collections.Generic;
using System.Linq;

namespace OddMaster.Core.Norms
{
    /// <summary>
    /// Список одиночных номеров и диапазонов, например «1.5 - 1.7, 2.4».
    /// Одиночный номер совпадает только сам с собой: «6.2» ≠ 6.2.1 (ADR-0005).
    /// </summary>
    public sealed class SignNumberSet
    {
        private readonly List<SignNumber> _singles = new List<SignNumber>();
        private readonly List<SignNumberRange> _ranges = new List<SignNumberRange>();

        private SignNumberSet(string source) { Source = source; }

        /// <summary>Исходная строка (для сообщений об ошибках).</summary>
        public string Source { get; }

        /// <summary>Одиночные номера в порядке появления.</summary>
        public IReadOnlyList<SignNumber> Singles => _singles;

        /// <summary>Диапазоны в порядке появления.</summary>
        public IReadOnlyList<SignNumberRange> Ranges => _ranges;

        /// <summary>Разбирает список. Разделитель элементов — запятая; диапазон — дефис/тире.</summary>
        public static SignNumberSet Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var result = new SignNumberSet(text.Trim());
            var pieces = text.Split(',');
            foreach (var raw in pieces)
            {
                var piece = raw.Trim();
                if (piece.Length == 0)
                    throw new FormatException($"Пустой элемент в списке номеров «{text}».");
                if (SignNumberRange.TryParse(piece, out var range))
                {
                    result._ranges.Add(range);
                    continue;
                }
                if (SignNumber.TryParse(piece, out var single))
                {
                    result._singles.Add(single);
                    continue;
                }
                throw new FormatException(
                    $"Не удалось разобрать элемент «{piece}» в списке «{text}». " +
                    "Ожидается номер «3.24» или диапазон «1.5 - 1.7».");
            }
            return result;
        }

        /// <summary>Пытается разобрать список без исключения.</summary>
        public static bool TryParse(string? text, out SignNumberSet set)
        {
            set = null!;
            if (text == null) return false;
            try { set = Parse(text); return true; }
            catch (FormatException) { return false; }
        }

        /// <summary>Проверяет вхождение номера в список.</summary>
        public bool Contains(SignNumber number)
        {
            if (number == null) throw new ArgumentNullException(nameof(number));
            return _singles.Any(s => s.Equals(number)) || _ranges.Any(r => r.Contains(number));
        }

        /// <summary>Исходная строка.</summary>
        public override string ToString() => Source;
    }
}
