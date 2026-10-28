using System;
using System.Collections.Generic;
using OddMaster.Core.Geometry;

namespace OddMaster.Core.Typography
{
    /// <summary>Раскладка надписи: кегль, ширина, базовая линия — без text-anchor и dominant-baseline.</summary>
    public sealed class TextLayout
    {
        /// <summary>Создаёт раскладку.</summary>
        public TextLayout(string value, double heightMm, double widthMm, double capHeightMm,
            Point2 baselineStart, string fontFamily)
        {
            Value = value; HeightMm = heightMm; WidthMm = widthMm; CapHeightMm = capHeightMm;
            BaselineStart = baselineStart; FontFamily = fontFamily;
        }

        /// <summary>Текст.</summary>
        public string Value { get; }
        /// <summary>Кегль, мм.</summary>
        public double HeightMm { get; }
        /// <summary>Ширина строки по модели метрик, мм.</summary>
        public double WidthMm { get; }
        /// <summary>Высота прописных, мм.</summary>
        public double CapHeightMm { get; }
        /// <summary>Левая точка базовой линии (начало текста в САПР).</summary>
        public Point2 BaselineStart { get; }
        /// <summary>Семейство шрифтов.</summary>
        public string FontFamily { get; }

        /// <summary>Габарит надписи в системе знака (Y вверх).</summary>
        public BoundingBox Box()
        {
            var descent = HeightMm - CapHeightMm;
            return new BoundingBox(BaselineStart.X, BaselineStart.Y - descent,
                BaselineStart.X + WidthMm, BaselineStart.Y + CapHeightMm);
        }
    }

    /// <summary>Текстовый движок: вписывает строку в поле, вычисляет якорь сам (CR-07).</summary>
    public static class TextEngine
    {
        /// <summary>Вписывает одну строку в прямоугольник field и центрирует её.</summary>
        public static TextLayout Layout(string text, double boxWidthMm, double boxHeightMm,
            FontMetrics metrics, Point2 center)
        {
            if (metrics == null) throw new ArgumentNullException(nameof(metrics));
            var advance = metrics.TotalAdvance(text);
            var byHeight = boxHeightMm * metrics.MaxHeightRatio;
            var byWidth = boxWidthMm / advance;
            var height = Math.Min(byHeight, byWidth);
            return WithHeight(text, height, metrics, center);
        }

        /// <summary>Строит несколько строк одного кегля, чтобы они поместились в поле.</summary>
        public static IReadOnlyList<TextLayout> LayoutBlock(IReadOnlyList<string> lines,
            double boxWidthMm, double boxHeightMm, FontMetrics metrics, Point2 center)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            if (metrics == null) throw new ArgumentNullException(nameof(metrics));
            if (lines.Count == 0) return Array.Empty<TextLayout>();
            double height = double.MaxValue;
            foreach (var line in lines)
                height = Math.Min(height, Math.Min(boxHeightMm * metrics.MaxHeightRatio,
                    boxWidthMm / metrics.TotalAdvance(line)));
            var step = height * (1.0 + metrics.LineGapRatio);
            var blockHeight = step * lines.Count;
            var top = center.Y + blockHeight / 2;
            var result = new List<TextLayout>(lines.Count);
            for (int i = 0; i < lines.Count; i++)
            {
                var lineCenter = new Point2(center.X, top - step * (i + 0.5));
                result.Add(WithHeight(lines[i], height, metrics, lineCenter));
            }
            return result;
        }

        /// <summary>Оценка ширины строки при заданном кегле (для инвариантов).</summary>
        public static double EstimateWidth(string text, double heightMm, FontMetrics metrics) =>
            heightMm * metrics.TotalAdvance(text);

        private static TextLayout WithHeight(string text, double height, FontMetrics metrics, Point2 center)
        {
            var cap = height * metrics.CapHeightRatio;
            var width = height * metrics.TotalAdvance(text);
            // Базовая линия: em-бокс центрирован по center.Y, строка центрирована по center.X.
            var baseline = new Point2(center.X - width / 2, center.Y + height / 2 - cap);
            return new TextLayout(text, height, width, cap, baseline, metrics.FontFamily);
        }
    }
}
