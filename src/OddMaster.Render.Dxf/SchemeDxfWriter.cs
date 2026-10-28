using System;
using System.Collections.Generic;
using System.Linq;
using OddMaster.Core.Alignment;
using OddMaster.Core.Config;
using OddMaster.Core.Generation;
using OddMaster.Core.Geometry;
using OddMaster.Core.Scheme;
using OddMaster.Core.Typography;

namespace OddMaster.Render.Dxf
{
    /// <summary>
    /// Запись схемы дислокации в DXF: ось, пикетные отметки, опоры, стопки мини-знаков,
    /// выноски и баннер «ЧЕРНОВИК/ДЕМО». Координаты — метры, как в журнале координат.
    /// </summary>
    public static class SchemeDxfWriter
    {
        /// <summary>Строит документ схемы. Геометрии знаков передаются по ключу StatementKey.</summary>
        public static DxfBackend Build(SchemeModel scheme,
            IReadOnlyDictionary<string, SignGeometry> geometries, DrawingConventions drawing, FontMetrics font)
        {
            if (scheme == null) throw new ArgumentNullException(nameof(scheme));
            if (geometries == null) throw new ArgumentNullException(nameof(geometries));
            if (drawing == null) throw new ArgumentNullException(nameof(drawing));
            if (font == null) throw new ArgumentNullException(nameof(font));

            var backend = new DxfBackend(drawing.DxfFlattenSagittaMm) { InsUnits = 6 };
            backend.AddLayer("AXIS", DxfColorMap.Aci("Black"));
            backend.AddLayer("STATION", DxfColorMap.Aci("Grey"));
            backend.AddLayer("POST", DxfColorMap.Aci("Black"));
            backend.AddLayer("LABEL", DxfColorMap.Aci("Black"));
            backend.AddLayer("BANNER", DxfColorMap.Aci("Red"));

            var log = SchemeReactor.Compute(scheme);
            var tickRadius = PaperToDrawing(drawing.PostSymbolRadiusMm, drawing);
            var miniWidth = PaperToDrawing(drawing.MiniSignOnPaperMm, drawing);
            var labelHeight = tickRadius * 1.6;

            // Ось.
            backend.AddPolyline(scheme.Alignment.Vertices, false, "AXIS");

            // Пикетные отметки и их подписи.
            var tick = drawing.StationTickEveryM;
            if (tick > 0)
            {
                var last = scheme.Alignment.InitialStationMeters + scheme.Alignment.Length;
                for (var station = scheme.Alignment.InitialStationMeters; station <= last + 1e-9; station += tick)
                {
                    var point = scheme.Alignment.PointAt(station).Point;
                    backend.AddLine(new Point2(point.X, point.Y - tickRadius),
                        new Point2(point.X, point.Y + tickRadius), "STATION");
                    backend.AddText(new Station(station).Format(),
                        new Point2(point.X + tickRadius * 0.4, point.Y - tickRadius * 2.2),
                        labelHeight, "STATION");
                }
            }

            // Опоры, стопки знаков и выноски.
            foreach (var post in scheme.Posts)
            {
                var row = log.Rows.First(r => r.PostId == post.Id);
                backend.AddCircle(new Point2(row.X, row.Y), tickRadius, "POST");

                var stackY = row.Y - tickRadius * 2;
                for (var index = 0; index < post.Mounts.Count; index++)
                {
                    var sign = post.Mounts[index];
                    if (!geometries.TryGetValue(sign.StatementKey, out var geometry)) continue;
                    var scale = miniWidth / geometry.WidthMm;
                    stackY -= geometry.HeightMm * scale + tickRadius;
                    var scope = "P" + Index(post.Id) + "." + sign.Number.ToString().Replace('.', '-') + "." + index;
                    backend.AddSign(geometry, new Point2(row.X, stackY), scale, SanitizeScope(scope), font);
                }

                backend.AddLine(new Point2(row.X, row.Y - tickRadius),
                    new Point2(row.X, stackY), "POST");
                backend.AddText(post.Id + ": " + post.SignsTopDown(),
                    new Point2(row.X - miniWidth * 0.5, stackY - tickRadius * 2.4),
                    labelHeight, "LABEL");
            }

            // Баннер черновика: без него стенд нельзя показывать как норму.
            var points = new List<Point2>(scheme.Alignment.Vertices);
            foreach (var row in log.Rows) points.Add(new Point2(row.X, row.Y));
            var minX = points.Min(p => p.X);
            var maxY = points.Max(p => p.Y);
            backend.AddText(drawing.DraftBanner, new Point2(minX, maxY + tickRadius * 12),
                labelHeight, "BANNER");
            return backend;
        }

        private static double PaperToDrawing(double paperMm, DrawingConventions drawing) =>
            paperMm * drawing.SheetScaleDenominator / 1000.0;

        private static int Index(string postId)
        {
            var digits = new string(postId.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var value) ? value : postId.Length;
        }

        /// <summary>Имя слоя в DXF не должно содержать пробелов и служебных символов.</summary>
        private static string SanitizeScope(string scope)
        {
            var cleaned = new string(scope.Select(c =>
                char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-' ? c : '_').ToArray());
            return cleaned.Length == 0 ? "S" : cleaned;
        }
    }
}
