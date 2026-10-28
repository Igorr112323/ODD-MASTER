using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OddMaster.Core.Actualizer;
using OddMaster.Core.Catalog;
using OddMaster.Core.Config;
using OddMaster.Core.Document;
using OddMaster.Core.Geometry;
using OddMaster.Core.Pictograms;
using OddMaster.Core.Scheme;
using OddMaster.Core.Typography;
using OddMaster.Core.Norms;
using OddMaster.Core.Zones;
using OddMaster.TestKit;
using Xunit;
using IoPath = System.IO.Path;

namespace OddMaster.Core.Tests
{
    /// <summary>Покрытие новых модулей P2/P3: пиктограммы, документ, зоны, импорт различий (CR-03).</summary>
    public class NewModulesCoverageTests
    {
        private static string TempRoot()
        {
            var dir = IoPath.Combine(IoPath.GetTempPath(), "odd-cov-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(IoPath.Combine(dir, "config/rules"));
            Directory.CreateDirectory(IoPath.Combine(dir, "config/pictograms"));
            return dir;
        }

        [Fact(DisplayName = "M12-X01: PictogramDefinition.ToJson содержит все поля")]
        public void PictogramJson()
        {
            var def = new PictogramDefinition("pict.test", "3.19", "StudentTranscribed",
                "прил. Б, стр. 83", new PictogramGrid(12, "D"),
                new List<string> { "{\"role\":\"symbol\",\"color\":\"Black\",\"shapes\":[]}" }, "заметка");
            var json = def.ToJson();
            TestAssert.Contains("\"id\": \"pict.test\"", json);
            TestAssert.Contains("\"status\": \"StudentTranscribed\"", json);
            TestAssert.Contains("\"cellsAcross\": 12", json);
            TestAssert.Contains("\"layers\"", json);
            // JsonSerializer по умолчанию экранирует кириллицу в \uXXXX — это валидный JSON.
            TestAssert.Contains("\"note\"", json);
        }

        [Fact(DisplayName = "M12-X02: PictogramRegistry ловит ошибки статуса, сетки и ссылки на знак")]
        public void RegistryErrors()
        {
            var root = TempRoot();
            File.WriteAllText(IoPath.Combine(root, "config/pictograms/broken.json"),
                "{\"pictograms\":[{\"id\":\"p1\",\"sign\":\"9.99\",\"status\":\"Nope\",\"source\":\"\","
                + "\"grid\":{\"cellsAcross\":0,\"spans\":\"Z\"},\"layers\":[]}]}");
            var cfg = ConfigLoader.Load(Repo.Root());
            var errors = PictogramRegistry.Validate(root, cfg.Catalog);
            TestAssert.True(errors.Count >= 5, string.Join(" | ", errors));
            TestAssert.Contains("неизвестный статус", string.Join(" ", errors));
            TestAssert.Contains("cellsAcross", string.Join(" ", errors));
            Directory.Delete(root, true);
        }

        [Fact(DisplayName = "M12-X03: реестр без папки даёт понятную ошибку")]
        public void RegistryMissingFolder()
        {
            var dir = IoPath.Combine(IoPath.GetTempPath(), "odd-cov-empty-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var loaded = PictogramRegistry.Load(dir, out var errors);
            TestAssert.Equal(0, loaded.Count);
            TestAssert.True(errors.Count > 0);
            Directory.Delete(dir, true);
        }

        [Fact(DisplayName = "M12-X04: импортёр понимает path M/L, polygon и несколько линий")]
        public void ImportPaths()
        {
            const string svg = "<svg viewBox=\"0 0 100 100\">"
                + "<path d=\"M 10 10 L 20 20 L 30 10 Z\"/>"
                + "<polygon points=\"40,40 50,40 50,50\"/>"
                + "<polyline points=\"60,60 70,70\"/></svg>";
            var def = PictogramImporter.Import(svg, "3.19", "тест", 10, 0.01);
            TestAssert.True(def.Layers.Count == 1);
            TestAssert.Contains("polyline", def.ToJson());
            TestAssert.True(def.Layers[0].Contains("\"points\""));
        }

        [Fact(DisplayName = "M12-X05: импортёр отклоняет SVG без линий и кривые")]
        public void ImportRejects()
        {
            TestAssert.Throws<FormatException>(() =>
                PictogramImporter.Import("<svg viewBox=\"0 0 10 10\"></svg>", "3.19", "тест"));
            var curves = PictogramImporter.Import(
                "<svg viewBox=\"0 0 10 10\"><path d=\"M0 0 C 1 1 2 2 3 3\"/><polyline points=\"1,1 2,2\"/></svg>",
                "3.19", "тест");
            TestAssert.Contains("polyline", curves.ToJson());
        }

        [Fact(DisplayName = "M12-X06: Дуглас–Пекер сохраняет изломы и вырожденные линии")]
        public void SimplifyCorners()
        {
            var zigzag = new List<Point2>
            {
                new Point2(0, 0), new Point2(1, 0), new Point2(2, 5), new Point2(3, 0), new Point2(4, 0)
            };
            var kept = PictogramImporter.DouglasPeucker(zigzag, 0.2);
            TestAssert.True(kept.Count >= 3 && kept.Count < zigzag.Count + 1,
                "сохранено точек: " + kept.Count.ToString(CultureInfo.InvariantCulture));
            var same = PictogramImporter.DouglasPeucker(new List<Point2> { new Point2(1, 1), new Point2(1, 1) }, 0.1);
            TestAssert.Equal(2, same.Count);
        }

        [Fact(DisplayName = "M12-X07: накладка без viewBox использует запасной размер")]
        public void OverlayNoViewBox()
        {
            var result = PictogramOverlay.Overlay("AA==", "<svg></svg>");
            TestAssert.Contains("viewBox=\"0 0 800 800\"", result);
            TestAssert.Contains("width=\"800\"", result);
        }

        [Fact(DisplayName = "M12-X08: nomenclature импорт ловит битые строки")]
        public void NomenclatureErrors()
        {
            TestAssert.Throws<FormatException>(() =>
                NomenclatureImporter.Parse("номер;наименование;группа;статус\n3.1;Тест\n"));
            TestAssert.Throws<FormatException>(() =>
                NomenclatureImporter.Parse("номер;наименование;группа;статус\n3.1;Тест;x;Unverified\n"));
            var rows = NomenclatureImporter.Parse("\uFEFFномер;наименование;группа;статус\n\n3.1;Тест;3;Unverified\n");
            TestAssert.Equal(1, rows.Count);
            TestAssert.Contains("3.1", NomenclatureImporter.ToCatalogStubs(rows));
        }

        [Fact(DisplayName = "M12-X09: документ схемы хранит зоны и длину зоны")]
        public void DocumentZones()
        {
            var doc = new SchemeDocument { Id = "D1", Title = "тест" };
            var zone = new DocumentZone
            {
                Id = "Z1", StartSign = "3.24", EndSign = "3.25",
                StartStationMeters = 100, EndStationMeters = 350
            };
            zone.Plates.Add("8.2.1");
            doc.Zones.Add(zone);
            TestAssert.Equal(250, zone.LengthMeters);
            var back = SchemeDocument.FromJson(doc.ToJson());
            TestAssert.Equal(1, back.Zones.Count);
            TestAssert.Equal("8.2.1", back.Zones[0].Plates[0]);
        }

        [Fact(DisplayName = "M12-X10: LiveStatement добавляет и удаляет знаки, печатает CSV")]
        public void LiveStatementAddRemove()
        {
            var cfg = ConfigLoader.Load(Repo.Root());
            var axis = new OddMaster.Core.Alignment.Alignment(new[] { new Point2(0, 0), new Point2(100, 0) });
            var post = new Post("P1", 10, 5);
            post.Add(new SignInstance("s1", OddMaster.Core.Norms.SignNumber.Parse("3.24"), "II", "speed", 40m));
            var live = LiveStatement.Build(new SchemeModel("s", axis, new[] { post }), cfg.Catalog);
            TestAssert.Equal(1, live.Rows().Count);
            var added = new SignInstance("s2", OddMaster.Core.Norms.SignNumber.Parse("2.4"), "II");
            var delta = live.Apply(null, added);
            TestAssert.Equal(1, delta.Changes.Count);
            TestAssert.Equal(2, live.Rows().Count);
            live.Apply(added, null);
            TestAssert.Equal(1, live.Rows().Count);
            TestAssert.Contains("Номер знака", live.ToCsv());
            TestAssert.Contains("Ведомость ТС ОДД", live.ToMarkdown());
        }

        [Fact(DisplayName = "M12-X11: легенда пустой схемы и JSON листа")]
        public void EmptyLegendAndSheet()
        {
            var cfg = ConfigLoader.Load(Repo.Root());
            var axis = new OddMaster.Core.Alignment.Alignment(new[] { new Point2(0, 0), new Point2(10, 0) });
            var legend = LegendModel.Build(new SchemeModel("empty", axis), cfg.Catalog);
            TestAssert.Equal(0, legend.Count);
            TestAssert.Contains("| Номер знака |", LegendModel.ToMarkdown(legend));
            var sheet = new SheetModel("A2", 500.0, 20.0);
            TestAssert.Equal("A2", sheet.Format);
            TestAssert.Contains("\"scaleDenominator\": 500", sheet.ToJson());
        }

        [Fact(DisplayName = "M12-X12: ZoneRuleSet требует normRef/verification")]
        public void ZoneProvenance()
        {
            var root = TempRoot();
            File.WriteAllText(IoPath.Combine(root, "config/rules/zones.json"),
                "{\"plates\":[],\"pairing\":[]}");
            TestAssert.Throws<ConfigException>(() => ZoneRuleSet.Load(root));
            Directory.Delete(root, true);
            var hintsRoot = TempRoot();
            File.WriteAllText(IoPath.Combine(hintsRoot, "config/rules/hints-52289.json"),
                "{\"hints\":[{\"sign\":\"3.24\",\"clause\":\"п. 5.1\"}]}");
            var hints = ReferenceHints.Load(hintsRoot);
            TestAssert.Equal(1, hints.Count);
            Directory.Delete(hintsRoot, true);
        }

        [Fact(DisplayName = "M12-X13: DiffMapImporter покрывает все категории и статусы")]
        public void DiffCategories()
        {
            const string header = "номер_2004;наименование_2004;номер_2024;наименование_2024;категория;основание (пункт);примечание;статус\n";
            var csv = header
                + "1.1;A;1.2;B;Replaced;п. 1;x;CuratorExtracted\n"
                + "1.3;C;;;Removed;п. 2;y;StudentVerified\n"
                + "1.4;D;1.5;E;NeedsDecision;п. 3;z;Unverified\n"
                + "1.6;F;1.7;G;AllowedSimplify;п. 4;w;Placeholder\n"
                + "1.8;H;1.9;I;UnknownCategory;п. 5;v;Unverified\n"
                + "2.1;J;2.1;J;Unchanged;п. 6;u;Unverified\n"
                + "# комментарий;x;y;z;a;b;c;d\n";
            var map = DiffMapImporter.Import(csv, "тест");
            TestAssert.Equal(5, map.Rules.Count);
            TestAssert.True(map.Rules.Any(r => r.Category == MigrationCategory.Removed && r.ResultSigns == null));
            TestAssert.True(map.Rules.Any(r => r.Verification == Verification.CuratorExtracted));
            TestAssert.True(map.Rules.Any(r => r.Verification == Verification.Placeholder));
            TestAssert.True(map.Rules.Any(r => r.Category == MigrationCategory.NeedsDecision));
            TestAssert.Contains("ЧЕРНОВИК", map.Banner);
            TestAssert.Throws<FormatException>(() => DiffMapImporter.Import(header + "1.1;A;B\n"));
        }

        [Fact(DisplayName = "M12-X14: FontMetrics и TextEngine работают с неизвестными символами")]
        public void FontMetricsFallback()
        {
            var cfg = ConfigLoader.Load(Repo.Root());
            var m = cfg.Typography;
            TestAssert.True(m.TotalAdvance("Ж") >= m.DefaultAdvanceRatio);
            TestAssert.True(m.TotalAdvance("") > 0);
            var layout = TextEngine.Layout("40", 300, 300, m, new Point2(0, 0));
            TestAssert.True(layout.WidthMm <= 300.01);
            TestAssert.True(layout.HeightMm > 0);
            TestAssert.True(TextEngine.EstimateWidth("40", layout.HeightMm, m) > 0);
            var block = TextEngine.LayoutBlock(new[] { "ЧЕРНОВИК:", "есть непроверенные параметры" },
                200, 100, m, new Point2(0, 0));
            TestAssert.Equal(2, block.Count);
            TestAssert.True(block.All(l => l.WidthMm <= 200.01));
        }

        [Fact(DisplayName = "M12-X15: Arrow и MirrorPath обратимы на дугах")]
        public void ArrowAndMirror()
        {
            var arrow = GeometryPrimitives.Arrow(10, false);
            var mirrored = GeometryPrimitives.Arrow(10, true);
            var reflected = GeometryPrimitives.MirrorPath(arrow);
            TestAssert.Equal(arrow.Segments.Count, reflected.Segments.Count);
            for (int i = 0; i < arrow.Segments.Count; i++)
                TestAssert.Close(-arrow.Segments[i].End.X, reflected.Segments[i].End.X, 1e-9);
            TestAssert.Close(-arrow.Start.X, mirrored.Start.X, 1e-9);
            var circle = GeometryPrimitives.Circle(100);
            var circleMirror = GeometryPrimitives.MirrorPath(circle);
            TestAssert.Equal(circle.Segments.Count, circleMirror.Segments.Count);
            TestAssert.Close(100, circleMirror.Bounds().Width, 0.001);
        }

        [Fact(DisplayName = "M12-X16: JsonConfig читает отсутствующий файл как пустую строку")]
        public void ReadUtf8IfExists()
        {
            TestAssert.Equal(string.Empty, JsonConfig.ReadUtf8IfExists(Repo.Root(), "нет-такого-файла.txt"));
            TestAssert.Contains("8.0.100", JsonConfig.ReadUtf8IfExists(Repo.Root(), "global.json"));
            TestAssert.Throws<ConfigException>(() => JsonConfig.Load(Repo.Root(), "нет-такого.json"));
        }
    }
}
