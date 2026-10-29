using Xunit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OddMaster.Core.Alignment;
using OddMaster.Core.Geometry;
using OddMaster.Core.Norms;
using OddMaster.Core.Scheme;
using OddMaster.TestKit;

namespace OddMaster.Core.Tests
{
    /// <summary>Ось, пикетаж, опоры, дислокация и реактор (M4).</summary>
    public class AlignmentTests
    {
        private static Alignment.Alignment Straight() =>
            new Alignment.Alignment(new[] { new Point2(0, 0), new Point2(1000, 0) });

        private static Alignment.Alignment Polyline() =>
            new Alignment.Alignment(new[]
            {
                new Point2(0, 0), new Point2(300, 0), new Point2(300, 400), new Point2(800, 400)
            });

        [Fact(DisplayName = "M4-A01: формат ПК12+35,40 и перенос разряда 1299,996 → ПК13+00,00")]
        public void StationFormatAndCarry()
        {
            TestAssert.Equal("ПК12+35,40", new Station(1235.40).Format());
            TestAssert.Equal("ПК13+00,00", new Station(1299.996).Format());
            TestAssert.Equal("ПК0+00,00", new Station(0).Format());
            TestAssert.Equal("ПК1+00,00", new Station(99.996).Format());
        }

        [Fact(DisplayName = "M4-A02: разбор и форматирование туда и обратно")]
        public void StationRoundTrip()
        {
            foreach (var value in new[] { 0.0, 35.4, 1235.4, 999.99, 1300.0 })
            {
                var text = new Station(value).Format();
                var parsed = Station.Parse(text);
                TestAssert.Close(value, parsed.Meters, 0.005, text);
            }
            TestAssert.Throws<FormatException>(() => Station.Parse("12+35,40"));
            TestAssert.Throws<FormatException>(() => Station.Parse("ПК12+100,00"));
        }

        [Fact(DisplayName = "M4-A03: настраиваемый формат (пробел и точка)")]
        public void CustomStationFormat()
        {
            var format = new StationFormat("ПК", " ", "+", ".", 2);
            TestAssert.Equal("ПК 12+35.40", new Station(1235.4).Format(format));
            TestAssert.Close(1235.4, Station.Parse("ПК 12+35.40", format).Meters, 0.001);
        }

        [Fact(DisplayName = "M4-A04: PointAt(Project(p)) ≈ p на прямой и ломаной")]
        public void ProjectPointAtRoundTrip()
        {
            foreach (var axis in new[] { Straight(), Polyline() })
            {
                foreach (var p in new[]
                {
                    new Point2(50, 10), new Point2(0, 0), new Point2(300, 200),
                    new Point2(500, 420), new Point2(800, 400)
                })
                {
                    var projection = axis.Project(p);
                    var back = axis.PointAt(projection.Station, projection.Offset);
                    TestAssert.Close(p.X, back.Point.X, 1e-6, "X " + p);
                    TestAssert.Close(p.Y, back.Point.Y, 1e-6, "Y " + p);
                }
            }
        }

        [Fact(DisplayName = "M4-A05: проекция за концами оси помечается Outside")]
        public void ProjectionBeyondEnds()
        {
            var axis = Straight();
            TestAssert.True(axis.Project(new Point2(-50, 0)).Outside);
            TestAssert.True(axis.Project(new Point2(1500, 0)).Outside);
            TestAssert.False(axis.Project(new Point2(500, 0)).Outside);
            TestAssert.True(axis.PointAt(1200).Outside);
            TestAssert.False(axis.PointAt(500).Outside);
        }

        [Fact(DisplayName = "M4-A06: сдвиг оси на (50;0) меняет X, но не пикеты")]
        public void ShiftKeepsStations()
        {
            var before = Polyline();
            var after = before.Shift(new Vector2(50, 0));
            var p1 = new Point2(300, 100);
            var a = before.Project(p1);
            var b = after.Project(p1 + new Vector2(50, 0));
            TestAssert.Close(a.Station, b.Station, 1e-9);
            var pointA = before.PointAt(250, 0);
            var pointB = after.PointAt(250, 0);
            TestAssert.Close(pointA.Point.X + 50, pointB.Point.X, 1e-9);
            TestAssert.Close(pointA.Point.Y, pointB.Point.Y, 1e-9);
        }

        [Fact(DisplayName = "M4-A07: знак смещения: справа по ходу = «+» (ADR-0015)")]
        public void OffsetSignRule()
        {
            var axis = Straight();
            var right = axis.PointAt(500, 5);
            TestAssert.Close(-5, right.Point.Y, 1e-9);
            var projection = axis.Project(new Point2(500, -5));
            TestAssert.Close(5, projection.Offset, 1e-9);
        }

        [Fact(DisplayName = "M4-A08: сегмент нулевой длины → находка валидатора")]
        public void ZeroLengthSegmentIsFinding()
        {
            var axis = new Alignment.Alignment(new[] { new Point2(0, 0), new Point2(0, 0), new Point2(100, 0) });
            var findings = axis.Validate();
            TestAssert.True(findings.Count > 0);
            TestAssert.Contains("нулевой длины", string.Join(" ", findings.Select(f => f.Message)));
        }

