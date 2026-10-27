using System;
using System.Collections.Generic;
using System.Globalization;
using OddMaster.Core.Geometry;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Alignment
{
    /// <summary>Результат вычисления точки на оси: координата и признак выхода за границы.</summary>
    public sealed class AlignmentPoint
    {
        /// <summary>Создаёт результат.</summary>
        public AlignmentPoint(Point2 point, double station, double offset, int segmentIndex, bool outside)
        {
            Point = point; Station = station; Offset = offset; SegmentIndex = segmentIndex; Outside = outside;
        }

        /// <summary>Мировая координата, м.</summary>
        public Point2 Point { get; }
        /// <summary>Пикет, м.</summary>
        public double Station { get; }
        /// <summary>Смещение (справа «+»), м.</summary>
        public double Offset { get; }
        /// <summary>Индекс сегмента.</summary>
        public int SegmentIndex { get; }
        /// <summary>Истина, если точка за пределами оси.</summary>
        public bool Outside { get; }
    }

    /// <summary>Результат проекции точки на ось.</summary>
    public sealed class Projection
    {
        /// <summary>Создаёт результат.</summary>
        public Projection(double station, double offset, int segmentIndex, double distance, bool outside)
        {
            Station = station; Offset = offset; SegmentIndex = segmentIndex; Distance = distance; Outside = outside;
        }

        /// <summary>Пикет проекции, м.</summary>
        public double Station { get; }
        /// <summary>Смещение от оси (справа «+»), м.</summary>
        public double Offset { get; }
        /// <summary>Индекс ближайшего сегмента.</summary>
        public int SegmentIndex { get; }
        /// <summary>Расстояние до оси, м.</summary>
        public double Distance { get; }
        /// <summary>Истина, если проекция за концами оси.</summary>
        public bool Outside { get; }
    }

    /// <summary>Ось дороги — полилиния с начальным пикетом. Кривые — позже.</summary>
    public sealed class Alignment
    {
        private readonly List<Point2> _vertices;

        /// <summary>Создаёт ось. Начальный пикет задаётся в метрах.</summary>
        public Alignment(IEnumerable<Point2> vertices, double initialStationMeters = 0.0)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            _vertices = new List<Point2>(vertices);
            InitialStationMeters = initialStationMeters;
        }

        /// <summary>Вершины полилинии.</summary>
        public IReadOnlyList<Point2> Vertices => _vertices;

        /// <summary>Начальный пикет, м.</summary>
        public double InitialStationMeters { get; }

        /// <summary>Длина оси, м.</summary>
        public double Length
        {
            get
            {
                double total = 0;
                for (int i = 0; i < _vertices.Count - 1; i++)
                    total += (_vertices[i + 1] - _vertices[i]).Length;
                return total;
            }
        }

        /// <summary>Проверяет ось: меньше двух вершин, сегмент нулевой длины.</summary>
        public List<Finding> Validate()
        {
            var findings = new List<Finding>();
            if (_vertices.Count < 2)
                findings.Add(Finding.Error("A-001", "Ось должна содержать минимум две вершины."));
            for (int i = 0; i < _vertices.Count - 1; i++)
            {
                var len = (_vertices[i + 1] - _vertices[i]).Length;
                if (len < 1e-9)
                    findings.Add(Finding.Warning("A-002",
                        "Сегмент оси нулевой длины.", "сегмент " + i.ToString(CultureInfo.InvariantCulture)));
            }
            return findings;
        }

        /// <summary>Координата точки на оси с заданным пикетом и смещением (справа «+»).</summary>
        public AlignmentPoint PointAt(double station, double offset = 0.0)
        {
            if (_vertices.Count < 2) throw new InvalidOperationException("Ось содержит меньше двух вершин.");
            var distance = station - InitialStationMeters;
            double accumulated = 0;
            for (int i = 0; i < _vertices.Count - 1; i++)
            {
                var a = _vertices[i];
                var b = _vertices[i + 1];
                var segment = b - a;
                var len = segment.Length;
                if (len < 1e-9) continue;
                var dir = segment / len;
                if (distance <= accumulated + len || i == _vertices.Count - 2)
                {
                    var local = distance - accumulated;
                    var basePoint = a + dir * local;
                    var point = basePoint + dir.RightNormal() * offset;
                    var outside = distance < -1e-9 || distance > accumulated + len + 1e-9;
                    return new AlignmentPoint(point, station, offset, i, outside);
                }
                accumulated += len;
            }
            throw new InvalidOperationException("Не удалось вычислить точку на оси.");
        }

        /// <summary>Проекция точки на ось: пикет, смещение, сегмент, расстояние, признак «за пределами».</summary>
        public Projection Project(Point2 point)
        {
            if (_vertices.Count < 2) throw new InvalidOperationException("Ось содержит меньше двух вершин.");
            double bestDistance = double.MaxValue;
            double bestStation = InitialStationMeters;
            double bestOffset = 0;
            int bestSegment = 0;
            bool bestOutside = false;
            double accumulated = 0;
            for (int i = 0; i < _vertices.Count - 1; i++)
            {
                var a = _vertices[i];
                var b = _vertices[i + 1];
                var segment = b - a;
                var len = segment.Length;
                if (len < 1e-9) continue;
                var dir = segment / len;
                var t = (point - a).Dot(dir) / len;
                var clamped = Math.Max(0.0, Math.Min(1.0, t));
                var closest = a + dir * len * clamped;
                var distance = point.DistanceTo(closest);
                if (distance < bestDistance - 1e-12)
                {
                    bestDistance = distance;
                    bestStation = InitialStationMeters + accumulated + clamped * len;
                    bestOffset = (point - closest).Dot(dir.RightNormal());
                    bestSegment = i;
                    bestOutside = t < -1e-9 || t > 1 + 1e-9;
                }
                accumulated += len;
            }
            return new Projection(bestStation, bestOffset, bestSegment, bestDistance, bestOutside);
        }

        /// <summary>Сдвигает ось — «реактор» в ядре. Пикеты не меняются.</summary>
        public Alignment Shift(Vector2 delta)
        {
            var vertices = new List<Point2>(_vertices.Count);
            foreach (var v in _vertices) vertices.Add(v + delta);
            return new Alignment(vertices, InitialStationMeters);
        }

        /// <summary>Поворачивает ось вокруг точки (угол в градусах, против часовой).</summary>
        public Alignment Rotate(double degrees, Point2 center)
        {
            var radians = degrees * Math.PI / 180.0;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);
            var vertices = new List<Point2>(_vertices.Count);
            foreach (var v in _vertices)
            {
                var dx = v.X - center.X;
                var dy = v.Y - center.Y;
                vertices.Add(new Point2(center.X + dx * cos - dy * sin, center.Y + dx * sin + dy * cos));
            }
            return new Alignment(vertices, InitialStationMeters);
        }

        /// <summary>Меняет начальный пикет — пересчёт журнала координат.</summary>
        public Alignment WithInitialStation(double meters) => new Alignment(_vertices, meters);

        /// <summary>Заменяет геометрию оси (реактор на изменение полилинии).</summary>
        public Alignment WithVertices(IEnumerable<Point2> vertices) => new Alignment(vertices, InitialStationMeters);
    }
}
