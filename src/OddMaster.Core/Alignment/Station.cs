using System;
using System.Globalization;

namespace OddMaster.Core.Alignment
{
    /// <summary>Формат записи пикета «ПК12+35,40» (ADR-0013).</summary>
    public sealed class StationFormat
    {
        /// <summary>Формат по умолчанию: «ПК12+35,40» без пробела, запятая в дроби.</summary>
        public static StationFormat Default => new StationFormat("ПК", "", "+", ",", 2);

        /// <summary>Создаёт формат.</summary>
        public StationFormat(string prefix, string spaceAfterPrefix, string plusSign,
            string decimalSeparator, int fractionDigits)
        {
            Prefix = prefix;
            SpaceAfterPrefix = spaceAfterPrefix;
            PlusSign = plusSign;
            DecimalSeparator = decimalSeparator;
            FractionDigits = fractionDigits;
        }

        /// <summary>Префикс «ПК».</summary>
        public string Prefix { get; }
        /// <summary>Пробел после префикса (может быть пустым).</summary>
        public string SpaceAfterPrefix { get; }
        /// <summary>Знак перед метрами.</summary>
        public string PlusSign { get; }
        /// <summary>Разделитель дробной части.</summary>
        public string DecimalSeparator { get; }
        /// <summary>Число знаков после разделителя.</summary>
        public int FractionDigits { get; }
    }

    /// <summary>Пикет в метрах (ПК = 100 м). Округление до сантиметров переносит разряд в следующий ПК.</summary>
    public readonly struct Station : IComparable<Station>, IEquatable<Station>
    {
        /// <summary>Создаёт пикет из метров.</summary>
        public Station(double meters) { Meters = meters; }

        /// <summary>Абсолютное значение пикета, м.</summary>
        public double Meters { get; }

        /// <summary>Форматирует пикет. 1299,996 м → «ПК13+00,00».</summary>
        public string Format(StationFormat? format = null)
        {
            var f = format ?? StationFormat.Default;
            var factor = Pow10(f.FractionDigits);
            var totalCm = Math.Round((decimal)Meters * 100m, 0, MidpointRounding.AwayFromZero);
            // ПК — это пикет по 100 м, а не километр: «ПК12+35,40» = 1235,40 м.
            var pk = (long)decimal.Floor(totalCm / 10000m);
            var restCm = totalCm - pk * 10000m;
            var restMeters = restCm / 100m;
            var whole = (long)decimal.Floor(restCm / 100m);
            var fractionalCm = (long)((restCm - whole * 100m) * factor / 100m);
            var restText = whole.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0')
                + f.DecimalSeparator
                + fractionalCm.ToString(CultureInfo.InvariantCulture).PadLeft(f.FractionDigits, '0');
            return f.Prefix + f.SpaceAfterPrefix + pk.ToString(CultureInfo.InvariantCulture)
                + f.PlusSign + restText;
        }

        private static int Pow10(int n)
        {
            int result = 1;
            for (int i = 0; i < n; i++) result *= 10;
            return result;
        }

        /// <summary>Разбирает строку пикета. Плохой формат — FormatException.</summary>
        public static Station Parse(string text, StationFormat? format = null)
        {
            var f = format ?? StationFormat.Default;
            if (text == null) throw new ArgumentNullException(nameof(text));
            var value = text.Trim();
            if (!value.StartsWith(f.Prefix, StringComparison.Ordinal))
                throw new FormatException($"Пикет «{text}» должен начинаться с «{f.Prefix}».");
            value = value.Substring(f.Prefix.Length).TrimStart();
            var plusIndex = value.IndexOf(f.PlusSign, StringComparison.Ordinal);
            if (plusIndex < 0)
                throw new FormatException($"В пикете «{text}» нет знака «{f.PlusSign}».");
            var kmText = value.Substring(0, plusIndex).Trim();
            var restText = value.Substring(plusIndex + f.PlusSign.Length).Replace(f.DecimalSeparator, ".");
            if (!long.TryParse(kmText, NumberStyles.None, CultureInfo.InvariantCulture, out var pk))
                throw new FormatException($"Не удалось прочитать номер пикета в «{text}».");
            if (!double.TryParse(restText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var rest))
                throw new FormatException($"Не удалось прочитать метры в пикете «{text}».");
            if (rest < 0 || rest >= 100)
                throw new FormatException($"Метры в пикете «{text}» должны быть от 0 до 99,99.");
            return new Station(pk * 100 + rest);
        }

        /// <summary>Сравнение по метрам.</summary>
        public int CompareTo(Station other) => Meters.CompareTo(other.Meters);

        /// <summary>Равенство с допуском 1e-9.</summary>
        public bool Equals(Station other) => Math.Abs(Meters - other.Meters) < 1e-9;

        /// <summary>Равенство.</summary>
        public override bool Equals(object? obj) => obj is Station s && Equals(s);

        /// <summary>Хэш.</summary>
        public override int GetHashCode() => Meters.GetHashCode();

        /// <summary>Инвариантное представление в метрах.</summary>
        public override string ToString() => Meters.ToString("0.###", CultureInfo.InvariantCulture) + " м";
    }
}
