using System;
using System.Collections.Generic;
using System.Text;
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
            // CR-17: блок целиком обязан влезть в boxHeight, иначе вторая и третья строки
            // выходили за габарит (водяной знак и шапка таблицы этим и страдали).
            var stepRatio = 1.0 + metrics.LineGapRatio;
            double height = boxHeightMm * metrics.MaxHeightRatio;
            height = Math.Min(height, boxHeightMm / (lines.Count * stepRatio));
            foreach (var line in lines)
                height = Math.Min(height, boxWidthMm / metrics.TotalAdvance(line));
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

        /// <summary>
        /// Перенос по словам и вписывание блока в прямоугольник: кегль один для всех строк,
        /// ширина каждой строки ≤ boxWidthMm. Нужен там, где узкая колонка таблицы должна
        /// остаться читаемой: лучше две строки, чем кегль 1,7 мм (форма ведомости, GOST-011).
        /// Алгоритм детерминированный: greedy-перенос и монотонное уменьшение кегля (до восьми шагов),
        /// поэтому результат не зависит от порядка обхода и повторяется байт в байт.
        /// </summary>
        public static IReadOnlyList<TextLayout> LayoutWrapped(string text, double boxWidthMm,
            double boxHeightMm, FontMetrics metrics, Point2 center)
        {
            if (metrics == null) throw new ArgumentNullException(nameof(metrics));
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<TextLayout>();
            var words = text!.Split(new[] { ' ', '\u00a0' }, StringSplitOptions.RemoveEmptyEntries);
            var budget = boxHeightMm * metrics.MaxHeightRatio;
            var step = 1.0 + metrics.LineGapRatio;
            var legible = metrics.CapHeightRatio > 0 ? metrics.MinCapHeightMm / metrics.CapHeightRatio : 0;
            // Стартовый кегль: не больше бюджета высоты и не больше того, при котором самое
            // длинное слово ещё влезает в ширину без разрыва (но не ниже порога читаемости).
            double widest = 0;
            foreach (var word in words) widest = Math.Max(widest, metrics.TotalAdvance(word));
            var height = budget;
            if (widest > 0) height = Math.Min(height, Math.Max(boxWidthMm / widest, legible));
            // Кегль только уменьшаем: итерация монотонна и заведомо сходится.
            // Блок из n строк влезает и в высоту прямоугольника, и в бюджет кегля,
            // если кегль ≤ min(бюджет, boxHeight / (n * межстрочный)).
            for (var iteration = 0; iteration < 8; iteration++)
            {
                var linesAtHeight = Wrap(words, boxWidthMm, height, metrics);
                var allowed = Math.Min(budget, boxHeightMm / (linesAtHeight.Count * step));
                if (height <= allowed + 1e-9) break;
                height = Math.Max(Math.Min(allowed, height * 0.9), legible);
            }
            var lines = Wrap(words, boxWidthMm, height, metrics);
            return LayoutBlock(lines, boxWidthMm, boxHeightMm, metrics, center);
        }

        private static List<string> Wrap(IReadOnlyList<string> words, double boxWidthMm, double height,
            FontMetrics metrics)
        {
            var lines = new List<string>();
            var current = new StringBuilder();
            foreach (var word in words)
            {
                foreach (var part in SplitByWidth(word, boxWidthMm, height, metrics))
                {
                    var candidate = current.Length == 0 ? part : current + " " + part;
                    if (current.Length > 0 && !Fits(candidate, height, boxWidthMm, metrics))
                    {
                        lines.Add(current.ToString());
                        current.Clear();
                        current.Append(part);
                    }
                    else
                    {
                        current.Clear();
                        current.Append(candidate);
                    }
                }
            }
            if (current.Length > 0) lines.Add(current.ToString());
            return lines;
        }

        private static IEnumerable<string> SplitByWidth(string word, double boxWidthMm, double height,
            FontMetrics metrics)
        {
            if (Fits(word, height, boxWidthMm, metrics) || word.Length < 2)
            {
                yield return word;
                yield break;
            }
            var chunk = new StringBuilder();
            foreach (var ch in word)
            {
                var candidate = chunk.ToString() + ch;
                if (chunk.Length > 0 && !Fits(candidate, height, boxWidthMm, metrics))
                {
                    yield return chunk.ToString();
                    chunk.Clear();
                }
                chunk.Append(ch);
            }
            if (chunk.Length > 0) yield return chunk.ToString();
        }

        /// <summary>
        /// Влезает ли строка в ширину при заданном кегле. Допуск относительный: кегль считается
        /// ровно по ширине самого длинного слова, и без допуска строка на границе дробилась бы
        /// из-за погрешности double («Парамет|р» вместо «Параметр»).
        /// </summary>
        private static bool Fits(string text, double height, double boxWidthMm, FontMetrics metrics) =>
            metrics.TotalAdvance(text) * height <= boxWidthMm * (1 + 1e-9);

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
