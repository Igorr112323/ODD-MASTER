using Xunit;
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
    [Collection("Demo artifacts")]
    public class SvgTests
    {
        private static (SignGeometry Geometry, SvgSignRenderer Renderer) Make(string number, string sizeClass)
        {
            var cfg = ConfigLoader.Load(Repo.Root());
            var sign = cfg.Catalog.Find(number)!;
            var result = new SignGenerator(cfg).Generate(sign, sizeClass,
                OddMaster.Demo.DemoRunner.DefaultParameters(sign));
            TestAssert.True(result.IsSuccess, number + "/" + sizeClass);
            return (result.Geometry!, new SvgSignRenderer(cfg.Drawing, cfg.Typography));
        }

        [Fact(DisplayName = "M3-S01: все SVG галереи разбираются как XML")]
        public void GalleryIsValidXml()
        {
            var dir = IoPath.Combine(Repo.Root(), "docs/demo/signs");
            var files = Directory.GetFiles(dir, "*.svg").OrderBy(x => x, StringComparer.Ordinal).ToList();
            TestAssert.True(files.Count >= 20, "мало файлов галереи: " + files.Count);
            foreach (var file in files)
            {
                var doc = XDocument.Parse(File.ReadAllText(file));
                TestAssert.Equal("svg", doc.Root!.Name.LocalName);
            }
        }

        [Fact(DisplayName = "M3-S02: golden-файлы 3.24/II и 2.5/II совпадают")]
        public void GoldenFilesMatch()
        {
            foreach (var (number, sizeClass) in new[] { ("3.24", "II"), ("2.5", "II"), ("8.2.1", "II") })
            {
                var (geometry, renderer) = Make(number, sizeClass);
                var svg = renderer.Render(geometry);
                var name = number.Replace('.', '-') + "_" + sizeClass + ".svg";
                var gallery = Repo.ReadText("docs/demo/signs/" + name);
                TestAssert.Equal(gallery, svg);
                Golden.CheckText("tests/OddMaster.Render.Svg.Tests/Approved/" + name, svg);
            }
        }

        [Fact(DisplayName = "M3-S03: metadata содержит номер, типоразмер, профиль и не содержит время/GUID")]
        public void MetadataIsCompleteAndDeterministic()
        {
            var (geometry, renderer) = Make("3.24", "II");
            var svg = renderer.Render(geometry);
            TestAssert.Contains("number=\"3.24\"", svg);
            TestAssert.Contains("sizeClass=\"II\"", svg);
            TestAssert.Contains("profile=\"GOST_R_52290_2024\"", svg);
            TestAssert.Contains("sizeTable=\"Д.4\"", svg);
            TestAssert.Contains("isDraft=\"true\"", svg);
            TestAssert.NotContains("2026-", svg);
            TestAssert.NotContains("Generated", svg);
            TestAssert.NotContains("guid", svg.ToLowerInvariant());
        }

        [Fact(DisplayName = "M3-S04: два прогона дают одинаковые байты")]
        public void RenderIsByteDeterministic()
        {
            var (geometry, renderer) = Make("2.4", "III");
            var a = renderer.Render(geometry);
            var b = renderer.Render(geometry);
            TestAssert.Equal(a, b);
            var bytesA = System.Text.Encoding.UTF8.GetBytes(a);
            var bytesB = System.Text.Encoding.UTF8.GetBytes(b);
            TestAssert.True(bytesA.SequenceEqual(bytesB));
        }

        [Fact(DisplayName = "M3-S05: у черновика есть водяной знак, у проверенного — нет")]
        public void WatermarkOnlyForDraft()
        {
            var (geometry, renderer) = Make("6.4", "II");
            TestAssert.Contains("ЧЕРНОВИК", renderer.Render(geometry));
            var clean = CleanGeometry();
            TestAssert.NotContains("ЧЕРНОВИК", renderer.Render(clean));
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

        [Fact(DisplayName = "M3-S06: рендер не зависит от текущей культуры (ru-RU / en-US)")]
        public void RenderIsCultureInvariant()
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
                TestAssert.Equal(results[0], results[1]);
                TestAssert.NotContains(",", results[0].Split(new[] { "viewBox=\"" }, StringSplitOptions.None)[1].Split('"')[0]);
            }
            finally { CultureInfo.CurrentCulture = saved; }
        }

        [Fact(DisplayName = "M3-S07: слои оформлены как <g id>, цвета — из палитры")]
        public void LayersAndPalette()
        {
            var (geometry, renderer) = Make("2.5", "II");
            var svg = renderer.Render(geometry);
            TestAssert.Contains("id=\"Layer-Outline\"", svg);
            TestAssert.Contains("id=\"Layer-Field\"", svg);
            TestAssert.Contains("id=\"DraftWatermark\"", svg);
            TestAssert.Contains("#D7191C", svg);
            TestAssert.Equal("#D7191C", SvgPalette.Resolve("Red"));
            TestAssert.Equal("#808080", SvgPalette.Resolve("UnknownColor"));
        }

        [Fact(DisplayName = "M3-S09: отсутствующий golden-файл — ошибка, а не тихое создание эталона")]
        public void MissingGoldenFailsLoudly()
        {
            var saved = Environment.GetEnvironmentVariable("ODD_UPDATE_GOLDEN");
            try
            {
                Environment.SetEnvironmentVariable("ODD_UPDATE_GOLDEN", null);
                var ex = TestAssert.Throws<InvalidOperationException>(() =>
                    Golden.CheckText("tests/OddMaster.Render.Svg.Tests/Approved/__absent__.svg", "x"));
                TestAssert.Contains("Отсутствует golden-файл", ex.Message);
            }
            finally { Environment.SetEnvironmentVariable("ODD_UPDATE_GOLDEN", saved); }
        }

        [Fact(DisplayName = "M3-S08: XML-экранирование и инвариантные числа в пути")]
        public void EscapingAndNumbers()
        {
            TestAssert.Equal("&lt;a&amp;b&quot;", SvgSignRenderer.Escape("<a&b\""));
            TestAssert.Equal("0.5", SvgPathWriter.Num(0.5));
            TestAssert.Equal("700", SvgPathWriter.Num(700));
            var saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
                TestAssert.Equal("0.5", SvgPathWriter.Num(0.5));
            }
            finally { CultureInfo.CurrentCulture = saved; }
        }
    }
}
