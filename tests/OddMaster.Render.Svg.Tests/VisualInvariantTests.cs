using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OddMaster.Core.Catalog;
using OddMaster.Core.Config;
using OddMaster.Core.Generation;
using OddMaster.Core.Geometry;
using OddMaster.Render.Svg;
using OddMaster.TestKit;
using Xunit;
using IoPath = System.IO.Path;

namespace OddMaster.Render.Svg.Tests
{
    /// <summary>Геометрические инварианты §9.3 (CR-10). Проверяются на всех сгенерированных знаках.</summary>
    [Collection("Demo artifacts")]
    public class VisualInvariantTests
    {
        private static NormativeConfigSet Config() => ConfigLoader.Load(Repo.Root());

        private static List<SignGeometry> AllSigns()
        {
            var cfg = Config();
            var gen = new SignGenerator(cfg);
            var result = new List<SignGeometry>();
            foreach (var sign in cfg.Catalog.Signs)
                foreach (var sizeClass in Classes(cfg.MainProfile, sign.Number))
                {
                    var parameters = OddMaster.Demo.DemoRunner.DefaultParameters(sign);
                    var generated = gen.Generate(sign, sizeClass, parameters);
                    if (generated.IsSuccess) result.Add(generated.Geometry!);
                }
            return result;
        }

        private static List<string> Classes(SizeProfile profile, OddMaster.Core.Norms.SignNumber number)
        {
            var list = new List<string>();
            foreach (var table in profile.Tables)
                foreach (var row in table.Rows)
                {
                    if (!row.Signs.Contains(number)) continue;
                    foreach (var key in row.Sizes.Keys.OrderBy(x => x, StringComparer.Ordinal))
                        if (row.Sizes[key].Values.Count > 0 && !list.Contains(key)) list.Add(key);
                }
            return list;
        }

        [Fact(DisplayName = "M9-V01: вся геометрия внутри габарита носителя")]
        public void GeometryInsideBounds()
        {
            foreach (var g in AllSigns())
            {
                foreach (var layer in g.Layers)
                {
                    if (layer.Geometry == null) continue;
                    var box = layer.Geometry.Bounds();
                    TestAssert.True(box.MinX >= g.Bounds.MinX - 0.02,
                        g.Sign.Number + "/" + g.SizeClass + " " + layer.Kind + " minX");
                    TestAssert.True(box.MaxX <= g.Bounds.MaxX + 0.02,
                        g.Sign.Number + "/" + g.SizeClass + " " + layer.Kind + " maxX");
                    TestAssert.True(box.MinY >= g.Bounds.MinY - 0.02,
                        g.Sign.Number + "/" + g.SizeClass + " " + layer.Kind + " minY");
                    TestAssert.True(box.MaxY <= g.Bounds.MaxY + 0.02,
                        g.Sign.Number + "/" + g.SizeClass + " " + layer.Kind + " maxY");
                }
            }
        }

        [Fact(DisplayName = "M9-V02: габарит текста по метрикам внутри поля с отступом")]
        public void TextInsideField()
        {
            var cfg = Config();
            foreach (var g in AllSigns())
            {
                var builderInset = g.OuterBorderWidthMm;
                var field = new OddMaster.Core.Geometry.BoundingBox(
                    g.Bounds.MinX + builderInset, g.Bounds.MinY + builderInset,
                    g.Bounds.MaxX - builderInset, g.Bounds.MaxY - builderInset);
                foreach (var text in g.Texts)
                {
                    TestAssert.NotNull(text.Layout);
                    var box = text.Layout!.Box();
                    TestAssert.True(box.MinX >= field.MinX - 0.02,
                        g.Sign.Number + "/" + g.SizeClass + " text minX " + text.Value);
                    TestAssert.True(box.MaxX <= field.MaxX + 0.02,
                        g.Sign.Number + "/" + g.SizeClass + " text maxX " + text.Value);
                    TestAssert.True(box.MinY >= field.MinY - 0.02,
                        g.Sign.Number + "/" + g.SizeClass + " text minY " + text.Value);
                    TestAssert.True(box.MaxY <= field.MaxY + 0.02,
                        g.Sign.Number + "/" + g.SizeClass + " text maxY " + text.Value);
                }
                TestAssert.True(cfg.Typography.CapHeightRatio > 0);
            }
        }

        [Fact(DisplayName = "M9-V03: нет нулевых сегментов и повторённых вершин")]
        public void NoDegenerateSegments()
        {
            foreach (var g in AllSigns())
            {
                foreach (var path in Layers(g).Concat(new[] { g.Outline }))
                {
                    var previous = path.Start;
                    foreach (var segment in path.Segments)
                    {
                        TestAssert.True(segment.Start.DistanceTo(previous) < 1e-6,
                            g.Sign.Number + ": разрыв контура");
                        TestAssert.True(segment.Start.DistanceTo(segment.End) > 1e-9,
                            g.Sign.Number + "/" + g.SizeClass + ": нулевой сегмент");
                        previous = segment.End;
                    }
                }
            }
        }

        private static IEnumerable<OddMaster.Core.Geometry.Path> Layers(SignGeometry g) =>
            g.Layers.Where(l => l.Geometry != null).Select(l => l.Geometry!);