        [Fact(DisplayName = "M4-A09: Post.Mounts — упорядоченный список сверху вниз")]
        public void PostMountsOrder()
        {
            var post = new Post("P1", 100, 3);
            post.Add(new SignInstance("s1", SignNumber.Parse("3.24"), "II", "speed", 40m));
            post.Add(new SignInstance("s2", SignNumber.Parse("8.2.1"), "II", "distance", 50m));
            TestAssert.Equal("3.24 (40), 8.2.1 (50)", post.SignsTopDown());
            TestAssert.Equal("справа", post.Side);
            TestAssert.True(post.Mounts[1].IsPlate);
        }

        [Fact(DisplayName = "M4-A10: журнал координат: BOM, «;», запятая в дроби, детерминизм")]
        public void CoordinateLogCsv()
        {
            var scheme = BuildScheme();
            var log = SchemeReactor.Compute(scheme);
            var csv = log.ToCsv();
            TestAssert.True(csv[0] == '\uFEFF', "нет BOM");
            TestAssert.Contains(";", csv);
            TestAssert.Contains("Опора;Пикет;Сторона", csv);
            TestAssert.Contains("5,00", csv);
            TestAssert.Contains("\r\n", csv);
            TestAssert.Equal(log.ToCsv(), SchemeReactor.Compute(BuildScheme()).ToCsv());
        }

        private static SchemeModel BuildScheme()
        {
            var post = new Post("P1", 250, 5);
            post.Add(new SignInstance("s1", SignNumber.Parse("3.24"), "II", "speed", 40m));
            return new SchemeModel("demo", Polyline(), new[] { post });
        }

        [Fact(DisplayName = "M4-A11: diff журнала при сдвиге показывает X +50")]
        public void ReactorDiff()
        {
            var before = BuildScheme();
            var after = new SchemeModel("demo", before.Alignment.Shift(new Vector2(50, 0)), before.Posts);
            var diff = SchemeReactor.Diff(before, after);
            TestAssert.Contains("X", diff);
            TestAssert.Contains("50", diff);
            TestAssert.NotContains("пикет", diff);
        }

        [Fact(DisplayName = "M4-A12: смена начального пикета меняет подпись, но не координаты")]
        public void InitialStationChange()
        {
            var before = BuildScheme();
            // Physical point remains the same: station 250 becomes station 1250 with initial station 1000.
            var shiftedPost = new Post("P1", 1250, 5);
            shiftedPost.Add(new SignInstance("s1", SignNumber.Parse("3.24"), "II", "speed", 40m));
            var after = new SchemeModel("demo", before.Alignment.WithInitialStation(1000), new[] { shiftedPost });
            var logBefore = SchemeReactor.Compute(before);
            var logAfter = SchemeReactor.Compute(after);
            TestAssert.Close(logBefore.Rows[0].X, logAfter.Rows[0].X, 1e-9);
            TestAssert.Contains("пикет", logBefore.Diff(logAfter));
        }

        [Fact(DisplayName = "M4-A13: поворот оси меняет координаты, сохраняя пикеты")]
        public void RotateAxis()
        {
            var axis = Straight();
            var rotated = axis.Rotate(90, new Point2(0, 0));
            var p = rotated.PointAt(500, 0);
            TestAssert.Close(0, p.Point.X, 1e-9);
            TestAssert.Close(500, p.Point.Y, 1e-9);
            TestAssert.Close(500, rotated.Project(new Point2(0, 500)).Station, 1e-9);
        }

        [Fact(DisplayName = "M4-A14: 200 случайных точек с фиксированным seed: проекция обратима")]
        public void RandomizedProjection()
        {
            var axis = Polyline();
            var random = new Random(20261027);
            for (int i = 0; i < 200; i++)
            {
                var station = random.NextDouble() * axis.Length;
                var offset = (random.NextDouble() - 0.5) * 100;
                var point = axis.PointAt(station, offset).Point;
                var projection = axis.Project(point);
                var reconstructed = axis.PointAt(projection.Station, projection.Offset).Point;
                // Возле вершин ломаной ближайший сегмент может смениться, поэтому
                // проверяем обратимость координаты, а не равенство пикета/смещения.
                TestAssert.Close(point.X, reconstructed.X, 1e-6, "X итерации " + i);
                TestAssert.Close(point.Y, reconstructed.Y, 1e-6, "Y итерации " + i);
            }
        }

        [Fact(DisplayName = "M4-A15: форматирование пикета и CSV не зависит от текущей культуры")]
        public void CultureInvariantStationAndCsv()
        {
            var saved = CultureInfo.CurrentCulture;
            try
            {
                var results = new List<string>();
                foreach (var name in new[] { "ru-RU", "en-US" })
                {
                    CultureInfo.CurrentCulture = new CultureInfo(name);
                    results.Add(new Station(1235.4).Format() + "|" + SchemeReactor.Compute(BuildScheme()).ToCsv());
                }
                TestAssert.Equal(results[0], results[1]);
            }
            finally { CultureInfo.CurrentCulture = saved; }
        }
    }
}
