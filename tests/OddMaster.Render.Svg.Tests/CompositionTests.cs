using System;
using System.Collections.Generic;
using System.Linq;
using OddMaster.Core.Catalog;
using OddMaster.Core.Config;
using OddMaster.Core.Generation;
using OddMaster.Core.Geometry;
using OddMaster.Render.Svg;
using OddMaster.TestKit;
using Xunit;

namespace OddMaster.Render.Svg.Tests
{
    /// <summary>
    /// CR-22…CR-25: состав знаков из примитивов, зеркальность по слоям,
    /// водяной знак внутри поля и вертикальные таблички. Проверки M16-E01…E07.
    /// </summary>
    [Collection("Demo artifacts")]
    public class CompositionTests
    {
        private static NormativeConfigSet Config() => ConfigLoader.Load(Repo.Root());

        /// <summary>Первый типоразмер, который реально есть у знака в профиле (I, II, III, IV).</summary>
        private static SignGeometry Build(string number, string? sizeClass = null)
        {
            var cfg = Config();
            var sign = cfg.Catalog.Find(number);
            if (sign == null) throw new InvalidOperationException("нет знака " + number + " в каталоге");
            var generator = new SignGenerator(cfg);
            var classes = sizeClass != null
                ? new[] { sizeClass }
                : new[] { "I", "II", "III", "IV" };
            var problems = new List<string>();
            foreach (var candidate in classes)
            {
                var generated = generator.Generate(sign, candidate,
                    OddMaster.Demo.DemoRunner.DefaultParameters(sign));
                if (generated.IsSuccess) return generated.Geometry!;
                problems.Add(candidate + ": " + string.Join("; ", generated.Findings.Select(f => f.Message)));
            }
            throw new InvalidOperationException("знак " + number + " не построен: " + string.Join(" | ", problems));
        }

        [Fact(DisplayName = "M16-E01 У знака без пиктограммы нет пиктограммного слоя")]
        public void E01_NoPictogramLayer()
        {
            var cfg = Config();
            var withoutPictogram = cfg.Catalog.Signs.Where(s => s.Pictogram == null).ToList();
            TestAssert.True(withoutPictogram.Count >= 12,
                "в каталоге слишком мало знаков без пиктограммы: " + withoutPictogram.Count);
            foreach (var sign in withoutPictogram)
            {
                var geometry = Build(sign.Number.ToString());
                TestAssert.True(geometry.Layers.All(l => l.Kind != SignLayerKind.Pictogram),
                    "у знака " + sign.Number + " не должно быть пиктограммного слоя (CR-22)");
                TestAssert.True(geometry.Layers.Any(l => l.Kind == SignLayerKind.Composition
                        || l.Kind == SignLayerKind.Text || geometry.Texts.Count > 0),
                    "у знака " + sign.Number + " нет ни состава, ни надписи");
            }
        }

        [Fact(DisplayName = "M16-E02 Примитивы состава: 3.1 «кирпич», 3.27 крест, 5.19.1 треугольник")]
        public void E02_CompositionElements()
        {
            var brick = Build("3.1");
            var brickLayers = brick.Layers.Where(l => l.Kind == SignLayerKind.Composition).ToList();
            TestAssert.True(brickLayers.Count >= 2, "у 3.1 должны быть контур и «кирпич», слоёв: " + brickLayers.Count);
            TestAssert.True(brickLayers.Any(l => l.ColorName == "White" && l.Filled),
                "белый «кирпич» обязан быть залитым слоем состава");

            var cross = Build("3.27");
            var crossLayers = cross.Layers
                .Where(l => l.Kind == SignLayerKind.Composition && l.ColorName == "Red" && l.Filled).ToList();
            TestAssert.Equal(2, crossLayers.Count, "косой крест 3.27 — две красные полосы");

            var triangle = Build("5.19.1");
            TestAssert.True(triangle.Layers.Any(l => l.Kind == SignLayerKind.Composition && l.ColorName == "White"),
                "у 5.19.1 должна быть белая треугольная вставка");

            // Все элементы состава лежат внутри габарита знака.
            foreach (var geometry in new[] { brick, cross, triangle })
                foreach (var layer in geometry.Layers.Where(l => l.Kind == SignLayerKind.Composition && l.Geometry != null))
                    foreach (var point in PathFlattener.FlattenClosed(layer.Geometry!, 1.0))
                        TestAssert.True(
                            point.X >= geometry.Bounds.MinX - 0.5 && point.X <= geometry.Bounds.MaxX + 0.5 &&
                            point.Y >= geometry.Bounds.MinY - 0.5 && point.Y <= geometry.Bounds.MaxY + 0.5,
                            "элемент состава знака " + geometry.Sign.Number + " вышел за габарит");
        }

        [Fact(DisplayName = "M16-E03 Зеркалится только пиктограмма: у 3.18.1 и 3.18.2 полоса одинаковая")]
        public void E03_MirrorOnlyPictogram()
        {
            var first = Build("3.18.1");
            var second = Build("3.18.2");
            var bandFirst = CompositionPath(first, "Red");
            var bandSecond = CompositionPath(second, "Red");
            TestAssert.Equal(BandSignature(bandFirst), BandSignature(bandSecond),
                "красная полоса у пары 3.18.x обязана идти в одну сторону (CR-23, прил. А стр. 35)");

            var arrowFirst = first.Layers.FirstOrDefault(l => l.Kind == SignLayerKind.Pictogram);
            var arrowSecond = second.Layers.FirstOrDefault(l => l.Kind == SignLayerKind.Pictogram);
            TestAssert.True(arrowFirst != null && arrowSecond != null, "у 3.18.x есть пиктограмма-заглушка");
            TestAssert.True(BandSignature(arrowFirst!.Geometry!) != BandSignature(arrowSecond!.Geometry!),
                "пиктограмма пары обязана быть зеркальной");
        }