        [Fact(DisplayName = "M9-V04: в Generation/* нет цветовых литералов; цвета слоев из палитры")]
        public void ColorsComeFromCatalog()
        {
            var dir = IoPath.Combine(Repo.Root(), "src/OddMaster.Core/Generation");
            var forbidden = new[] { "\"Red\"", "\"White\"", "\"Blue\"", "\"Black\"", "\"Green\"", "\"Yellow\"" };
            foreach (var file in Directory.GetFiles(dir, "*.cs"))
            {
                var text = File.ReadAllText(file);
                foreach (var literal in forbidden)
                    TestAssert.NotContains(literal, text, IoPath.GetFileName(file));
            }
            foreach (var g in AllSigns())
            {
                var allowed = g.Sign.Colors.Values.ToHashSet(StringComparer.Ordinal);
                foreach (var layer in g.Layers)
                {
                    TestAssert.True(allowed.Contains(layer.ColorName),
                        g.Sign.Number + ": цвет " + layer.ColorName + " не из каталога");
                    TestAssert.True(SvgPalette.IsKnown(layer.ColorName),
                        g.Sign.Number + ": цвет " + layer.ColorName + " не из палитры");
                    TestAssert.True(Enum.IsDefined(typeof(SignLayerKind), layer.Kind));
                }
            }
        }

        [Fact(DisplayName = "M9-V05: STOP вписан в восьмиугольник 2.5")]
        public void StopFitsOctagon()
        {
            var cfg = Config();
            var sign = cfg.Catalog.Find("2.5")!;
            var g = new SignGenerator(cfg).Generate(sign, "II").Geometry!;
            var text = g.Texts.Single();
            TestAssert.Equal("STOP", text.Value);
            var box = text.Layout!.Box();
            TestAssert.True(box.Width < g.Bounds.Width * 0.8, "STOP слишком wide: " + box.Width.ToString("0.##", CultureInfo.InvariantCulture));
            TestAssert.True(box.Height < g.Bounds.Height * 0.5, "STOP слишком high: " + box.Height.ToString("0.##", CultureInfo.InvariantCulture));
        }

        [Fact(DisplayName = "M9-V06: водяной знак внутри габарита знака")]
        public void WatermarkInsideBounds()
        {
            var cfg = Config();
            var renderer = new SvgSignRenderer(cfg.Drawing, cfg.Typography);
            foreach (var g in AllSigns())
            {
                if (!g.IsDraft) continue;
                var lines = renderer.Watermark(g);
                TestAssert.True(lines.Count >= 2);
                foreach (var line in lines)
                {
                    var box = line.Box();
                    TestAssert.True(box.MinX >= g.Bounds.MinX - 0.02,
                        g.Sign.Number + ": watermark minX");
                    TestAssert.True(box.MaxX <= g.Bounds.MaxX + 0.02,
                        g.Sign.Number + ": watermark maxX " + box.Width);
                    TestAssert.True(box.MinY >= g.Bounds.MinY - 0.02,
                        g.Sign.Number + ": watermark minY");
                    TestAssert.True(box.MaxY <= g.Bounds.MaxY + 0.02,
                        g.Sign.Number + ": watermark maxY");
                }
            }
        }

        [Fact(DisplayName = "M9-V07: одинаковый вход даёт побайтно одинаковый SVG")]
        public void DeterministicSvg()
        {
            var cfg = Config();
            var renderer = new SvgSignRenderer(cfg.Drawing, cfg.Typography);
            foreach (var g in AllSigns())
                TestAssert.Equal(renderer.Render(g), renderer.Render(g));
        }

        [Fact(DisplayName = "M9-V08: 2.4 без пиктограммы, 3.25 — пучок полос вместо пиктограммы")]
        public void Cr05Cr06CatalogRules()
        {
            var cfg = Config();
            TestAssert.True(cfg.Catalog.Find("2.4")!.Pictogram == null,
                "у 2.4 не должно быть пиктограммы");
            // CR-22: полосы 3.25 — примитив состава (bundleSlash), а не пиктограмма.
            var sign325 = cfg.Catalog.Find("3.25")!;
            TestAssert.True(sign325.Pictogram == null, "3.25: пиктограммы нет, есть примитивы состава");
            TestAssert.True(sign325.Elements.Contains("bundleSlash"), "3.25: пучок полос «/» из конфига");
            var g = new SignGenerator(cfg).Generate(sign325, "II",
                new Dictionary<string, decimal> { ["speed"] = 40m }).Geometry!;
            TestAssert.True(g.Layers.Count(l => l.Kind == SignLayerKind.Pictogram) == 0,
                "3.25: пиктограммного слоя быть не должно");
            TestAssert.True(g.Layers.Count(l => l.Kind == SignLayerKind.Composition) == 1 + 5,
                "3.25: контур плюс пять полос пучка");
            TestAssert.True(g.Layers.All(l => l.Kind != SignLayerKind.InnerBorder),
                "3.25: красная внутренняя кайма недопустима");
            TestAssert.True(cfg.Catalog.Find("6.4")!.Colors.ContainsKey("innerBorder") == false);
        }
    }
}
