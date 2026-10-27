using System.Collections.Generic;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Config
{
    /// <summary>Правило «список номеров → числовое значение» (кайма, радиус).</summary>
    public sealed class ValueRule
    {
        /// <summary>Создаёт правило.</summary>
        public ValueRule(SignNumberSet signs, double value)
        {
            Signs = signs;
            Value = value;
        }

        /// <summary>Номера знаков.</summary>
        public SignNumberSet Signs { get; }

        /// <summary>Значение, мм.</summary>
        public double Value { get; }
    }

    /// <summary>Каймы и допуски по п. 3.14–3.16 ГОСТ Р 52290-2024.</summary>
    public sealed class BordersNorms
    {
        /// <summary>Создаёт набор правил кайм.</summary>
        public BordersNorms(NormRef normRef, Verification verification, List<ValueRule> outer,
            List<ValueRule> plates, List<ValueRule> redRadius, SignNumberSet mayBeOmitted,
            double? innerBorderWidthMm, string innerBorderWidthNote, Dictionary<string, double> tolerances,
            List<string> markers)
        {
            NormRef = normRef;
            Verification = verification;
            Outer = outer;
            Plates = plates;
            RedRadius = redRadius;
            MayBeOmitted = mayBeOmitted;
            InnerBorderWidthMm = innerBorderWidthMm;
            InnerBorderWidthNote = innerBorderWidthNote;
            Tolerances = tolerances;
            Markers = markers;
        }

        /// <summary>Ссылка на норму.</summary>
        public NormRef NormRef { get; }

        /// <summary>Статус проверки.</summary>
        public Verification Verification { get; }

        /// <summary>Ширина наружной каймы, мм.</summary>
        public IReadOnlyList<ValueRule> Outer { get; }

        /// <summary>Ширина каймы табличек, мм.</summary>
        public IReadOnlyList<ValueRule> Plates { get; }

        /// <summary>Внутренний радиус красной каймы, мм.</summary>
        public IReadOnlyList<ValueRule> RedRadius { get; }

        /// <summary>Знаки, у которых наружную кайму можно не выполнять.</summary>
        public SignNumberSet MayBeOmitted { get; }

        /// <summary>Ширина внутренней каймы: null до сверки с рисунками прил. Д (GOST-001).</summary>
        public double? InnerBorderWidthMm { get; }

        /// <summary>Пояснение к пропущенному значению.</summary>
        public string InnerBorderWidthNote { get; }

        /// <summary>Допуски из п. 3.15–3.16.</summary>
        public IReadOnlyDictionary<string, double> Tolerances { get; }

        /// <summary>Маркеры GOST-NNN.</summary>
        public IReadOnlyList<string> Markers { get; }

        /// <summary>Ищет все подходящие правила каймы (таблички проверяются отдельно).</summary>
        public List<ValueRule> MatchOuter(SignNumber number)
        {
            var result = new List<ValueRule>();
            foreach (var rule in Outer)
                if (rule.Signs.Contains(number)) result.Add(rule);
            return result;
        }

        /// <summary>Ищет все подходящие правила каймы табличек.</summary>
        public List<ValueRule> MatchPlate(SignNumber number)
        {
            var result = new List<ValueRule>();
            foreach (var rule in Plates)
                if (rule.Signs.Contains(number)) result.Add(rule);
            return result;
        }
    }
}
