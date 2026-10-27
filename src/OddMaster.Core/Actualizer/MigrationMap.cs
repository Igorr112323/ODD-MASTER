using System.Collections.Generic;
using System.Linq;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Actualizer
{
    /// <summary>Категории отчёта Актуализатора (глоссарий 7.3).</summary>
    public enum MigrationCategory
    {
        /// <summary>Заменено на новый номер.</summary>
        Replaced,
        /// <summary>Знак отменён.</summary>
        Removed,
        /// <summary>Нужно решение человека: конфликт правил или неоднозначность.</summary>
        NeedsDecision,
        /// <summary>Допустимо оставить, но можно упростить (поправка куратора 3.5).</summary>
        AllowedSimplify,
        /// <summary>Номер уже есть в каталоге 2024, замена не нужна.</summary>
        Unchanged,
        /// <summary>Номер не найден ни в каталоге, ни в карте замен.</summary>
        Unknown
    }

    /// <summary>Правило карты замен: условие → результат → категория.</summary>
    public sealed class MigrationRule
    {
        /// <summary>Создаёт правило.</summary>
        public MigrationRule(string id, SignNumberSet conditionSigns, List<string> conditionPlates,
            SignNumberSet? resultSigns, List<string> resultPlates, string? note,
            MigrationCategory category, NormRef normRef, Verification verification, string? comment)
        {
            Id = id; ConditionSigns = conditionSigns; ConditionPlates = conditionPlates;
            ResultSigns = resultSigns; ResultPlates = resultPlates; Note = note;
            Category = category; NormRef = normRef; Verification = verification; Comment = comment;
        }

        /// <summary>Идентификатор правила.</summary>
        public string Id { get; }
        /// <summary>Номера знаков, на которые правило срабатывает.</summary>
        public SignNumberSet ConditionSigns { get; }
        /// <summary>Сопутствующие таблички в условии.</summary>
        public IReadOnlyList<string> ConditionPlates { get; }
        /// <summary>Новые номера (null для Removed).</summary>
        public SignNumberSet? ResultSigns { get; }
        /// <summary>Новые таблички.</summary>
        public IReadOnlyList<string> ResultPlates { get; }
        /// <summary>Пояснение к результату.</summary>
        public string? Note { get; }
        /// <summary>Категория.</summary>
        public MigrationCategory Category { get; }
        /// <summary>Ссылка на норму.</summary>
        public NormRef NormRef { get; }
        /// <summary>Статус проверки правила.</summary>
        public Verification Verification { get; }
        /// <summary>Комментарий автора карты.</summary>
        public string? Comment { get; }

        /// <summary>Проверяет условие правила.</summary>
        public bool Matches(SignNumber number, IEnumerable<string> plates)
        {
            if (!ConditionSigns.Contains(number)) return false;
            var plateSet = new HashSet<string>(plates, System.StringComparer.Ordinal);
            return ConditionPlates.All(plateSet.Contains);
        }
    }

    /// <summary>Карта замен 2004 → 2024. Боевая карта пустая; в тестах и демо — карта с пометкой ДЕМО.</summary>
    public sealed class MigrationMap
    {
        /// <summary>Создаёт карту.</summary>
        public MigrationMap(string map, string title, NormRef normRef, Verification verification,
            string banner, string note, bool isDemo, List<MigrationRule> rules, List<string> markers)
        {
            Map = map; Title = title; NormRef = normRef; Verification = verification;
            Banner = banner; Note = note; IsDemo = isDemo; Rules = rules; Markers = markers;
        }

        /// <summary>Идентификатор карты.</summary>
        public string Map { get; }
        /// <summary>Название.</summary>
        public string Title { get; }
        /// <summary>Ссылка на норму.</summary>
        public NormRef NormRef { get; }
        /// <summary>Статус проверки.</summary>
        public Verification Verification { get; }
        /// <summary>Баннер предупреждения.</summary>
        public string Banner { get; }
        /// <summary>Пояснение.</summary>
        public string Note { get; }
        /// <summary>Истина для синтетической ДЕМО-карты.</summary>
        public bool IsDemo { get; }
        /// <summary>Правила.</summary>
        public IReadOnlyList<MigrationRule> Rules { get; }
        /// <summary>Маркеры GOST-NNN.</summary>
        public IReadOnlyList<string> Markers { get; }

        /// <summary>Все правила, сработавшие на элемент. Два и более — конфликт.</summary>
        public List<MigrationRule> Match(SignNumber number, IEnumerable<string> plates) =>
            Rules.Where(r => r.Matches(number, plates)).ToList();

        /// <summary>Истина, если хотя бы одно правило не сверено студентом.</summary>
        public bool NeedsWarningBanner() => Rules.Any(r => r.Verification != Verification.StudentVerified);
    }
}
