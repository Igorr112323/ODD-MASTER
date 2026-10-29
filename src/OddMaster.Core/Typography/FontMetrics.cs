using System;
using System.Collections.Generic;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Typography
{
    /// <summary>Метрики шрифта из config/typography/fonts.json. Статус Placeholder (GOST-002).</summary>
    public sealed class FontMetrics
    {
        /// <summary>Создаёт метрики.</summary>
        public FontMetrics(string fontId, string fontFamily, double capHeightRatio,
            double defaultAdvanceRatio, double maxHeightRatio, double paddingRatio,
            double lineGapRatio, Dictionary<string, double> advanceRatios,
            double minCapHeightMm, NormRef normRef, Verification verification, string? note)
        {
            FontId = fontId; FontFamily = fontFamily;
            CapHeightRatio = capHeightRatio; DefaultAdvanceRatio = defaultAdvanceRatio;
            MaxHeightRatio = maxHeightRatio; PaddingRatio = paddingRatio;
            LineGapRatio = lineGapRatio;
            MinCapHeightMm = minCapHeightMm;
            AdvanceRatios = advanceRatios ?? throw new ArgumentNullException(nameof(advanceRatios));
            NormRef = normRef; Verification = verification; Note = note;
        }

        /// <summary>Идентификатор набора метрик.</summary>
        public string FontId { get; }
        /// <summary>Семейство шрифтов с запасом.</summary>
        public string FontFamily { get; }
        /// <summary>Доля высоты прописных от кегля.</summary>
        public double CapHeightRatio { get; }
        /// <summary>Ширина символа по умолчанию в долях кегля.</summary>
        public double DefaultAdvanceRatio { get; }
        /// <summary>Максимальная доля высоты поля под кегль.</summary>
        public double MaxHeightRatio { get; }
        /// <summary>Отступ от поля знака в долях его габарита.</summary>
        public double PaddingRatio { get; }
        /// <summary>Межстрочный интервал в долях кегля.</summary>
        public double LineGapRatio { get; }
        /// <summary>
        /// Порог читаемости: минимальная высота прописных, ниже которой текст не уменьшают,
        /// а переносят. Условное значение (GOST-002), не норма ГОСТ.
        /// </summary>
        public double MinCapHeightMm { get; }
        /// <summary>Ширины символов в долях кегля.</summary>
        public IReadOnlyDictionary<string, double> AdvanceRatios { get; }
        /// <summary>Ссылка на норму.</summary>
        public NormRef NormRef { get; }
        /// <summary>Статус проверки.</summary>
        public Verification Verification { get; }
        /// <summary>Пояснение.</summary>
        public string? Note { get; }

        /// <summary>Суммарная ширина строки в долях кегля.</summary>
        public double TotalAdvance(string text)
        {
            double total = 0;
            foreach (var ch in text ?? string.Empty)
                total += AdvanceRatios.TryGetValue(ch.ToString(), out var ratio) ? ratio : DefaultAdvanceRatio;
            return total > 0 ? total : DefaultAdvanceRatio;
        }
    }
}