        private static Path CompositionPath(SignGeometry geometry, string color) =>
            geometry.Layers.First(l => l.Kind == SignLayerKind.Composition && l.ColorName == color).Geometry!;

        private static string BandSignature(Path path)
        {
            var points = PathFlattener.FlattenClosed(path, 1.0);
            var minX = points.Min(p => p.X);
            var maxX = points.Max(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxY = points.Max(p => p.Y);
            // Знак наклона полосы: где «верхний конец» — справа или слева.
            var topPointX = points.OrderByDescending(p => p.Y).First().X;
            var slopeSign = topPointX > (minX + maxX) / 2 ? "+" : "-";
            return slopeSign + "|" + Math.Round(maxX - minX, 3) + "|" + Math.Round(maxY - minY, 3);
        }

        [Fact(DisplayName = "M16-E04 Водяной знак целиком внутри поля, а не только внутри bbox")]
        public void E04_WatermarkInsideField()
        {
            var font = Config().Typography;
            var renderer = new SvgSignRenderer(Config().Drawing, font);
            foreach (var number in new[] { "3.24", "3.27", "2.3.1", "2.4", "2.5", "3.1" })
            {
                var geometry = Build(number);
                var field = geometry.Layers.First(l => l.Kind == SignLayerKind.Field).Geometry!;
                var lines = renderer.Watermark(geometry);
                TestAssert.True(lines.Count > 0, "водяной знак пуст у " + number);
                foreach (var line in lines)
                    TestAssert.True(PathQueries.ContainsRectangle(field, line.Box()),
                        "строка «" + line.Value + "» знака " + number + " выходит за контур поля (CR-24)");
            }
        }

        [Fact(DisplayName = "M16-E05 8.2.2 и 8.2.3 вертикальные, содержимое по изображению")]
        public void E05_VerticalPlates()
        {
            var up = Build("8.2.2", "I");
            var down = Build("8.2.3", "I");
            TestAssert.True(up.HeightMm > up.WidthMm,
                "8.2.2 обязана быть вертикальной: " + up.WidthMm + "×" + up.HeightMm);
            TestAssert.True(down.HeightMm > down.WidthMm,
                "8.2.3 обязана быть вертикальной: " + down.WidthMm + "×" + down.HeightMm);
            TestAssert.True(up.Texts.Count == 1, "у 8.2.2 есть расстояние");
            TestAssert.True(down.Texts.Count == 0, "у 8.2.3 нет расстояния (прил. А стр. 60)");
            TestAssert.True(up.Findings.Any(f => f.Marker == "GOST-026"), "гипотеза H↔B помечена GOST-026");
            TestAssert.True(down.Findings.Any(f => f.Marker == "GOST-026"), "гипотеза H↔B помечена GOST-026");
        }

        [Fact(DisplayName = "M16-E06 Библиотека элементов: все ссылки каталога существуют")]
        public void E06_ElementLibrary()
        {
            var cfg = Config();
            TestAssert.True(cfg.Elements.Elements.Count >= 12,
                "в библиотеке элементов слишком мало примитивов: " + cfg.Elements.Elements.Count);
            foreach (var sign in cfg.Catalog.Signs)
                foreach (var id in sign.Elements)
                    TestAssert.True(cfg.Elements.Find(id) != null,
                        "знак " + sign.Number + " ссылается на неизвестный элемент «" + id + "»");
            TestAssert.Equal("Unverified", cfg.Elements.Verification.ToString(),
                "размеры элементов состава не сверены с ГОСТ");
        }

        [Fact(DisplayName = "M16-E07 Цвета MVP-30: поле и кайма заданы каталогом, не кодом")]
        public void E07_ColorsFromCatalog()
        {
            var cfg = Config();
            foreach (var sign in cfg.Catalog.Signs)
            {
                TestAssert.True(sign.Colors.ContainsKey("field"),
                    "у знака " + sign.Number + " не задан цвет поля");
                TestAssert.True(sign.Colors.ContainsKey("outerBorder"),
                    "у знака " + sign.Number + " не задан цвет наружной каймы");
            }
            // CR-22: синее поле у 3.27/3.28, белое у 3.31, красное у 3.1.
            TestAssert.Equal("Blue", cfg.Catalog.Find("3.27")!.Colors["field"]);
            TestAssert.Equal("Blue", cfg.Catalog.Find("3.28")!.Colors["field"]);
            TestAssert.Equal("White", cfg.Catalog.Find("3.31")!.Colors["field"]);
            TestAssert.Equal("Red", cfg.Catalog.Find("3.1")!.Colors["field"]);
            // CURATOR-03, стр. 31: у 2.1 белый ромб с тонким чёрным контуром, жёлтый ромб — вставка.
            TestAssert.Equal("White", cfg.Catalog.Find("2.1")!.Colors["field"]);
            TestAssert.Equal("Yellow", cfg.Catalog.Find("2.1")!.Colors["accent"]);
            TestAssert.True(cfg.Catalog.Find("2.1")!.Elements.Contains("diamondYellow"),
                "2.1: жёлтый ромб задаётся элементом diamondYellow, а не полем");
            TestAssert.Equal("White", cfg.Catalog.Find("5.41")!.Colors["field"]);
        }
    }
}
