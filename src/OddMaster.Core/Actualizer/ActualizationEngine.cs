using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OddMaster.Core.Catalog;

namespace OddMaster.Core.Actualizer
{
    /// <summary>Движок Актуализатора: распознавание → карта замен → шесть категорий (ADR-0010).</summary>
    public sealed class ActualizationEngine
    {
        private readonly ILegacyRecognizer _recognizer;
        private readonly MigrationMap _map;
        private readonly SignCatalog _catalog;

        /// <summary>Создаёт движок.</summary>
        public ActualizationEngine(ILegacyRecognizer recognizer, MigrationMap map, SignCatalog catalog)
        {
            _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        /// <summary>Обрабатывает старую схему. Конфликт правил → NeedsDecision, ничего не применяется молча.</summary>
        public ActualizationReport Run(LegacyScheme scheme)
        {
            if (scheme == null) throw new ArgumentNullException(nameof(scheme));
            var items = new List<ActualizationItem>();
            foreach (var element in scheme.Elements) items.Add(Classify(element));
            var banner = _map.NeedsWarningBanner()
                ? "Карта замен не сверена студентом по таблице различий — ЧЕРНОВИК/ДЕМО. Не применять без проверки."
                : "Карта замен проверена.";
            if (_map.IsDemo) banner = "ДЕМО: карта синтетическая, номера условные. " + banner;
            return new ActualizationReport(_map.Title, _map.IsDemo, banner, items);
        }

        private ActualizationItem Classify(LegacyElement element)
        {
            var recognition = _recognizer.Recognize(element);
            if (recognition.Number == null)
                return new ActualizationItem(element.Id, element.BlockName, null,
                    recognition.Confidence, recognition.PatternId, MigrationCategory.Unknown,
                    null, null, "Не распознано ни одним шаблоном.");
            var number = recognition.Number;
            var matches = _map.Match(number, recognition.Plates);
            if (matches.Count > 1)
                return new ActualizationItem(element.Id, element.BlockName, number.ToString(),
                    recognition.Confidence, recognition.PatternId, MigrationCategory.NeedsDecision,
                    null, string.Join(",", matches.Select(m => m.Id)),
                    "Конфликт правил карты замен: " + string.Join(", ", matches.Select(m => m.Id))
                    + ". Нужно решение человека.");
            if (matches.Count == 1)
            {
                var rule = matches[0];
                return new ActualizationItem(element.Id, element.BlockName, number.ToString(),
                    recognition.Confidence, recognition.PatternId, rule.Category,
                    rule.ResultSigns?.ToString(), rule.Id,
                    (rule.Note ?? "") + (rule.Comment != null ? " " + rule.Comment : ""));
            }
            if (_catalog.Find(number) != null)
                return new ActualizationItem(element.Id, element.BlockName, number.ToString(),
                    recognition.Confidence, recognition.PatternId, MigrationCategory.Unchanged,
                    number.ToString(), null,
                    "Номер уже есть в каталоге 2024; замена не требуется.");
            return new ActualizationItem(element.Id, element.BlockName, number.ToString(),
                recognition.Confidence, recognition.PatternId, MigrationCategory.Unknown,
                null, null,
                "Номер не найден ни в карте замен, ни в каталоге 2024.");
        }
    }
}
