using System;
using System.Collections.Generic;

namespace OddMaster.Core.Geometry
{
    /// <summary>
    /// Отсечение многоугольника по выпуклому контуру (алгоритм Сазерленда—Ходжмена).
    /// Нужно составу знака: диагональная полоса «во всё поле» не должна вылезать за ромб
    /// или круг — иначе на превью видны «уши» за контуром (CR-22).
    /// Операция детерминирована: порядок вершин и рёбер фиксирован.
    /// </summary>
    public static class PolygonClip
    {
        private const double Epsilon = 1e-9;

        /// <summary>
        /// Отсекает subject по выпуклому clip. Возвращает пустой список, если пересечения нет.
        /// Ориентация clip может быть любой: рёбра считаются «положительной» полуплоскостью.
        /// </summary>
        public static IReadOnlyList<Point2> Clip(IReadOnlyList<Point2> subject, IReadOnlyList<Point2> clip)
        {
            if (subject == null) throw new ArgumentNullException(nameof(subject));
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            if (subject.Count < 3 || clip.Count < 3) return Array.Empty<Point2>();
            var output = new List<Point2>(subject);
            var clockwise = SignedArea(clip) < 0;
            for (var i = 0; i < clip.Count && output.Count > 0; i++)
            {
                var a = clip[i];
                var b = clip[(i + 1) % clip.Count];
                var input = output;
                output = new List<Point2>(input.Count + 4);
                for (var j = 0; j < input.Count; j++)
                {
                    var current = input[j];
                    var previous = input[(j + input.Count - 1) % input.Count];
                    var currentInside = Inside(current, a, b, clockwise);
                    var previousInside = Inside(previous, a, b, clockwise);
                    if (currentInside)
                    {
                        if (!previousInside) Add(output, Intersect(previous, current, a, b));
                        Add(output, current);
                    }
                    else if (previousInside)
                    {
                        Add(output, Intersect(previous, current, a, b));
                    }
                }
            }
            return output.Count >= 3 ? output : (IReadOnlyList<Point2>)Array.Empty<Point2>();
        }

        /// <summary>Площадь многоугольника со знаком (положительная — против часовой).</summary>
        public static double SignedArea(IReadOnlyList<Point2> polygon)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            var sum = 0.0;
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                sum += a.X * b.Y - b.X * a.Y;
            }
            return sum / 2;
        }

        /// <summary>Строит замкнутый контур из вершин (с отбрасыванием повторов).</summary>
        public static Path ToPath(IReadOnlyList<Point2> polygon)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            if (polygon.Count < 3) throw new ArgumentException("Многоугольник должен иметь не меньше трёх вершин.",
                nameof(polygon));
            var path = new Path(polygon[0]);
            for (var i = 1; i < polygon.Count; i++) path.LineTo(polygon[i]);
            return path.Close();
        }

        private static bool Inside(Point2 p, Point2 a, Point2 b, bool clockwise)
        {
            var cross = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            return clockwise ? cross <= Epsilon : cross >= -Epsilon;
        }

        private static Point2 Intersect(Point2 p1, Point2 p2, Point2 a, Point2 b)
        {
            var dx1 = p2.X - p1.X;
            var dy1 = p2.Y - p1.Y;
            var dx2 = b.X - a.X;
            var dy2 = b.Y - a.Y;
            var denominator = dx1 * dy2 - dy1 * dx2;
            if (Math.Abs(denominator) < Epsilon) return p2;
            var t = ((a.X - p1.X) * dy2 - (a.Y - p1.Y) * dx2) / denominator;
            return new Point2(p1.X + t * dx1, p1.Y + t * dy1);
        }

        private static void Add(List<Point2> points, Point2 point)
        {
            if (points.Count > 0 && points[points.Count - 1].DistanceTo(point) < Epsilon) return;
            points.Add(point);
        }
    }
}
