using System;
using System.Collections.Generic;
using System.Linq;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Config
{
    /// <summary>Диапазон значений в километрах из табл. Д.1.</summary>
    public sealed class KmRange
    {
        /// <summary>Создаёт диапазон.</summary>
        public KmRange(double? fromKm, double? toKm, bool toInclusive, double? aboveKm, double multipleOfKm)
        {
            FromKm = fromKm; ToKm = toKm; ToInclusive = toInclusive; AboveKm = aboveKm; MultipleOfKm = multipleOfKm;
        }

        /// <summary>Нижняя граница, км.</summary>
        public double? FromKm { get; }
        /// <summary>Верхняя граница, км.</summary>
        public double? ToKm { get; }
        /// <summary>Включать верхнюю границу.</summary>
        public bool ToInclusive { get; }
        /// <summary>Порог «свыше», км.</summary>
        public double? AboveKm { get; }
        /// <summary>Кратность внутри диапазона, км.</summary>
        public double MultipleOfKm { get; }

        /// <summary>Проверяет попадание значения (в км) в диапазон.</summary>
        public bool Covers(double km)
        {
            if (AboveKm.HasValue) return km > AboveKm.Value;
            var lower = !FromKm.HasValue || km >= FromKm.Value;
            var upper = !ToKm.HasValue || (ToInclusive ? km <= ToKm.Value : km < ToKm.Value);
            return lower && upper;
        }
    }

    /// <summary>Одно правило табл. Д.1.</summary>
    public sealed class ParameterRule
    {
        /// <summary>Создаёт правило.</summary>
        public ParameterRule(SignNumberSet signs, string parameter, string? unit, decimal? multipleOf,
            List<decimal> oneOf, decimal? above20MultipleOf, List<KmRange> kmRanges,
            int? stepMinutes, List<int> oneOfMinutes, decimal? fromHours, decimal? multipleOfHours)
        {
            Signs = signs; Parameter = parameter; Unit = unit; MultipleOf = multipleOf;
            OneOf = oneOf; Above20MultipleOf = above20MultipleOf; KmRanges = kmRanges;
            StepMinutes = stepMinutes; OneOfMinutes = oneOfMinutes; FromHours = fromHours; MultipleOfHours = multipleOfHours;
        }

        /// <summary>Номера знаков.</summary>
        public SignNumberSet Signs { get; }
        /// <summary>Имя параметра: speed, distance, mass, …</summary>
        public string Parameter { get; }
        /// <summary>Единица измерения.</summary>
        public string? Unit { get; }
        /// <summary>Кратность значения.</summary>
        public decimal? MultipleOf { get; }
        /// <summary>Разрешённый набор значений.</summary>
        public IReadOnlyList<decimal> OneOf { get; }
        /// <summary>Кратность для значений свыше 20 (масса).</summary>
        public decimal? Above20MultipleOf { get; }
        /// <summary>Диапазоны в километрах.</summary>
        public IReadOnlyList<KmRange> KmRanges { get; }
        /// <summary>Шаг времени, мин.</summary>
        public int? StepMinutes { get; }
        /// <summary>Разрешённые минуты.</summary>
        public IReadOnlyList<int> OneOfMinutes { get; }
        /// <summary>Порог часов.</summary>
        public decimal? FromHours { get; }
        /// <summary>Кратность часов.</summary>
        public decimal? MultipleOfHours { get; }
    }

    /// <summary>Результат проверки значения параметра.</summary>
    public sealed class ParameterCheck
    {
        /// <summary>Создаёт результат.</summary>
        public ParameterCheck(bool hasRule, bool isValid, string reason, ParameterRule? rule)
        {
            HasRule = hasRule; IsValid = isValid; Reason = reason; Rule = rule;
        }

        /// <summary>Есть ли правило для этого знака.</summary>
        public bool HasRule { get; }
        /// <summary>Значение допустимо.</summary>
        public bool IsValid { get; }
        /// <summary>Пояснение на русском.</summary>
        public string Reason { get; }
        /// <summary>Найденное правило.</summary>
        public ParameterRule? Rule { get; }
    }

    /// <summary>Параметры на знаках (п. 3.5, табл. Д.1). Кратность считается в decimal.</summary>
    public sealed class ParameterNorms
    {
        private readonly List<ParameterRule> _rules;

        /// <summary>Создаёт набор правил.</summary>
        public ParameterNorms(NormRef normRef, Verification verification, List<ParameterRule> rules,
            List<string> notes, List<string> markers)
        {
            NormRef = normRef; Verification = verification; _rules = rules;
            Notes = notes; Markers = markers;
        }

        /// <summary>Ссылка на норму.</summary>
        public NormRef NormRef { get; }
        /// <summary>Статус проверки.</summary>
        public Verification Verification { get; }
        /// <summary>Все правила.</summary>
        public IReadOnlyList<ParameterRule> Rules => _rules;
        /// <summary>Примечания куратора.</summary>
        public IReadOnlyList<string> Notes { get; }
        /// <summary>Маркеры GOST-NNN.</summary>
        public IReadOnlyList<string> Markers { get; }

        /// <summary>Правила, действующие для номера знака.</summary>
        public List<ParameterRule> Match(SignNumber number) =>
            _rules.Where(r => r.Signs.Contains(number)).ToList();

        /// <summary>Проверяет значение по правилу. Отсутствие правила — не ошибка, а пробел данных (R007).</summary>
        public ParameterCheck Validate(SignNumber number, decimal value)
        {
            var rules = Match(number);
            if (rules.Count == 0)
                return new ParameterCheck(false, true, "Правило табл. Д.1 для знака " + number + " не найдено (пробел данных).", null);

            foreach (var rule in rules)
            {
                if (!CheckOne(rule, value, out var reason))
                    return new ParameterCheck(true, false, reason, rule);
            }
            return new ParameterCheck(true, true, "Значение допустимо.", rules[0]);
        }

        private static bool CheckOne(ParameterRule rule, decimal value, out string reason)
        {
            reason = string.Empty;
            if (rule.MultipleOf.HasValue && rule.MultipleOf.Value > 0 && value % rule.MultipleOf.Value != 0)
            {
                reason = $"Значение {value} не кратно {rule.MultipleOf.Value} ({rule.Parameter}).";
                return false;
            }
            if (rule.OneOf.Count > 0 && rule.OneOf.Contains(value)) return true;
            if (rule.Above20MultipleOf.HasValue && value > 20 && value % rule.Above20MultipleOf.Value == 0) return true;
            if (rule.KmRanges.Count > 0 && CheckKmRanges(rule, value, out reason)) return true;
            if (rule.KmRanges.Count > 0 && reason.Length > 0) return false;
            if (rule.OneOf.Count > 0)
            {
                reason = $"Значение {value} отсутствует в разрешённом наборе ({rule.Parameter}).";
                return false;
            }
            return true;
        }

        private static bool CheckKmRanges(ParameterRule rule, decimal value, out string reason)
        {
            reason = string.Empty;
            var km = (double)value / (rule.Unit == "km" ? 1.0 : 1000.0);
            var range = rule.KmRanges.FirstOrDefault(r => r.Covers(km));
            if (range == null)
            {
                reason = $"Значение {value} вне диапазонов табл. Д.1 ({rule.Parameter}).";
                return false;
            }
            var step = (decimal)range.MultipleOfKm * (rule.Unit == "km" ? 1m : 1000m);
            if (step > 0 && value % step != 0)
            {
                reason = $"Значение {value} не кратно {step} в своём диапазоне ({rule.Parameter}).";
                return false;
            }
            return true;
        }
    }
}
