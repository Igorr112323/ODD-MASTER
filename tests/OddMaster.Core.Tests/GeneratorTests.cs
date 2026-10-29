using Xunit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OddMaster.Core.Catalog;
using OddMaster.Core.Config;
using OddMaster.Core.Generation;
using OddMaster.Core.Geometry;
using OddMaster.Core.Norms;
using OddMaster.TestKit;

namespace OddMaster.Core.Tests
{
    /// <summary>Инварианты параметрического генератора носителей (M2).</summary>
    public class GeneratorTests
    {
        private static SignGenerator Generator() => new SignGenerator(ConfigLoader.Load(Repo.Root()));

        private static Dictionary<string, decimal> Speed(decimal value) =>
            new Dictionary<string, decimal> { ["speed"] = value };

        [Fact(DisplayName = "M2-G01: POC-знаки генерируются во всех предусмотренных типоразмерах")]
        public void AllPocSignsGenerate()
        {
            var cfg = ConfigLoader.Load(Repo.Root());
            var gen = new SignGenerator(cfg);
            int checkedCount = 0;
            foreach (var sign in cfg.Catalog.Signs)
            {
                var classes = AvailableClasses(cfg.MainProfile, sign.Number);
                TestAssert.True(classes.Count > 0, "нет типоразмеров для " + sign.Number);
                foreach (var sizeClass in classes)
                {
                    var parameters = DemoParams(sign);
                    var result = gen.Generate(sign, sizeClass, parameters);
                    TestAssert.True(result.IsSuccess,
                        $"{sign.Number}/{sizeClass}: {string.Join("; ", result.Findings.Select(f => f.Format()))}");
                    TestAssert.True(result.Geometry!.Outline.IsClosed, sign.Number + ": контур не замкнут");
                    TestAssert.True(result.Geometry.Layers.Count >= 2, sign.Number + ": мало слоёв");
                    TestAssert.True(result.IsDraft, sign.Number + ": ожидался ЧЕРНОВИК");
                    checkedCount++;
                }
            }
            TestAssert.True(checkedCount >= 12, "проверено слишком мало комбинаций: " + checkedCount);
        }

        /// <summary>Произвольные демо-значения параметров, подходящие под табл. Д.1.</summary>
        private static Dictionary<string, decimal>? DemoParams(SignDefinition sign)
        {
            if (sign.ParameterKind == null) return null;
            var map = new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                ["speed"] = 40m, ["distance"] = 50m, ["mass"] = 8m, ["timeMinutes"] = 30m
            };
            return new Dictionary<string, decimal>
            {
                [sign.ParameterKind] = map.TryGetValue(sign.ParameterKind, out var v) ? v : 1m
            };
        }

        private static List<string> AvailableClasses(SizeProfile profile, SignNumber number)
        {
            var result = new List<string>();
            foreach (var table in profile.Tables)
                foreach (var row in table.Rows)
                {
                    if (!row.Signs.Contains(number)) continue;
                    foreach (var key in row.Sizes.Keys.OrderBy(x => x, StringComparer.Ordinal))
                        if (row.Sizes[key].Values.Count > 0 && !result.Contains(key)) result.Add(key);
                }
            return result;
        }

        [Fact(DisplayName = "M2-G02: габариты совпадают с данными прил. Д (0,01 мм)")]
        public void DimensionsMatchTables()
        {
            var gen = Generator();
            var cases = new (string Number, string Class, double W, double H)[]
            {
                ("2.4", "I", 700, 700 * Math.Sqrt(3) / 2),
                ("2.4", "II", 900, 900 * Math.Sqrt(3) / 2),
                ("2.5", "II", 700, 700),
                ("3.24", "II", 700, 700),
                ("3.25", "III", 900, 900),
                ("6.4", "II", 700, 700),
                ("8.2.1", "II", 700, 350)
            };
            foreach (var c in cases)
            {
                var p = c.Number == "3.24" || c.Number == "3.25" ? Speed(40m)
                    : c.Number == "8.2.1" ? new Dictionary<string, decimal> { ["distance"] = 50m } : null;
                var result = gen.Generate(c.Number, c.Class, p);
                TestAssert.True(result.IsSuccess, c.Number + "/" + c.Class + ": " +
                    string.Join("; ", result.Findings.Select(f => f.Format())));
                TestAssert.Close(c.W, result.Geometry!.WidthMm, 0.01, c.Number + " ширина");
                TestAssert.Close(c.H, result.Geometry.HeightMm, 0.01, c.Number + " высота");
            }
        }

