using System;
using OddMaster.Core.Generation;
using OddMaster.Core.Geometry;

namespace OddMaster.Core.Ports
{
    /// <summary>Отправляет геометрию знака в порт САПР. Не знает ни про MultiCAD, ни про SVG.</summary>
    public static class GeometryPortWriter
    {
        /// <summary>Рисует все слои и надписи знака в заданном масштабе и положении.</summary>
        public static void Write(IDrawingPort port, SignGeometry geometry, Point2 origin, double scale)
        {
            if (port == null) throw new ArgumentNullException(nameof(port));
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            foreach (var layer in geometry.Layers)
            {
                if (layer.Geometry == null) continue;
                var name = layer.Kind.ToString();
                if (layer.Filled)
                {
                    port.DrawFilledPath(TransformPath(layer.Geometry, origin, scale), name);
                    continue;
                }
                foreach (var segment in layer.Geometry.Segments)
                {
                    if (segment is ArcSegment arc)
                        port.DrawArc(Transform(arc.Center, origin, scale), arc.Radius * scale,
                            arc.StartAngleDeg, arc.SweepDeg, name);
                    else
                        port.DrawLine(Transform(segment.Start, origin, scale),
                            Transform(segment.End, origin, scale), name);
                }
            }
            foreach (var text in geometry.Texts)
            {
                var anchor = text.Layout != null ? text.Layout.BaselineStart : text.Anchor;
                port.DrawText(text.Value, Transform(anchor, origin, scale),
                    text.HeightMm * scale, "Text");
            }
        }

        private static Point2 Transform(Point2 point, Point2 origin, double scale) =>
            new Point2(origin.X + point.X * scale, origin.Y + point.Y * scale);

        /// <summary>Масштабирует контур целиком. Углы дуг сохраняются при равномерном масштабе.</summary>
        public static Path TransformPath(Path path, Point2 origin, double scale)
        {
            var result = new Path(Transform(path.Start, origin, scale));
            foreach (var segment in path.Segments)
            {
                if (segment is ArcSegment arc)
                    result.ArcTo(Transform(arc.Center, origin, scale), arc.Radius * scale,
                        arc.StartAngleDeg, arc.SweepDeg);
                else result.LineTo(Transform(segment.End, origin, scale));
            }
            if (path.IsClosed) result.Close();
            return result;
        }
    }
}
