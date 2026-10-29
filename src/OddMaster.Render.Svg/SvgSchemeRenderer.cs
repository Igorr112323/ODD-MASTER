using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OddMaster.Core.Config;
using OddMaster.Core.Geometry;
using OddMaster.Core.Generation;
using OddMaster.Core.Scheme;

namespace OddMaster.Render.Svg
{
    /// <summary>Рендер схемы дислокации: ось, пикеты, опоры, мини-знаки и баннер ЧЕРНОВИК/ДЕМО.</summary>
    public sealed class SvgSchemeRenderer
    {
        private readonly DrawingConventions _drawing;

        /// <summary>Создаёт рендер схемы.</summary>
        public SvgSchemeRenderer(DrawingConventions drawing)
        {
            _drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
        }

        /// <summary>Рисует схему. Геометрии знаков передаются по ключу StatementKey.</summary>
        public string Render(SchemeModel scheme, IReadOnlyDictionary<string, SignGeometry> geometries)
        {
            if (scheme == null) throw new ArgumentNullException(nameof(scheme));
            if (geometries == null) throw new ArgumentNullException(nameof(geometries));
            var log = SchemeReactor.Compute(scheme);
            var points = new List<Point2>(scheme.Alignment.Vertices);
            foreach (var row in log.Rows) points.Add(new Point2(row.X, row.Y));
            var minX = points.Min(p => p.X) - 60;
            var maxX = points.Max(p => p.X) + 60;
            var minY = points.Min(p => p.Y) - 60;
            var maxY = points.Max(p => p.Y) + 60;
            var width = maxX - minX;
            var height = maxY - minY;
            var sb = new StringBuilder();

            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"" +
                SvgPathWriter.Num(minX) + " " + SvgPathWriter.Num(-maxY) + " " +
                SvgPathWriter.Num(width) + " " + SvgPathWriter.Num(height) + "\">");
            sb.AppendLine("  <metadata><odd:scheme xmlns:odd=\"https://odd-master.local/metadata/1\"");
            sb.AppendLine("      id=\"" + SvgSignRenderer.Escape(scheme.Id) + "\"");
            sb.AppendLine("      posts=\"" + scheme.Posts.Count.ToString(CultureInfo.InvariantCulture) + "\"");
            sb.AppendLine("      scale=\"1:" + SvgPathWriter.Num(_drawing.SheetScaleDenominator) + "\"");
            sb.AppendLine("      colorimetry=\"DISPLAY_ONLY_NOT_GOST\"/>");
            sb.AppendLine("  </metadata>");
            sb.AppendLine("  <g id=\"Geometry\" transform=\"scale(1,-1)\">");
            sb.AppendLine("    <g id=\"Alignment\"><polyline fill=\"none\" stroke=\"#1A1A1A\" stroke-width=\"1.5\" points=\"" +
                string.Join(" ", scheme.Alignment.Vertices.Select(v => SvgPathWriter.Num(v.X) + "," + SvgPathWriter.Num(v.Y))) +
                "\"/></g>");

            var tickRadius = _drawing.PostSymbolRadiusMm * _drawing.SheetScaleDenominator / 1000.0;
            var miniWidthM = _drawing.MiniSignOnPaperMm * _drawing.SheetScaleDenominator / 1000.0;
            var labels = new List<string>();

            // Пикетные отметки каждые stationTickEveryM метров.
            var tick = _drawing.StationTickEveryM;
            if (tick > 0)
            {
                sb.AppendLine("    <g id=\"StationTicks\">");
                var length = scheme.Alignment.Length;
                for (var station = scheme.Alignment.InitialStationMeters;
                     station <= scheme.Alignment.InitialStationMeters + length + 1e-9; station += tick)
                {
                    var p = scheme.Alignment.PointAt(station).Point;
                    sb.Append("      <line x1=\"").Append(SvgPathWriter.Num(p.X))
                      .Append("\" y1=\"").Append(SvgPathWriter.Num(p.Y - tickRadius))
                      .Append("\" x2=\"").Append(SvgPathWriter.Num(p.X))
                      .Append("\" y2=\"").Append(SvgPathWriter.Num(p.Y + tickRadius))
                      .AppendLine("\" stroke=\"#808080\" stroke-width=\"0.8\"/>");
                    labels.Add("  <text x=\"" + SvgPathWriter.Num(p.X) + "\" y=\""
                        + SvgPathWriter.Num(-p.Y + tickRadius * 2.5)
                        + "\" text-anchor=\"middle\" font-size=\"" + SvgPathWriter.Num(tickRadius * 1.6)
                        + "\" font-family=\"Arial, sans-serif\" fill=\"#1A1A1A\">"
                        + new OddMaster.Core.Alignment.Station(station).Format() + "</text>");
                }
                sb.AppendLine("    </g>");
            }

