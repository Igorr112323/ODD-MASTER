using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Actualizer
{
    /// <summary>Элемент чужого DWG: имя блока, атрибуты, тексты, координаты.</summary>
    public sealed class LegacyElement
    {
        /// <summary>Создаёт элемент старой схемы.</summary>
        public LegacyElement(string id, string blockName, Dictionary<string, string> attributes,
            List<string> texts, double x, double y)
        {
            Id = id; BlockName = blockName; Attributes = attributes; Texts = texts; X = x; Y = y;
        }

        /// <summary>Идентификатор элемента (детерминированный, из фикстуры).</summary>
        public string Id { get; }
        /// <summary>Имя блока.</summary>
        public string BlockName { get; }
        /// <summary>Атрибуты блока.</summary>
        public IReadOnlyDictionary<string, string> Attributes { get; }
        /// <summary>Тексты рядом с блоком.</summary>
        public IReadOnlyList<string> Texts { get; }
        /// <summary>Координата X, м.</summary>
        public double X { get; }
        /// <summary>Координата Y, м.</summary>
        public double Y { get; }
    }

    /// <summary>Старая схема как набор элементов.</summary>
    public sealed class LegacyScheme
    {
        /// <summary>Создаёт схему.</summary>
        public LegacyScheme(List<LegacyElement> elements) { Elements = elements ?? throw new ArgumentNullException(nameof(elements)); }

        /// <summary>Элементы в порядке фикстуры.</summary>
        public IReadOnlyList<LegacyElement> Elements { get; }
    }

    /// <summary>Результат распознавания элемента.</summary>
    public sealed class LegacyRecognition
    {
        /// <summary>Создаёт результат.</summary>
        public LegacyRecognition(SignNumber? number, List<string> plates, double confidence, string patternId)
        {
            Number = number; Plates = plates; Confidence = confidence; PatternId = patternId;
        }

        /// <summary>Распознанный номер (null — не распознано).</summary>
        public SignNumber? Number { get; }
        /// <summary>Сопутствующие таблички.</summary>
        public IReadOnlyList<string> Plates { get; }
        /// <summary>Уверенность 0..1.</summary>
        public double Confidence { get; }
        /// <summary>Идентификатор шаблона.</summary>
        public string PatternId { get; }
    }

    /// <summary>Порт распознавателя старых обозначений.</summary>
    public interface ILegacyRecognizer
    {
        /// <summary>Распознаёт номер знака и таблички по имени блока и текстам.</summary>
        LegacyRecognition Recognize(LegacyElement element);
    }

    /// <summary>Шаблон: регулярное выражение → номер знака + уверенность.</summary>
    public sealed class RecognitionPattern
    {
        /// <summary>Создаёт шаблон.</summary>
        public RecognitionPattern(string id, string field, Regex regex, string? number,
            List<string> plates, double confidence)
        {
            Id = id; Field = field; Regex = regex; Number = number; Plates = plates; Confidence = confidence;
        }

        /// <summary>Идентификатор шаблона.</summary>
        public string Id { get; }
        /// <summary>Поле элемента: blockName, attribute:NAME или text.</summary>
        public string Field { get; }
        /// <summary>Регулярное выражение.</summary>
        public Regex Regex { get; }
        /// <summary>Номер знака (null — только таблички).</summary>
        public string? Number { get; }
        /// <summary>Таблички.</summary>
        public IReadOnlyList<string> Plates { get; }
        /// <summary>Уверенность 0..1.</summary>
        public double Confidence { get; }
    }

    /// <summary>Распознаватель по JSON-фикстуре с настраиваемыми шаблонами имён и текстов.</summary>
    public sealed class PatternRecognizer : ILegacyRecognizer
    {
        private readonly List<RecognitionPattern> _patterns;

        /// <summary>Создаёт распознаватель.</summary>
        public PatternRecognizer(List<RecognitionPattern> patterns)
        {
            _patterns = patterns ?? throw new ArgumentNullException(nameof(patterns));
        }

        /// <summary>Распознаёт элемент: первый подходящий шаблон выигрывает.</summary>
        public LegacyRecognition Recognize(LegacyElement element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            foreach (var pattern in _patterns)
            {
                var values = Values(element, pattern.Field);
                foreach (var value in values)
                {
                    if (!pattern.Regex.IsMatch(value)) continue;
                    var number = pattern.Number == null ? null : SignNumber.Parse(pattern.Number);
                    return new LegacyRecognition(number, new List<string>(pattern.Plates), pattern.Confidence, pattern.Id);
                }
            }
            return new LegacyRecognition(null, new List<string>(), 0.0, "NONE");
        }

        private static IEnumerable<string> Values(LegacyElement element, string field)
        {
            if (field == "blockName") { yield return element.BlockName; yield break; }
            if (field == "text") { foreach (var t in element.Texts) yield return t; yield break; }
            if (field.StartsWith("attribute:", StringComparison.Ordinal))
            {
                var name = field.Substring("attribute:".Length);
                if (element.Attributes.TryGetValue(name, out var value)) yield return value;
                yield break;
            }
            yield break;
        }
    }
}
