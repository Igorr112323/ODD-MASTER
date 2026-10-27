using System;
using System.Collections.Generic;

namespace OddMaster.Core.Geometry
{
    /// <summary>Результат построения контура: либо геометрия, либо понятная причина отказа.</summary>
    public sealed class PrimitiveResult
    {
        private PrimitiveResult(Path? path, string? problem)
        {
            Path = path; Problem = problem;
        }

        /// <summary>Контур, если построение успешно.</summary>
        public Path? Path { get; }
        /// <summary>Причина отказа (скругление не помещается).</summary>
        public string? Problem { get; }
        /// <summary>Истина при успехе.</summary>
        public bool IsSuccess => Path != null;

        /// <summary>Успешный результат.</summary>
        public static PrimitiveResult Ok(Path path) => new PrimitiveResult(path, null);
        /// <summary>Отказ с причиной.</summary>
        public static PrimitiveResult Fail(string problem) => new PrimitiveResult(null, problem);
    }

    /// <summary>Примитивы носителей знаков. Все размеры — миллиметры, центр в (0,0), ось Y вверх.</summary>
    public static class GeometryPrimitives
    {
        /// <summary>Окружность заданного диаметра: четыре дуги по 90° (SVG не умеет полный круг одной дугой).</summary>
        public static Path Circle(double diameter)
        {
            if (diameter <= 0) throw new ArgumentOutOfRangeException(nameof(diameter));
            var r = diameter / 2;
            var path = new Path(new Point2(r, 0));
            for (int i = 0; i < 4; i++) path.ArcTo(new Point2(0, 0), r, i * 90.0, 90.0);
            return path.Close();
        }

        /// <summary>Правильный восьмиугольник; B — расстояние между параллельными сторонами (GOST-005).</summary>
        public static Path RegularOctagon(double acrossFlats)
        {
            if (acrossFlats <= 0) throw new ArgumentOutOfRangeException(nameof(acrossFlats));
            var apothem = acrossFlats / 2;
            var r = apothem / Math.Cos(Math.PI / 8);
            var vertices = new List<Point2>();
            for (int i = 0; i < 8; i++)
            {
                var angle = (-22.5 + i * 45.0) * Math.PI / 180.0;
                vertices.Add(new Point2(r * Math.Cos(angle), r * Math.Sin(angle)));
            }
            return RoundedPolygon(vertices, 0).Path!;
        }

        /// <summary>Равносторонний треугольник со стороной A, скругление r вписано (GOST-003).</summary>
        public static PrimitiveResult Triangle(double side, double cornerRadius, bool up)
        {
            if (side <= 0) throw new ArgumentOutOfRangeException(nameof(side));
            var r = side / Math.Sqrt(3.0);
            var vertices = new List<Point2>();
            var baseAngle = up ? 90.0 : 270.0;
            for (int i = 0; i < 3; i++)
            {
                var angle = (baseAngle + i * 120.0) * Math.PI / 180.0;
                vertices.Add(new Point2(r * Math.Cos(angle), r * Math.Sin(angle)));
            }
            return RoundedPolygon(vertices, cornerRadius);
        }

        /// <summary>Прямоугольник B×H со скруглением углов r (GOST-020).</summary>
        public static PrimitiveResult RoundedRectangle(double width, double height, double cornerRadius)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            var vertices = new List<Point2>
            {
                new Point2(-width / 2, -height / 2),
                new Point2(width / 2, -height / 2),
                new Point2(width / 2, height / 2),
                new Point2(-width / 2, height / 2)
            };
            return RoundedPolygon(vertices, cornerRadius);
        }

        /// <summary>Ромб — квадрат со стороной side, повёрнутый на 45°.</summary>
        public static PrimitiveResult Diamond(double side)
        {
            if (side <= 0) throw new ArgumentOutOfRangeException(nameof(side));
            var half = side / Math.Sqrt(2.0);
            var vertices = new List<Point2>
            {
                new Point2(0, half), new Point2(-half, 0), new Point2(0, -half), new Point2(half, 0)
            };
            return RoundedPolygon(vertices, 0);
        }

        /// <summary>Выпуклый многоугольник со скруглёнными углами. Вершины — против часовой.</summary>
        public static PrimitiveResult RoundedPolygon(IReadOnlyList<Point2> vertices, double radius)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            if (vertices.Count < 3) return PrimitiveResult.Fail("Меньше трёх вершин.");
            if (radius < 0) return PrimitiveResult.Fail("Отрицательный радиус скругления.");
            var n = vertices.Count;
            var corners = new List<Corner>(n);
            for (int i = 0; i < n; i++)
            {
                var v = vertices[i];
                var v1 = (vertices[(i - 1 + n) % n] - v).Normalized();
                var v2 = (vertices[(i + 1) % n] - v).Normalized();
                if (radius <= 1e-9)
                {
                    corners.Add(Corner.Sharp(v));
                    continue;
                }
                var cosTheta = Math.Max(-1.0, Math.Min(1.0, v1.Dot(v2)));
                var theta = Math.Acos(cosTheta);
                var half = theta / 2;
                if (half < 1e-9) return PrimitiveResult.Fail("Вырожденный угол.");
                var d = radius / Math.Tan(half);
                var len1 = (vertices[(i - 1 + n) % n] - v).Length;
                var len2 = (vertices[(i + 1) % n] - v).Length;
                if (d > len1 / 2 || d > len2 / 2)
                    return PrimitiveResult.Fail(
                        $"Скругление r={radius:0.##} мм не помещается в сторону: требуется {d:0.##} мм, доступно {Math.Min(len1, len2) / 2:0.##} мм.");
                var tIn = v + v1 * d;
                var tOut = v + v2 * d;
                var bisector = (v1 + v2).Normalized();
                var center = v + bisector * (radius / Math.Sin(half));
                var a1 = Math.Atan2(tIn.Y - center.Y, tIn.X - center.X) * 180.0 / Math.PI;
                var a2 = Math.Atan2(tOut.Y - center.Y, tOut.X - center.X) * 180.0 / Math.PI;
                var sweep = NormalizeAngle(a2 - a1);
                corners.Add(Corner.Rounded(tIn, tOut, center, radius, a1, sweep));
            }

            var path = new Path(corners[0].TIn);
            for (int i = 0; i < n; i++)
            {
                var corner = corners[i];
                if (corner.HasArc) path.ArcTo(corner.Center, radius, corner.StartAngle, corner.Sweep);
                else path.LineTo(corner.TOut);
                var next = corners[(i + 1) % n];
                path.LineTo(next.TIn);
            }
            return PrimitiveResult.Ok(path.Close());
        }

        private static double NormalizeAngle(double degrees)
        {
            var d = ((degrees % 360.0) + 540.0) % 360.0 - 180.0;
            return d;
        }

        private sealed class Corner
        {
            public Point2 TIn;
            public Point2 TOut;
            public Point2 Center;
            public double Radius;
            public double StartAngle;
            public double Sweep;
            public bool HasArc;

            public static Corner Sharp(Point2 v) => new Corner { TIn = v, TOut = v, HasArc = false };

            public static Corner Rounded(Point2 tIn, Point2 tOut, Point2 center, double radius,
                double startAngle, double sweep) => new Corner
                {
                    TIn = tIn, TOut = tOut, Center = center, Radius = radius,
                    StartAngle = startAngle, Sweep = sweep, HasArc = Math.Abs(sweep) > 1e-9
                };
        }
    }
}