            foreach (var post in scheme.Posts)
            {
                var row = log.Rows.First(r => r.PostId == post.Id);
                sb.AppendLine("    <g id=\"Post-" + SvgSignRenderer.Escape(post.Id) + "\">");
                sb.Append("      <circle cx=\"").Append(SvgPathWriter.Num(row.X))
                  .Append("\" cy=\"").Append(SvgPathWriter.Num(row.Y))
                  .Append("\" r=\"").Append(SvgPathWriter.Num(tickRadius))
                  .AppendLine("\" fill=\"#FFFFFF\" stroke=\"#1A1A1A\" stroke-width=\"0.8\"/>");
                var stackY = row.Y - tickRadius * 2;
                foreach (var sign in post.Mounts)
                {
                    if (!geometries.TryGetValue(sign.StatementKey, out var geometry)) continue;
                    var scale = miniWidthM / geometry.WidthMm;
                    stackY -= geometry.HeightMm * scale + tickRadius;
                    sb.Append("      <g transform=\"translate(").Append(SvgPathWriter.Num(row.X))
                      .Append(" ").Append(SvgPathWriter.Num(stackY)).Append(") scale(")
                      .Append(SvgPathWriter.Num(scale)).Append(")\">");
                    foreach (var layer in geometry.Layers)
                    {
                        if (layer.Geometry == null) continue;
                        sb.Append("<path d=\"").Append(SvgPathWriter.Write(layer.Geometry)).Append("\" ")
                          .Append(layer.Filled
                              ? "fill=\"" + SvgPalette.Resolve(layer.ColorName) + "\" stroke=\"none\""
                              : "fill=\"none\" stroke=\"" + SvgPalette.Resolve(layer.ColorName) +
                                "\" stroke-width=\"" + SvgPathWriter.Num(layer.StrokeWidthMm) + "\"")
                          .Append("/>");
                    }
                    sb.AppendLine("</g>");
                }
                sb.Append("      <line x1=\"").Append(SvgPathWriter.Num(row.X))
                  .Append("\" y1=\"").Append(SvgPathWriter.Num(row.Y - tickRadius))
                  .Append("\" x2=\"").Append(SvgPathWriter.Num(row.X))
                  .Append("\" y2=\"").Append(SvgPathWriter.Num(stackY))
                  .AppendLine("\" stroke=\"#1A1A1A\" stroke-width=\"0.5\" stroke-dasharray=\"2,2\"/>");
                sb.AppendLine("    </g>");
                labels.Add("  <text x=\"" + SvgPathWriter.Num(row.X) + "\" y=\""
                    + SvgPathWriter.Num(-stackY - tickRadius) + "\" text-anchor=\"middle\" font-size=\""
                    + SvgPathWriter.Num(tickRadius * 1.8)
                    + "\" font-family=\"Arial, sans-serif\" fill=\"#1A1A1A\">"
                    + SvgSignRenderer.Escape(post.Id + ": " + post.SignsTopDown()) + "</text>");
            }
            sb.AppendLine("  </g>");
            foreach (var label in labels) sb.AppendLine(label);
            sb.Append("  <g id=\"DraftBanner\"><text x=\"").Append(SvgPathWriter.Num(minX + 10))
              .Append("\" y=\"").Append(SvgPathWriter.Num(-maxY + 15))
              .Append("\" font-family=\"Arial, sans-serif\" font-size=\"8\" fill=\"#D7191C\">")
              .Append(SvgSignRenderer.Escape(_drawing.DraftBanner)).AppendLine("</text></g>");
            sb.AppendLine("</svg>");
            return sb.ToString().Replace("\r\n", "\n");
        }
    }
}
