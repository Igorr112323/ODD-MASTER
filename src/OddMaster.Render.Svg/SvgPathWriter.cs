using System;
using System.Globalization;
using System.Text;
using OddMaster.Core.Geometry;

namespace OddMaster.Render.Svg
{
    /// <summary>Преобразование контура в атрибут d SVG-пути. Формат чисел инвариантный, 3 знака.</summary>
    public static class SvgPathWriter
    {
        /// <summary>Строит атрибут d: M/L/A/Z. Ось Y переворачивается группой SVG, числа здесь — в мм.</summary>
        public static string Write(Path path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            var sb = new StringBuilder();
            sb.Append('M').Append(Point(path.Start));
            foreach (var segment in path.Segments)
            {
                if (segment is ArcSegment arc)
                {
                    var large = Math.Abs(arc.SweepDeg) > 180.0 ? 1 : 0;
                    var sweep = arc.SweepDeg >= 0 ? 1 : 0;
                    sb.Append(" A").Append(Num(arc.Radius)).Append(' ').Append(Num(arc.Radius))
                      .Append(" 0 ").Append(large).Append(' ').Append(sweep).Append(' ')
                      .Append(Point(arc.End));
                }
                else sb.Append(" L").Append(Point(segment.End));
            }
            if (path.IsClosed) sb.Append(" Z");
            return sb.ToString();
        }

        private static string Point(Point2 p) => Num(p.X) + " " + Num(p.Y);

        /// <summary>Число в инвариантной культуре без экспоненты и лишних нулей.</summary>
        public static string Num(double value)
        {
            var rounded = Math.Round(value, 3, MidpointRounding.AwayFromZero);
            if (Math.Abs(rounded) < 0.0005) rounded = 0;
            return rounded.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
