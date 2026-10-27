using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using OddMaster.Core.Catalog;
using OddMaster.Core.Config;
using OddMaster.Core.Generation;
using OddMaster.Core.Geometry;
using OddMaster.Core.Norms;
using OddMaster.Render.Svg;
using OddMaster.TestKit;
using IoPath = System.IO.Path;

namespace OddMaster.Render.Svg.Tests
{
    /// <summary>Golden-тесты и инварианты SVG-рендера (M3).</summary>
    public static class SvgTests
    {
        private static (SignGeometry Geometry, SvgSignRenderer Renderer) Make(string number, string sizeClass)
        {
            var cfg = ConfigLoader.Load(Repo.Root());
            var sign = cfg.Catalog.Find(number)!;
            var result = new SignGenerator(cfg).Generate(sign, sizeClass,
                OddMaster.Demo.DemoRunner.DefaultParameters(sign));
            Assert.True(result.IsSuccess, number + "/" + sizeClass);
            return (result.Geometry!, new SvgSignRenderer(cfg.Drawing));
        }

        [OddFact("M3-S01: все SVG галереи разбираются как XML")]
        public static void GalleryIsValidXml()
        {
            var dir = IoPath.Combine(Repo.Root(), "docs/demo/signs");
            var files = Directory.GetFiles(dir, "*.svg").OrderBy(x => x, StringComparer.Ordinal).ToList();
            Assert.True(files.Count >= 20, "мало файлов галереи: " + files.Count);
            foreach (var file in files)
            {
                var doc = XDocument.Parse(File.ReadAllText(file));
                Assert.Equal("svg", doc.Root!.Name.LocalName);
            }
        }

        [OddFact("M3-S02: golden-файлы 3.24/II и 2.5/II совпадают")]
        public static void GoldenFilesMatch()
        {
            foreach (var (number, sizeClass) in new[] { ("3.24", "II"), ("2.5", "II"), ("8.2.1", "II") })
            {
                var (geometry, renderer) = Make(number, sizeClass);
                var svg = renderer.Render(geometry);
                var name = number.Replace('.', '-') + "_" + sizeClass + ".svg";
                var gallery = Repo.ReadText("docs/demo/signs/" + name);
                Assert.Equal(gallery, svg);
                Golden.CheckText("tests/OddMaster.Render.Svg.Tests/Approved/" + name, svg);
            }
        }

        [OddFact("M3-S03: metadata содержит номер, типоразмер, профиль и не содержит время/GUID")]
        public static void MetadataIsCompleteAndDeterministic()
        {
            var (geometry, renderer) = Make("3.24", "II");
            var svg = renderer.Render(geometry);
            Assert.Contains("number=\"3.24\"", svg);
            Assert.Contains("sizeClass=\"II\"", svg);
            Assert.Contains("profile=\"GOST_R_52290_2024\"", svg);
            Assert.Contains("sizeTable=\"Д.4\"", svg);
            Assert.Contains("isDraft=\"true\"", svg);
            Assert.NotContains("2026-", svg);
            Assert.NotContains("Generated", svg);
            Assert.NotContains("guid", svg.ToLowerInvariant());
        }

        [OddFact("M3-S04: два прогона дают одинаковые байты")]
        public static void RenderIsByteDeterministic()
        {
            var (geometry, renderer) = Make("2.4", "III");
            var a = renderer.Render(geometry);
            var b = renderer.Render(geometry);
            Assert.Equal(a, b);
            var bytesA = System.Text.Encoding.UTF8.GetBytes(a);
            var bytesB = System.Text.Encoding.UTF8.GetBytes(b);
            Assert.True(bytesA.SequenceEqual(bytesB));
        }

        [OddFact("M3-S05: у черновика есть водяной знак, у проверенного — нет")]
        public static void WatermarkOnlyForDraft()
        {
            var (geometry, renderer) = Make("6.4", "II");
            Assert.Contains("ЧЕРНОВИК", renderer.Render(geometry));
            var clean = CleanGeometry();
            Assert.NotContains("ЧЕРНОВИК", renderer.Render(clean));
        }

        private static SignGeometry CleanGeometry()
        {
            var cfg = ConfigLoader.Load(Repo.Root());
            var sign = cfg.Catalog.Find("6.4")!;
            var summary = new VerificationSummary();
            summary.Add("catalog", Verification.StudentVerified);
            summary.Add("colors", Verification.StudentVerified);
            var path = GeometryPrimitives.Circle(700);
            return new SignGeometry(sign, "II", "GOST_R_52290_2024", "Д.5",
                BoundingBox.Centered(700, 700), path,
                new List<SignLayer> { new SignLayer(SignLayerKind.Outline, "Blue", path, true, 0) },
                new List<SignText>(), 20, 6, false, null, summary, new List<Finding>());
        }

        [OddFact("M3-S06: рендер не зависит от текущей культуры (ru-RU / en-US)")]
        public static void RenderIsCultureInvariant()
        {
            var (geometry, renderer) = Make("3.25", "III");
            var saved = CultureInfo.CurrentCulture;
            try
            {
                var results = new List<string>();
                foreach (var name in new[] { "ru-RU", "en-US" })
                {
                    CultureInfo.CurrentCulture = new CultureInfo(name);
                    results.Add(renderer.Render(geometry));
                }
                Assert.Equal(results[0], results[1]);
                Assert.NotContains(",", results[0].Split(new[] { "viewBox=\"" }, StringSplitOptions.None)[1].Split('"')[0]);
            }
            finally { CultureInfo.CurrentCulture = saved; }
        }

        [OddFact("M3-S07: слои оформлены как <g id>, цвета — из палитры")]
        public static void LayersAndPalette()
        {
            var (geometry, renderer) = Make("2.5", "II");
            var svg = renderer.Render(geometry);
            Assert.Contains("id=\"Layer-Outline\"", svg);
            Assert.Contains("id=\"Layer-OuterBorder\"", svg);
            Assert.Contains("id=\"DraftWatermark\"", svg);
            Assert.Contains("#D7191C", svg);
            Assert.Equal("#D7191C", SvgPalette.Resolve("Red"));
            Assert.Equal("#808080", SvgPalette.Resolve("UnknownColor"));
        }

        [OddFact("M3-S09: отсутствующий golden-файл — ошибка, а не тихое создание эталона")]
        public static void MissingGoldenFailsLoudly()
        {
            var saved = Environment.GetEnvironmentVariable("ODD_UPDATE_GOLDEN");
            try
            {
                Environment.SetEnvironmentVariable("ODD_UPDATE_GOLDEN", null);
                var ex = Assert.Throws<InvalidOperationException>(() =>
                    Golden.CheckText("tests/OddMaster.Render.Svg.Tests/Approved/__absent__.svg", "x"));
                Assert.Contains("Отсутствует golden-файл", ex.Message);
            }
            finally { Environment.SetEnvironmentVariable("ODD_UPDATE_GOLDEN", saved); }
        }

        [OddFact("M3-S08: XML-экранирование и инвариантные числа в пути")]
        public static void EscapingAndNumbers()
        {
            Assert.Equal("&lt;a&amp;b&quot;", SvgSignRenderer.Escape("<a&b\""));
            Assert.Equal("0.5", SvgPathWriter.Num(0.5));
            Assert.Equal("700", SvgPathWriter.Num(700));
            var saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
                Assert.Equal("0.5", SvgPathWriter.Num(0.5));
            }
            finally { CultureInfo.CurrentCulture = saved; }
        }
    }
}
