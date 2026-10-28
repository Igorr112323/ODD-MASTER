using System;
using System.Collections.Generic;
using System.Linq;
using OddMaster.Core.Catalog;
using OddMaster.Core.Config;
using OddMaster.Core.Generation;
using OddMaster.Core.Geometry;
using OddMaster.Core.Norms;
using OddMaster.Core.Pictograms;
using OddMaster.TestKit;
using Xunit;

namespace OddMaster.Core.Tests
{
    /// <summary>P2: каталог MVP-30, таблички, зеркала, состав, пиктограммы, номенклатура.</summary>
    public class CatalogMvpTests
    {
        private static NormativeConfigSet Cfg() => ConfigLoader.Load(Repo.Root());

        [Fact(DisplayName = "M10-P01: каталог содержит 30 знаков и 11 табличек")]
        public void CatalogSize()
        {
            var cfg = Cfg();
            TestAssert.Equal(41, cfg.Catalog.Signs.Count);
            TestAssert.Equal(11, cfg.Catalog.Signs.Count(s => s.Group == 8));
            TestAssert.Equal(30, cfg.Catalog.Signs.Count(s => s.Group != 8));
        }

        [Fact(DisplayName = "M10-P02: у всех записей есть наименование и честный статус")]
        public void NamesAndStatuses()
        {
            foreach (var sign in Cfg().Catalog.Signs)
            {
                TestAssert.True(sign.Name.Length > 0, sign.Number + ": пустое наименование");
                TestAssert.True(sign.Verification != Verification.StudentVerified,
                    sign.Number + ": агент не имеет права ставить StudentVerified");
                TestAssert.True(sign.NormRef.Doc.Length > 0 && sign.NormRef.Clause.Length > 0,
                    sign.Number + ": нет normRef");
            }
        }

        [Fact(DisplayName = "M10-P03: каждый знак MVP генерируется хотя бы в одном типоразмере")]
        public void EverySignGenerates()
        {
            var cfg = Cfg();
            var gen = new SignGenerator(cfg);
            foreach (var sign in cfg.Catalog.Signs)
            {
                var classes = Classes(cfg.MainProfile, sign.Number);
                TestAssert.True(classes.Count > 0, sign.Number + ": нет типоразмеров в Д.2–Д.9");
                var parameters = DemoParams(sign);
                var ok = classes.Any(c => gen.Generate(sign, c, parameters).IsSuccess);
                TestAssert.True(ok, sign.Number + ": не удалось сгенерировать ни один типоразмер");
            }
        }

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

        private static List<string> Classes(SizeProfile profile, SignNumber number)
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

        [Fact(DisplayName = "M10-P04: mirrorOf и composedOf ссылаются на существующие знаки")]
        public void ReferencesResolve()
        {
            var cfg = Cfg();
            var mirrors = cfg.Catalog.Signs.Where(s => s.MirrorOf != null).ToList();
            TestAssert.Equal(3, mirrors.Count);
            foreach (var mirror in mirrors)
                TestAssert.NotNull(cfg.Catalog.Find(mirror.MirrorOf!), mirror.Number + " → " + mirror.MirrorOf);
            var composed = cfg.Catalog.Signs.Where(s => s.ComposedOf.Count > 0).ToList();
            TestAssert.Equal(2, composed.Count);
            foreach (var c in composed)
                foreach (var reference in c.ComposedOf)
                    TestAssert.NotNull(cfg.Catalog.Find(reference), c.Number + " → " + reference);
        }

        [Fact(DisplayName = "M10-P05: геометрия зеркальной пары совпадает с отражением")]
        public void MirrorGeometry()
        {
            var cfg = Cfg();
            var gen = new SignGenerator(cfg);
            foreach (var mirror in cfg.Catalog.Signs.Where(s => s.MirrorOf != null))
            {
                var original = cfg.Catalog.Find(mirror.MirrorOf!)!;
                var sizeClass = Classes(cfg.MainProfile, mirror.Number).First();
                var a = gen.Generate(original, sizeClass).Geometry!;
                var b = gen.Generate(mirror, sizeClass).Geometry!;
                var originalSymbol = a.Layers.Last(l => l.Kind == SignLayerKind.Pictogram).Geometry!;
                var mirroredSymbol = b.Layers.Last(l => l.Kind == SignLayerKind.Pictogram).Geometry!;
                var reflected = GeometryPrimitives.MirrorPath(originalSymbol);
                TestAssert.Equal(Points(originalSymbol).Count, Points(mirroredSymbol).Count);
                var expected = Points(reflected).OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
                var actual = Points(mirroredSymbol).OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
                for (int i = 0; i < expected.Count; i++)
                    TestAssert.Close(expected[i].X, actual[i].X, 1e-6, mirror.Number + " mirror X");
                for (int i = 0; i < expected.Count; i++)
                    TestAssert.Close(expected[i].Y, actual[i].Y, 1e-6, mirror.Number + " mirror Y");
            }
        }

