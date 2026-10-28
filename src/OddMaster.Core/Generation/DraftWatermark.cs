using System;
using System.Collections.Generic;
using OddMaster.Core.Geometry;
using OddMaster.Core.Typography;

namespace OddMaster.Core.Generation
{
    /// <summary>
    /// Раскладка баннера «ЧЕРНОВИК» внутри габарита знака.
    /// Один источник для SVG и DXF: оба рендера только рисуют, раскладка — в ядре.
    /// </summary>
    public static class DraftWatermark
    {
        /// <summary>Строки баннера. Формулировка фиксирована, чтобы артефакты были сравнимы.</summary>
        public static readonly IReadOnlyList<string> Lines = new[] { "ЧЕРНОВИК:", "есть непроверенные параметры" };

        /// <summary>Раскладка баннера в системе координат знака (Y вверх, центр в 0).</summary>
        public static IReadOnlyList<TextLayout> Layout(SignGeometry geometry, FontMetrics font)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            if (font == null) throw new ArgumentNullException(nameof(font));
            var bounds = geometry.Bounds;
            var boxWidth = bounds.Width * 0.90;
            var boxHeight = bounds.Height * 0.18;
            var center = new Point2(0, bounds.MinY + boxHeight * 0.60);
            return TextEngine.LayoutBlock(Lines, boxWidth, boxHeight, font, center);
        }
    }
}
