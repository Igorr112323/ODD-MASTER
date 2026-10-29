using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using OddMaster.Core.Composition;
using OddMaster.Core.Config;
using OddMaster.Core.Geometry;
using OddMaster.TestKit;

namespace OddMaster.Core.Tests
{
    /// <summary>
    /// Отсечение состава выпуклым контуром знака (CR-22, продолжение): диагональная полоса
    /// «во всё поле» не должна выходить за ромб, круг или треугольник — иначе на превью
    /// остаются «уши» за контуром, а в DXF — линии вне знака.
    /// </summary>
    public class PolygonClipTests
    {
        private static readonly IReadOnlyList<Point2> Square = new[]
        {
            new Point2(-10, -10), new Point2(10, -10), new Point2(10, 10), new Point2(-10, 10),
        };

        [Fact(DisplayName = "M17-C01: отсечение внутри контура не меняет многоугольник")]
        public void InsideIsUnchanged()
        {
            var subject = new[] { new Point2(-2, -2), new Point2(2, -2), new Point2(2, 2), new Point2(-2, 2) };
            var clipped = PolygonClip.Clip(subject, Square);
            TestAssert.Equal(4, clipped.Count);
            TestAssert.True(Math.Abs(PolygonClip.SignedArea(clipped) - 16.0) < 1e-9,
                "площадь должна сохраниться: " + PolygonClip.SignedArea(clipped));
        }

        [Fact(DisplayName = "M17-C02: полоса шире квадрата обрезается по его границе")]
        public void OversizedIsClipped()
        {
            var subject = new[]
            {
                new Point2(-100, -2), new Point2(100, -2), new Point2(100, 2), new Point2(-100, 2),
            };
            var clipped = PolygonClip.Clip(subject, Square);
            TestAssert.True(Math.Abs(PolygonClip.SignedArea(clipped) - 80.0) < 1e-9,
                "площадь обрезки: " + PolygonClip.SignedArea(clipped));
            TestAssert.True(clipped.All(p => Math.Abs(p.X) <= 10 + 1e-9), "вершины внутри квадрата по X");
        }

        [Fact(DisplayName = "M17-C03: выпуклый контур круглого знака даёт вершины не дальше радиуса")]
        public void CircleKeepsVerticesInside()
        {
            var outline = new Path(new Point2(150, 0))
                .ArcTo(new Point2(0, 0), 150, 0, 180)
                .ArcTo(new Point2(0, 0), 150, 180, 180)
                .Close();
            var square = new[] { new Point2(-150, -150), new Point2(150, -150), new Point2(150, 150),
                new Point2(-150, 150) };
            var clipped = PolygonClip.Clip(square, Flatten(outline));
            TestAssert.True(clipped.Count >= 3, "обрезка квадрата кругом должна остаться многоугольником");
            var maxDistance = clipped.Max(p => Math.Sqrt(p.X * p.X + p.Y * p.Y));
            TestAssert.True(maxDistance <= 150.5,
                "вершины не дальше радиуса с учётом разбиения дуги: " + maxDistance);
        }

        [Fact(DisplayName = "M17-C04: без пересечения — пустой результат, а не мусор")]
        public void DisjointIsEmpty()
        {
            var subject = new[] { new Point2(50, 50), new Point2(60, 50), new Point2(60, 60) };
            TestAssert.Equal(0, PolygonClip.Clip(subject, Square).Count);
            TestAssert.Equal(0, PolygonClip.Clip(new[] { new Point2(0, 0) }, Square).Count);
        }

        [Fact(DisplayName = "M17-C05: библиотека элементов грузится, полосы в круге не выходят за контур")]
        public void DiagonalBandsStayInsideCircle()
        {
            var set = ConfigLoader.Load(Repo.Root());
            var band = set.Elements.Find("bandSlash");
            var bundle = set.Elements.Find("bundleSlashRed");
            TestAssert.True(band != null && bundle != null, "в elements.json есть bandSlash и bundleSlashRed");
            var outline = new Path(new Point2(150, 0))
                .ArcTo(new Point2(0, 0), 150, 0, 180)
                .ArcTo(new Point2(0, 0), 150, 180, 180)
                .Close();
            var field = outline.Bounds();
            var layers = CompositionBuilder.Build(new[] { bundle! }, field, outline);
            TestAssert.Equal(bundle!.Count, layers.Count);
            foreach (var layer in layers)
            {
                var bounds = layer.Geometry!.Bounds();
                TestAssert.True(bounds.Width <= field.Width + 1e-6 && bounds.Height <= field.Height + 1e-6,
                    "полоса состава выходит за знак: " + bounds.Width.ToString("0.###", Culture) + " × " +
                    bounds.Height.ToString("0.###", Culture));
                var corners = new[]
                {
                    new Point2(bounds.MinX, bounds.MinY), new Point2(bounds.MaxX, bounds.MinY),
                    new Point2(bounds.MaxX, bounds.MaxY), new Point2(bounds.MinX, bounds.MaxY),
                };
                foreach (var corner in corners)
                {
                    var radius = Math.Sqrt(corner.X * corner.X + corner.Y * corner.Y);
                    TestAssert.True(radius <= 151.0,
                        "угол обрезки дальше радиуса: " + radius.ToString("0.###", Culture));
                }
            }
        }

        private static System.Globalization.CultureInfo Culture =>
            System.Globalization.CultureInfo.InvariantCulture;

        /// <summary>Разбиение контура в ломаную — та же логика, что в составном построении.</summary>
        private static IReadOnlyList<Point2> Flatten(Path path)
        {
            var points = new List<Point2> { path.Start };
            foreach (var segment in path.Segments)
            {
                if (segment is ArcSegment arc)
                {
                    var steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(arc.SweepDeg) / 15.0));
                    for (var i = 1; i <= steps; i++)
                    {
                        var angle = (arc.StartAngleDeg + arc.SweepDeg * i / steps) * Math.PI / 180.0;
                        points.Add(new Point2(arc.Center.X + arc.Radius * Math.Cos(angle),
                            arc.Center.Y + arc.Radius * Math.Sin(angle)));
                    }
                }
                else
                {
                    points.Add(segment.End);
                }
            }
            if (points.Count > 1 && points[0].DistanceTo(points[points.Count - 1]) < 1e-9)
                points.RemoveAt(points.Count - 1);
            return points;
        }
    }
}