        [Fact(DisplayName = "M2-G03: ширина наружной каймы 10/20 мм приходит из borders.json")]
        public void BorderWidthFromConfig()
        {
            var gen = Generator();
            var expected = new Dictionary<string, double>
            {
                ["2.4"] = 10, ["2.5"] = 20, ["3.24"] = 10, ["3.25"] = 10, ["6.4"] = 20, ["8.2.1"] = 10
            };
            foreach (var pair in expected)
            {
                var p = pair.Key == "3.24" || pair.Key == "3.25" ? Speed(40m)
                    : pair.Key == "8.2.1" ? new Dictionary<string, decimal> { ["distance"] = 50m } : null;
                var result = gen.Generate(pair.Key, "II", p);
                TestAssert.True(result.IsSuccess, pair.Key);
                TestAssert.Equal(pair.Value, result.Geometry!.OuterBorderWidthMm);
            }
        }

        [Fact(DisplayName = "M2-G04: 2.5 в типоразмере I → находка со ссылкой на Д.3")]
        public void MissingSizeClassGivesFinding()
        {
            var result = Generator().Generate("2.5", "I");
            TestAssert.False(result.IsSuccess);
            TestAssert.Contains("Д.3", string.Join(" ", result.Findings.Select(f => f.Format())));
        }

        [Fact(DisplayName = "M2-G05: 3.24 со скоростью 45 → находка, 40 → геометрия")]
        public void InvalidSpeedGivesFinding()
        {
            var gen = Generator();
            var bad = gen.Generate("3.24", "II", Speed(45m));
            TestAssert.False(bad.IsSuccess);
            TestAssert.Contains("R006", string.Join(" ", bad.Findings.Select(f => f.Format())));
            var good = gen.Generate("3.24", "II", Speed(40m));
            TestAssert.True(good.IsSuccess);
            TestAssert.Equal("40", good.Geometry!.ParameterValue);
        }

        [Fact(DisplayName = "M2-G06: индивидуальное проектирование возвращает находку, а не геометрию")]
        public void IndividualDesignIsFinding()
        {
            var cfg = ConfigLoader.Load(Repo.Root());
            var fake = new SignDefinition(SignNumber.Parse("6.14.1"), "Индивидуальный", 6,
                CarrierShape.RectangleHorizontal, "Д.8", false,
                new Dictionary<string, string>(), null, null, null, null,
                new NormRef("ГОСТ Р 52290-2024", "разд. 4"), Verification.CuratorExtracted, new List<string>());
            var result = new SignGenerator(cfg).Generate(fake, "II");
            TestAssert.False(result.IsSuccess);
            TestAssert.Contains("индивидуального проектирования", string.Join(" ", result.Findings.Select(f => f.Message)));
        }

        [Fact(DisplayName = "M2-G07: нижний треугольник — зеркальное отражение верхнего")]
        public void TriangleDownIsMirror()
        {
            var up = GeometryPrimitives.Triangle(900, 45, true);
            var down = GeometryPrimitives.Triangle(900, 45, false);
            TestAssert.True(up.IsSuccess && down.IsSuccess);
            var upPoints = SamplePoints(up.Path!);
            var mirrored = upPoints.Select(p => new Point2(p.X, -p.Y));
            var downPoints = SamplePoints(down.Path!);
            // Зеркалирование меняет направление обхода, поэтому сравниваем как множества точек.
            var expected = mirrored.ToList();
            foreach (var point in expected)
            {
                var found = downPoints.Any(p => Math.Abs(p.X - point.X) < 1e-6 && Math.Abs(p.Y - point.Y) < 1e-6);
                TestAssert.True(found, "нет зеркальной точки " + point);
            }
            TestAssert.Equal(expected.Count, downPoints.Count);
        }

        private static List<Point2> SamplePoints(Path path)
        {
            var points = new List<Point2> { path.Start };
            foreach (var segment in path.Segments)
            {
                if (segment is ArcSegment arc)
                {
                    for (int k = 0; k <= 8; k++)
                    {
                        var a = (arc.StartAngleDeg + arc.SweepDeg * k / 8.0) * Math.PI / 180.0;
                        points.Add(new Point2(arc.Center.X + arc.Radius * Math.Cos(a),
                            arc.Center.Y + arc.Radius * Math.Sin(a)));
                    }
                }
                else points.Add(segment.End);
            }
            return points;
        }