        private static List<Point2> Points(OddMaster.Core.Geometry.Path path)
        {
            var result = new List<Point2> { path.Start };
            foreach (var segment in path.Segments) result.Add(segment.End);
            return result;
        }

        [Fact(DisplayName = "M10-P06: параметры знаков MVP валидируются по табл. Д.1")]
        public void ParametersValidate()
        {
            var cfg = Cfg();
            foreach (var sign in cfg.Catalog.Signs.Where(s => s.ParameterKind != null))
            {
                var check = cfg.Parameters.Validate(sign.Number, 40m);
                TestAssert.True(check.IsValid || !check.HasRule,
                    sign.Number + ": " + check.Reason);
            }
            TestAssert.True(cfg.Catalog.Find("6.2.1")!.Text!.Marker == "GOST-009");
        }

        [Fact(DisplayName = "M10-P07: реестр пиктограмм прошёл валидацию, пустых статусов нет")]
        public void PictogramRegistryValid()
        {
            var cfg = Cfg();
            var errors = PictogramRegistry.Validate(Repo.Root(), cfg.Catalog);
            TestAssert.Equal(0, errors.Count);
            var loaded = PictogramRegistry.Load(Repo.Root(), out _);
            TestAssert.True(loaded.Count >= 30, "мало пиктограмм: " + loaded.Count);
            TestAssert.True(loaded.All(p => p.Status.Length > 0));
        }

        [Fact(DisplayName = "M10-P08: импорт синтетической стрелки даёт StudentTranscribed")]
        public void ImportArrow()
        {
            const string arrow = "<?xml version=\"1.0\"?><svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 120 120\">"
                + "<polyline points=\"10,60 30,60 30,50 60,60 30,70 30,60 90,60\"/></svg>";
            var definition = PictogramImporter.Import(arrow, "3.19.test", "синтетическая стрелка");
            TestAssert.Equal("StudentTranscribed", definition.Status);
            TestAssert.Contains("polyline", definition.ToJson());
            TestAssert.Contains("cellsAcross", definition.ToJson());
            var simplified = PictogramImporter.DouglasPeucker(new[]
            {
                new Point2(0, 0), new Point2(1, 0.01), new Point2(2, 0)
            }, 0.1);
            TestAssert.Equal(2, simplified.Count);
        }

        [Fact(DisplayName = "M10-P09: накладка содержит растр и полупрозрачный знак")]
        public void Overlay()
        {
            var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 800 800\"></svg>";
            var result = PictogramOverlay.Overlay("AA==", svg);
            TestAssert.Contains("data:image/png;base64,AA==", result);
            TestAssert.Contains("opacity=\"0.55\"", result);
        }

        [Fact(DisplayName = "M10-P10: CSV-номенклатура разбирается и даёт заготовки")]
        public void Nomenclature()
        {
            var csv = Repo.ReadText("config/catalog/nomenclature-2024.csv");
            var rows = NomenclatureImporter.Parse(csv);
            TestAssert.Equal(41, rows.Count);
            TestAssert.Equal("2.1", rows[0].Number);
            var stubs = NomenclatureImporter.ToCatalogStubs(rows);
            TestAssert.Contains("\"number\": \"2.1\"", stubs);
        }

        [Fact(DisplayName = "M10-P11: шаблон таблицы различий имеет 8 колонок")]
        public void DiffTemplate()
        {
            var header = Repo.ReadText("data/diff-2004-2024.template.csv").Split('\n')[0];
            TestAssert.Equal(8, header.Split(';').Length);
            TestAssert.Contains("категория", header);
            TestAssert.Contains("основание", header);
        }
    }
}
