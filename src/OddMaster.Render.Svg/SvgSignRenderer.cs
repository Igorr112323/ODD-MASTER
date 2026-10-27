using System;
using System.Globalization;
using System.Text;
using OddMaster.Core.Config;
using OddMaster.Core.Generation;

namespace OddMaster.Render.Svg
{
    /// <summary>Рендер знака в SVG. Никаких зависимостей от САПР; только строки и детерминизм.</summary>
    public sealed class SvgSignRenderer
    {
        private readonly DrawingConventions _drawing;

        /// <summary>Создаёт рендер с соглашениями отрисовки.</summary>
        public SvgSignRenderer(DrawingConventions drawing)
        {
            _drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
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
                sb.Append("  <g id=\"Layer-Text\"><text x=\"")
                  .Append(SvgPathWriter.Num(text.Anchor.X)).Append("\" y=\"")
                  .Append(SvgPathWriter.Num(-text.Anchor.Y)).Append("\" font-family=\"")
                  .Append(Escape(text.FontFamily)).Append("\" font-weight=\"bold\" font-size=\"")
                  .Append(SvgPathWriter.Num(text.HeightMm)).Append("\" text-anchor=\"middle\" dominant-baseline=\"central\" fill=\"")
                  .Append(SvgPalette.Resolve(text.ColorName)).Append("\">")
                  .Append(Escape(text.Value)).AppendLine("</text></g>");
            }

            if (geometry.IsDraft)
            {
                var watermarkSize = Math.Max(bounds.Width, bounds.Height) * 0.075;
                sb.Append("  <g id=\"DraftWatermark\"><text x=\"0\" y=\"")
                  .Append(SvgPathWriter.Num(-bounds.MaxY - pad * 0.35))
                  .Append("\" text-anchor=\"middle\" font-family=\"Arial, Liberation Sans, sans-serif\" font-size=\"")
                  .Append(SvgPathWriter.Num(watermarkSize)).Append("\" fill=\"#D7191C\" fill-opacity=\"0.85\">")
                  .Append("ЧЕРНОВИК: есть непроверенные параметры").AppendLine("</text></g>");
            }
            sb.AppendLine("</svg>");
            return sb.ToString().Replace("\r\n", "\n");
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