        [Fact(DisplayName = "M2-G08: неизвестная внутренняя кайма → заглушка, GOST-001 и ЧЕРНОВИК")]
        public void InnerBorderIsPlaceholder()
        {
            // Внутренняя кайма есть только у 2.4 (CR-04); у 3.24 её нет.
            var result = Generator().Generate("2.4", "II");
            TestAssert.True(result.IsSuccess);
            TestAssert.True(result.Geometry!.InnerBorderIsPlaceholder);
            TestAssert.Contains("GOST-001", string.Join(" ", result.Findings.Select(f => f.Format())));
            // CR-22 §3: ширина в мм неизвестна, но есть замеренная пропорция (Unverified),
            // поэтому статус — Unverified, а не Placeholder; маркер GOST-001 обязателен.
            TestAssert.Contains("Unverified", result.Geometry.Verification.Describe());
            var noInner = Generator().Generate("3.24", "II", Speed(40m));
            TestAssert.True(noInner.IsSuccess);
            TestAssert.False(noInner.Geometry!.InnerBorderIsPlaceholder);
        }

        [Fact(DisplayName = "M2-G09: два прогона дают одинаковую геометрию")]
        public void GenerationIsDeterministic()
        {
            var gen = Generator();
            var a = gen.Generate("3.24", "II", Speed(40m));
            var b = gen.Generate("3.24", "II", Speed(40m));
            TestAssert.Equal(Describe(a.Geometry!), Describe(b.Geometry!));
        }

        private static string Describe(SignGeometry g)
        {
            var parts = new List<string>
            {
                g.WidthMm.ToString("R", CultureInfo.InvariantCulture),
                g.HeightMm.ToString("R", CultureInfo.InvariantCulture),
                g.OuterBorderWidthMm.ToString("R", CultureInfo.InvariantCulture),
                g.Verification.Describe()
            };
            parts.AddRange(g.Outline.Segments.Select(s =>
                s.Start + "->" + s.End + " " + (s is ArcSegment arc ? "A" + arc.SweepDeg.ToString("R", CultureInfo.InvariantCulture) : "L")));
            return string.Join("|", parts);
        }

        [Fact(DisplayName = "M2-G10: скругление, которое не помещается, даёт находку-причину")]
        public void TooLargeRadiusFails()
        {
            var result = GeometryPrimitives.RoundedRectangle(100, 100, 80);
            TestAssert.False(result.IsSuccess);
            TestAssert.Contains("не помещается", result.Problem ?? "");
        }

        [Fact(DisplayName = "M2-G11: габарит окружности по дугам равен диаметру")]
        public void CircleBoundsAreExact()
        {
            var path = GeometryPrimitives.Circle(700);
            var bounds = path.Bounds();
            TestAssert.Close(700, bounds.Width, 0.0001);
            TestAssert.Close(700, bounds.Height, 0.0001);
            TestAssert.True(path.IsClosed);
        }

        [Fact(DisplayName = "M2-G12: восьмиугольник: B — расстояние между параллельными сторонами")]
        public void OctagonAcrossFlats()
        {
            var path = GeometryPrimitives.RegularOctagon(700);
            var bounds = path.Bounds();
            TestAssert.Close(700, bounds.Width, 0.01);
            TestAssert.Close(700, bounds.Height, 0.01);
        }

        [Fact(DisplayName = "M2-G13: в исходниках Core нет Multicad/Teigha/Avalonia/WinForms/WPF/Render")]
        public void ArchitectureNoCadReferences()
        {
            var root = Repo.Root();
            var files = System.IO.Directory.GetFiles(System.IO.Path.Combine(root, "src/OddMaster.Core"),
                "*.cs", System.IO.SearchOption.AllDirectories)
                .Where(f => !IsIgnored(f) && !IsIgnored(f));
            var forbidden = new[] { "Multicad", "Teigha", "Avalonia", "System.Windows", "OddMaster.Render" };
            foreach (var file in files)
            {
                var text = System.IO.File.ReadAllText(file);
                foreach (var word in forbidden)
                    TestAssert.NotContains(word, text, System.IO.Path.GetFileName(file));
            }
            var csproj = Repo.ReadText("src/OddMaster.Core/OddMaster.Core.csproj");
            foreach (var word in forbidden) TestAssert.NotContains(word, csproj);
        }

        /// <summary>Кроссплатформенная проверка служебных каталогов (Windows использует \).</summary>
        private static bool IsIgnored(string path)
        {
            var p = path.Replace('\\', '/');
            return p.Contains("/obj/", StringComparison.Ordinal)
                || p.Contains("/bin/", StringComparison.Ordinal)
                || p.Contains("/.git/", StringComparison.Ordinal);
        }
    }
}
