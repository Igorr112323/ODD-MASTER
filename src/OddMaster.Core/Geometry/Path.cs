using System;
using System.Collections.Generic;
using System.Linq;

namespace OddMaster.Core.Geometry
{
    /// <summary>Прямоугольник габарита: минимум/максимум по X и Y.</summary>
    public readonly struct BoundingBox
    {
        /// <summary>Создаёт габарит по двум углам.</summary>
        public BoundingBox(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY;
        }

        /// <summary>Минимум X.</summary>
        public double MinX { get; }
        /// <summary>Минимум Y.</summary>
        public double MinY { get; }
        /// <summary>Максимум X.</summary>
        public double MaxX { get; }
        /// <summary>Максимум Y.</summary>
        public double MaxY { get; }
        /// <summary>Ширина.</summary>
        public double Width => MaxX - MinX;
        /// <summary>Высота.</summary>
        public double Height => MaxY - MinY;

        /// <summary>Габарит по ширине/высоте с центром в начале координат.</summary>
        public static BoundingBox Centered(double width, double height) =>
            new BoundingBox(-width / 2, -height / 2, width / 2, height / 2);
    }

    /// <summary>Сегмент контура: отрезок или дуга.</summary>
    public interface IPathSegment
    {
        /// <summary>Начало сегмента.</summary>
        Point2 Start { get; }
        /// <summary>Конец сегмента.</summary>
        Point2 End { get; }
        /// <summary>Точки, достаточные для точного габарита (для дуг — экстремумы по осям).</summary>
        IEnumerable<Point2> ExtremePoints();
    }

    /// <summary>Отрезок прямой.</summary>
    public sealed class LineSegment : IPathSegment
    {
        /// <summary>Создаёт отрезок.</summary>
        public LineSegment(Point2 start, Point2 end) { Start = start; End = end; }

        /// <summary>Начало.</summary>
        public Point2 Start { get; }
        /// <summary>Конец.</summary>
        public Point2 End { get; }

        /// <summary>Концы отрезка.</summary>
        public IEnumerable<Point2> ExtremePoints() { yield return Start; yield return End; }
    }

    /// <summary>Дуга окружности. Углы — в градусах, положительное направление — против часовой (Y вверх).</summary>
    public sealed class ArcSegment : IPathSegment
    {
        /// <summary>Создаёт дугу по центру, радиусу и углам.</summary>
        public ArcSegment(Point2 center, double radius, double startAngleDeg, double sweepDeg)
        {
            Center = center; Radius = radius; StartAngleDeg = startAngleDeg; SweepDeg = sweepDeg;
            Start = PointAt(startAngleDeg);
            End = PointAt(startAngleDeg + sweepDeg);
        }

        /// <summary>Центр окружности.</summary>
        public Point2 Center { get; }
        /// <summary>Радиус.</summary>
        public double Radius { get; }
        /// <summary>Начальный угол, градусы.</summary>
        public double StartAngleDeg { get; }
        /// <summary>Развёртка дуги: &gt; 0 — против часовой, &lt; 0 — по часовой.</summary>
        public double SweepDeg { get; }

        /// <summary>Начало дуги.</summary>
        public Point2 Start { get; }
        /// <summary>Конец дуги.</summary>
        public Point2 End { get; }

        private Point2 PointAt(double angleDeg)
        {
            var rad = angleDeg * Math.PI / 180.0;
            return new Point2(Center.X + Radius * Math.Cos(rad), Center.Y + Radius * Math.Sin(rad));
        }

        /// <summary>Концы дуги и экстремумы по осям, попадающие в развёртку.</summary>
        public IEnumerable<Point2> ExtremePoints()
        {
            yield return Start;
            yield return End;
            var sweep = SweepDeg;
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                var target = quadrant * 90.0;
                // Нормализуем разность в направлении развёртки.
                var delta = sweep >= 0
                    ? ((target - StartAngleDeg) % 360.0 + 360.0) % 360.0
                    : -(((StartAngleDeg - target) % 360.0 + 360.0) % 360.0);
                if ((sweep >= 0 && delta <= sweep) || (sweep < 0 && delta >= sweep))
                    yield return PointAt(target);
            }
        }
    }

    /// <summary>Замкнутый или разомкнутый контур из отрезков и дуг.</summary>
    public sealed class Path
    {
        private readonly List<IPathSegment> _segments = new List<IPathSegment>();

        /// <summary>Создаёт контур с начальной точкой.</summary>
        public Path(Point2 start) { Start = start; }

        /// <summary>Начальная точка контура.</summary>
        public Point2 Start { get; }

        /// <summary>Сегменты в порядке обхода.</summary>
        public IReadOnlyList<IPathSegment> Segments => _segments;

        /// <summary>Текущая конечная точка.</summary>
        public Point2 Current => _segments.Count == 0 ? Start : _segments[_segments.Count - 1].End;

        /// <summary>Добавляет отрезок до точки.</summary>
        public Path LineTo(Point2 point)
        {
            // CR-09: нулевые отрезки создают вырожденные сегменты в САПР.
            if (Current.DistanceTo(point) < 1e-9) return this;
            _segments.Add(new LineSegment(Current, point));
            return this;
        }

        /// <summary>Добавляет дугу с заданным центром и радиусом.</summary>
        public Path ArcTo(Point2 center, double radius, double startAngleDeg, double sweepDeg)
        {
            if (Math.Abs(sweepDeg) < 1e-9) return this;
            var arc = new ArcSegment(center, radius, startAngleDeg, sweepDeg);
            var gap = Current.DistanceTo(arc.Start);
            if (gap > 1e-6)
                throw new InvalidOperationException("Дуга не состыкована с текущей точкой контура (зазор " + gap + ").");
            _segments.Add(arc);
            return this;
        }

        /// <summary>Замыкает контур отрезком, если нужно.</summary>
        public Path Close()
        {
            if (Current.DistanceTo(Start) > 1e-6) LineTo(Start);
            return this;
        }

        /// <summary>Истина, если контур замкнут с точностью 1e-6.</summary>
        public bool IsClosed => _segments.Count > 0 && Current.DistanceTo(Start) < 1e-6;

        /// <summary>Габарит контура по всем сегментам (для дуг — с учётом экстремумов).</summary>
        public BoundingBox Bounds()
        {
            var points = new List<Point2> { Start };
            foreach (var segment in _segments) points.AddRange(segment.ExtremePoints());
            return new BoundingBox(points.Min(p => p.X), points.Min(p => p.Y),
                points.Max(p => p.X), points.Max(p => p.Y));
        }
    }
}
