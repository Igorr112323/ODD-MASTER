using System;
using System.Collections.Generic;

namespace OddMaster.Core.Geometry
{
    /// <summary>
    /// Аппроксимация контура ломаной с заданной стрелкой прогиба (sagitta).
    /// Нужна там, где у приёмника нет дуг (полилинии DXF, тесселяция для проверок).
    /// Детерминизм: число хорд зависит только от радиуса, развёртки и допуска.
    /// </summary>
    public static class PathFlattener
    {
        /// <summary>Точки ломаной: начало контура, промежуточные вершины и конец каждого сегмента.</summary>
        public static IReadOnlyList<Point2> Flatten(Path path, double maxSagittaMm)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!(maxSagittaMm > 0)) throw new ArgumentOutOfRangeException(nameof(maxSagittaMm),
                "Допуск стрелки прогиба должен быть положительным, получено " + maxSagittaMm + ".");
            var points = new List<Point2> { path.Start };
            foreach (var segment in path.Segments)
            {
                if (segment is ArcSegment arc) AppendArc(points, arc, maxSagittaMm);
                else AppendDistinct(points, segment.End);
            }
            return points;
        }

        /// <summary>Точки замкнутой ломаной без повторения начальной вершины в конце.</summary>
        public static IReadOnlyList<Point2> FlattenClosed(Path path, double maxSagittaMm)
        {
            var points = new List<Point2>(Flatten(path, maxSagittaMm));
            if (points.Count > 1 && points[points.Count - 1].DistanceTo(points[0]) < 1e-9)
                points.RemoveAt(points.Count - 1);
            return points;
        }

        /// <summary>Число хорд для дуги при допуске: ceil(|развёртка| / максимальный шаг).</summary>
        public static int ChordCount(double radius, double sweepDeg, double maxSagittaMm)
        {
            if (!(maxSagittaMm > 0)) throw new ArgumentOutOfRangeException(nameof(maxSagittaMm));
            var sweep = Math.Abs(sweepDeg);
            if (sweep < 1e-12) return 0;
            if (radius <= 0) return 1;
            // sagitta = r * (1 - cos(step/2)); при sagitta >= r шаг не ограничен.
            var cosine = 1.0 - maxSagittaMm / radius;
            if (cosine <= -1.0) return 1;
            var maxStepDeg = 2.0 * Math.Acos(cosine) * 180.0 / Math.PI;
            if (maxStepDeg <= 1e-9) return 1;
            return Math.Max(1, (int)Math.Ceiling(sweep / maxStepDeg - 1e-12));
        }

        private static void AppendArc(List<Point2> points, ArcSegment arc, double maxSagittaMm)
        {
            var count = ChordCount(arc.Radius, arc.SweepDeg, maxSagittaMm);
            if (count <= 0) return;
            var step = arc.SweepDeg / count;
            for (var i = 1; i < count; i++)
            {
                var angle = (arc.StartAngleDeg + step * i) * Math.PI / 180.0;
                AppendDistinct(points, new Point2(arc.Center.X + arc.Radius * Math.Cos(angle),
                    arc.Center.Y + arc.Radius * Math.Sin(angle)));
            }
            // Конец дуги берём точно из сегмента: стык не должен «плыть» от тригонометрии.
            AppendDistinct(points, arc.End);
        }

        private static void AppendDistinct(List<Point2> points, Point2 point)
        {
            // CR-09: повторные вершины ломают полилинию в САПР.
            if (points.Count > 0 && points[points.Count - 1].DistanceTo(point) < 1e-9) return;
            points.Add(point);
        }
    }

    /// <summary>Тип примитива, которым контур уходит в приёмник.</summary>
    public enum FlatShapeKind
    {
        /// <summary>В контуре были только отрезки.</summary>
        Polyline,
        /// <summary>В контуре были дуги: при аппроксимации ломаной погрешность ограничена допуском.</summary>
        Flattened
    }
}
