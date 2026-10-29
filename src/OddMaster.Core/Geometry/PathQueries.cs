using System;
using System.Collections.Generic;

namespace OddMaster.Core.Geometry
{
    /// <summary>
    /// Проверки попадания точки в замкнутый контур. Нужны инварианту CR-24:
    /// водяной знак обязан лежать внутри поля знака, а не только внутри его габарита.
    /// </summary>
    public static class PathQueries
    {
        /// <summary>Допуск аппроксимации для проверок попадания, мм.</summary>
        private const double FlattenSagittaMm = 0.5;

        /// <summary>
        /// Лежит ли точка внутри замкнутого контура (правило чётности, контур аппроксимируется ломаной).
        /// Точка на границе считается внутри — инвариант не должен «дрожать» на касании.
        /// </summary>
        public static bool ContainsPoint(Path path, Point2 point)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            var polygon = PathFlattener.FlattenClosed(path, FlattenSagittaMm);
            return ContainsPoint(polygon, point, 1e-6);
        }

        /// <summary>Лежит ли точка внутри многоугольника (правило чётности).</summary>
        public static bool ContainsPoint(IReadOnlyList<Point2> polygon, Point2 point, double tolerance)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            if (polygon.Count < 3) return false;
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if (DistanceToSegment(point, a, b) <= tolerance) return true;
                var intersects = (a.Y > point.Y) != (b.Y > point.Y) &&
                                 point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X;
                if (intersects) inside = !inside;
            }
            return inside;
        }

        /// <summary>
        /// Все четыре угла прямоугольника внутри контура. Проверяются углы, а не центр:
        /// именно они первыми выходят за круг или скос треугольника (CR-24).
        /// </summary>
        public static bool ContainsRectangle(Path path, BoundingBox box) =>
            ContainsPoint(path, new Point2(box.MinX, box.MinY)) &&
            ContainsPoint(path, new Point2(box.MaxX, box.MinY)) &&
            ContainsPoint(path, new Point2(box.MinX, box.MaxY)) &&
            ContainsPoint(path, new Point2(box.MaxX, box.MaxY));

        /// <summary>Расстояние от точки до отрезка, мм.</summary>
        public static double DistanceToSegment(Point2 point, Point2 start, Point2 end)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var lengthSquared = dx * dx + dy * dy;
            if (lengthSquared < 1e-18) return point.DistanceTo(start);
            var t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared;
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            return point.DistanceTo(new Point2(start.X + t * dx, start.Y + t * dy));
        }
    }
}
