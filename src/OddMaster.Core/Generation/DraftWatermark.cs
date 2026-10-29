using System;
using System.Collections.Generic;
using System.Linq;
using OddMaster.Core.Catalog;
using OddMaster.Core.Geometry;
using OddMaster.Core.Typography;

namespace OddMaster.Core.Generation
{
    /// <summary>
    /// Раскладка баннера «ЧЕРНОВИК» внутри поля знака.
    /// Один источник для SVG и DXF: оба рендера только рисуют, раскладка — в ядре.
    /// </summary>
    public static class DraftWatermark
    {
        /// <summary>Строки баннера. Формулировка фиксирована, чтобы артефакты были сравнимы.</summary>
        public static readonly IReadOnlyList<string> Lines = new[] { "ЧЕРНОВИК:", "есть непроверенные параметры" };

        // Кандидаты положения и масштаба перебираются в фиксированном порядке — результат детерминирован.
        private static readonly double[] CenterFractions = { 0.55, 0.40, 0.65, 0.50, 0.30 };
        private static readonly double[] Scales = { 1.0, 0.85, 0.72, 0.60, 0.48, 0.38, 0.30 };

        /// <summary>
        /// Раскладка баннера в системе координат знака (Y вверх, центр в 0).
        /// CR-24: надпись обязана целиком лежать внутри поля (многоугольника), а не только
        /// внутри габаритного прямоугольника: на круге и треугольнике углы bbox уже за контуром.
        /// </summary>
        public static IReadOnlyList<TextLayout> Layout(SignGeometry geometry, FontMetrics font)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            if (font == null) throw new ArgumentNullException(nameof(font));
            var field = FieldPath(geometry);
            var bounds = geometry.Bounds;
            if (field == null) return LayoutInside(bounds, null, font);

            foreach (var fraction in CenterFractions)
                foreach (var scale in Scales)
                {
                    var lines = LayoutInside(bounds, field, font, fraction, scale);
                    if (IsInside(field, lines)) return lines;
                }
            // Крайний случай: поле слишком узкое для двух строк — одна строка «ЧЕРНОВИК» по центру.
            foreach (var fraction in CenterFractions)
                foreach (var scale in Scales)
                {
                    var lines = LayoutSingle(field, bounds, font, fraction, scale);
                    if (IsInside(field, lines)) return lines;
                }
            return LayoutInside(bounds, null, font);
        }

        /// <summary>Поле знака: слой Field, а если его нет — контур.</summary>
        private static Path? FieldPath(SignGeometry geometry)
        {
            var field = geometry.Layers.FirstOrDefault(l => l.Kind == SignLayerKind.Field && l.Geometry != null);
            return field?.Geometry ?? geometry.Outline;
        }

        private static bool IsInside(Path field, IReadOnlyList<TextLayout> lines)
        {
            if (lines.Count == 0) return false;
            foreach (var line in lines)
                if (!PathQueries.ContainsRectangle(field, line.Box())) return false;
            return true;
        }

        private static IReadOnlyList<TextLayout> LayoutInside(BoundingBox bounds, Path? field,
            FontMetrics font, double centerFraction = 0.55, double scale = 1.0)
        {
            var boxWidth = bounds.Width * 0.90 * scale;
            var boxHeight = bounds.Height * 0.18 * scale;
            var center = new Point2(0, bounds.MinY + bounds.Height * centerFraction);
            return TextEngine.LayoutBlock(Lines, boxWidth, boxHeight, font, center);
        }

        private static IReadOnlyList<TextLayout> LayoutSingle(Path field, BoundingBox bounds,
            FontMetrics font, double centerFraction, double scale)
        {
            var boxWidth = bounds.Width * 0.80 * scale;
            var boxHeight = bounds.Height * 0.10 * scale;
            var center = new Point2(0, bounds.MinY + bounds.Height * centerFraction);
            return TextEngine.LayoutBlock(new[] { Lines[0] }, boxWidth, boxHeight, font, center);
        }
    }
}
