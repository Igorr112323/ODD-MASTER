using System;
using System.Collections.Generic;
using System.Text;
using OddMaster.Core.Config;
using OddMaster.Core.Geometry;
using OddMaster.Core.Generation;
using OddMaster.Core.Typography;

namespace OddMaster.Render.Svg
{
    /// <summary>Рендер знака в SVG. Никаких зависимостей от САПР; только строки и детерминизм.</summary>
    public sealed class SvgSignRenderer
    {
        private readonly DrawingConventions _drawing;
        private readonly FontMetrics _font;

        /// <summary>Создаёт рендер с соглашениями отрисовки и метриками шрифта.</summary>
        public SvgSignRenderer(DrawingConventions drawing, FontMetrics font)
        {
            _drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
            _font = font ?? throw new ArgumentNullException(nameof(font));
        }

        /// <summary>Формирует SVG-документ. Одинаковый вход даёт байт-в-байт одинаковый результат.</summary>
        public string Render(SignGeometry geometry)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            var bounds = geometry.Bounds;
            var pad = Math.Max(bounds.Width, bounds.Height) * _drawing.SvgPaddingRatio;
            var viewBox = string.Join(" ", new[]
            {
                SvgPathWriter.Num(bounds.MinX - pad), SvgPathWriter.Num(-bounds.MaxY - pad),
                SvgPathWriter.Num(bounds.Width + 2 * pad), SvgPathWriter.Num(bounds.Height + 2 * pad)
            });

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"" + viewBox + "\" width=\"" +
                SvgPathWriter.Num(bounds.Width + 2 * pad) + "mm\" height=\"" +
                SvgPathWriter.Num(bounds.Height + 2 * pad) + "mm\">");
            sb.AppendLine("  <metadata><odd:sign xmlns:odd=\"https://odd-master.local/metadata/1\"");
            sb.AppendLine("      number=\"" + Escape(geometry.Sign.Number.ToString()) + "\"");
            sb.AppendLine("      sizeClass=\"" + Escape(geometry.SizeClass) + "\"");
            sb.AppendLine("      profile=\"" + Escape(geometry.ProfileId) + "\"");
            sb.AppendLine("      sizeTable=\"" + Escape(geometry.SizeTableId) + "\"");
            sb.AppendLine("      outerBorderMm=\"" + Escape(SvgPathWriter.Num(geometry.OuterBorderWidthMm)) + "\"");
            sb.AppendLine("      verification=\"" + Escape(geometry.Verification.Describe()) + "\"");
            sb.AppendLine("      isDraft=\"" + (geometry.IsDraft ? "true" : "false") + "\"");
            sb.AppendLine("      colorimetry=\"DISPLAY_ONLY_NOT_GOST\"/>");
            sb.AppendLine("  </metadata>");

            // Геометрия задана в мм с осью Y вверх; SVG рисует Y вниз.
            sb.AppendLine("  <g id=\"Geometry\" transform=\"scale(1,-1)\">");
            foreach (var layer in geometry.Layers)
            {
                if (layer.Geometry == null) continue;
                sb.AppendLine("    <g id=\"Layer-" + layer.Kind + "\">");
                sb.Append("      <path d=\"").Append(SvgPathWriter.Write(layer.Geometry)).Append("\" ")
                  .Append(layer.Filled
                      ? "fill=\"" + SvgPalette.Resolve(layer.ColorName) + "\" stroke=\"none\""
                      : "fill=\"none\" stroke=\"" + SvgPalette.Resolve(layer.ColorName) +
                        "\" stroke-width=\"" + SvgPathWriter.Num(layer.StrokeWidthMm) + "\"")
                  .AppendLine("/>");
                sb.AppendLine("    </g>");
            }
            sb.AppendLine("  </g>");

            foreach (var text in geometry.Texts)
            {
                // CR-07: якорь и кегль посчитаны TextEngine; text-anchor/dominant-baseline не нужны.
                var layout = text.Layout;
                var x = layout != null ? layout.BaselineStart.X : text.Anchor.X - text.HeightMm;
                var y = layout != null ? -layout.BaselineStart.Y : -text.Anchor.Y;
                sb.Append("  <g id=\"Layer-Text\"><text x=\"")
                  .Append(SvgPathWriter.Num(x)).Append("\" y=\"")
                  .Append(SvgPathWriter.Num(y)).Append("\" font-family=\"")
                  .Append(Escape(text.FontFamily)).Append("\" font-weight=\"bold\" font-size=\"")
                  .Append(SvgPathWriter.Num(text.HeightMm)).Append("\" fill=\"")
                  .Append(SvgPalette.Resolve(text.ColorName)).Append("\">")
                  .Append(Escape(text.Value)).AppendLine("</text></g>");
            }

            if (geometry.IsDraft)
            {
                // CR-08: две строки, кегль подобран так, чтобы габарит водяного знака
                // находился внутри габарита знака. Инвариант проверяет тест M9-V06.
                sb.AppendLine("  <g id=\"DraftWatermark\">");
                foreach (var line in Watermark(geometry))
                {
                    sb.Append("    <text x=\"").Append(SvgPathWriter.Num(line.BaselineStart.X))
                      .Append("\" y=\"").Append(SvgPathWriter.Num(-line.BaselineStart.Y))
                      .Append("\" font-family=\"").Append(Escape(line.FontFamily))
                      .Append("\" font-size=\"").Append(SvgPathWriter.Num(line.HeightMm))
                      .Append("\" fill=\"#D7191C\" fill-opacity=\"0.85\">")
                      .Append(Escape(line.Value)).AppendLine("</text>");
                }
                sb.AppendLine("  </g>");
            }
            sb.AppendLine("</svg>");
            return sb.ToString().Replace("\r\n", "\n");
        }

        /// <summary>Раскладка водяного знака: доступна тестам как инвариант «внутри габарита».</summary>
        public IReadOnlyList<TextLayout> Watermark(SignGeometry geometry)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            var bounds = geometry.Bounds;
            var lines = new[] { "ЧЕРНОВИК:", "есть непроверенные параметры" };
            var boxWidth = bounds.Width * 0.90;
            var boxHeight = bounds.Height * 0.18;
            var center = new Point2(0, bounds.MinY + boxHeight * 0.60);
            return TextEngine.LayoutBlock(lines, boxWidth, boxHeight, _font, center);
        }

        /// <summary>Экранирование XML-текста.</summary>
        public static string Escape(string? value)
        {
            if (value == null) return string.Empty;
            return value.Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal)
                .Replace("\"", "&quot;", StringComparison.Ordinal)
                .Replace("'", "&apos;", StringComparison.Ordinal);
        }
    }
}
