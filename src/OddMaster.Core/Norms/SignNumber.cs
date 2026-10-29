using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace OddMaster.Core.Norms
{
    /// <summary>
    /// Номер знака «группа.знак[.разновидность[.разновидность]]».
    /// Сравнение покомпонентное: 3.2 &lt; 3.10 &lt; 3.24, 2.3.7 &lt; 2.4 (ADR-0005).
    /// </summary>
    public sealed class SignNumber : IComparable<SignNumber>, IEquatable<SignNumber>
    {
        private static readonly Regex Pattern = new Regex(@"^[0-9]+(\.[0-9]+){1,3}$", RegexOptions.Compiled);
        private readonly int[] _parts;

        private SignNumber(int[] parts) { _parts = parts; }

        /// <summary>Компоненты номера (минимум два: группа и номер знака).</summary>
        public int[] Parts => (int[])_parts.Clone();

        /// <summary>Группа знаков 1–8.</summary>
        public int Group => _parts[0];

        /// <summary>Истина, если у номера есть разновидности (больше двух компонент).</summary>
        public bool HasVariants => _parts.Length > 2;

        /// <summary>Разбирает номер. Пустая строка, «3..24», «3.24.», «a.1» — ошибка.</summary>
        public static SignNumber Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var value = text.Trim();
            if (!Pattern.IsMatch(value))
                throw new FormatException($"Некорректный номер знака «{text}». Ожидается формат «группа.знак[.разновидность]», например 3.24 или 8.21.3.");
            var parts = value.Split('.').Select(p => int.Parse(p, NumberStyles.None, CultureInfo.InvariantCulture)).ToArray();
            if (parts[0] < 1 || parts[0] > 8)
                throw new FormatException($"Группа знака должна быть от 1 до 8, получено «{text}».");
            return new SignNumber(parts);
        }

        /// <summary>Пытается разобрать номер без исключения.</summary>
        public static bool TryParse(string? text, out SignNumber number)
        {
            number = null!;
            if (text == null) return false;
            try { number = Parse(text); return true; }
            catch (FormatException) { return false; }
            catch (OverflowException) { return false; }
        }

        /// <summary>Покомпонентное сравнение. Более короткий номер меньше своего продолжения.</summary>
        public int CompareTo(SignNumber? other)
        {
            if (other == null) return 1;
            var n = Math.Min(_parts.Length, other._parts.Length);
            for (int i = 0; i < n; i++)
            {
                var cmp = _parts[i].CompareTo(other._parts[i]);
                if (cmp != 0) return cmp;
            }
            return _parts.Length.CompareTo(other._parts.Length);
        }

        /// <summary>Равенство по компонентам.</summary>
        public bool Equals(SignNumber? other) => other != null && CompareTo(other) == 0;

        /// <summary>Равенство по компонентам.</summary>
        public override bool Equals(object? obj) => Equals(obj as SignNumber);

        /// <summary>Хэш по компонентам.</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (var p in _parts) hash = hash * 31 + p;
                return hash;
            }
        }

        /// <summary>Номер в канонической записи через точку.</summary>
        public override string ToString() => string.Join(".", _parts.Select(p => p.ToString(CultureInfo.InvariantCulture)));

        /// <summary>Истина, если этот номер — строгое начало (префикс) другого.</summary>
        public bool IsStrictPrefixOf(SignNumber other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            if (_parts.Length >= other._parts.Length) return false;
            for (int i = 0; i < _parts.Length; i++)
                if (_parts[i] != other._parts[i]) return false;
            return true;
        }
    }
}
