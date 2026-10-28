using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using OddMaster.Core.Actualizer;
using OddMaster.Core.Config;
using OddMaster.Core.Document;
using OddMaster.Core.Geometry;
using OddMaster.Core.Norms;
using OddMaster.Core.Scheme;
using OddMaster.Core.Zones;
using OddMaster.TestKit;
using Xunit;

namespace OddMaster.Core.Tests
{
    /// <summary>P3: документ схемы, живая ведомость, легенда, зоны, импорт различий.</summary>
    public class DocumentModelTests
    {
        private static SchemeModel Scheme(int posts = 12)
        {
            var axis = new OddMaster.Core.Alignment.Alignment(new[]
            {
                new Point2(0, 0), new Point2(400, 0), new Point2(400, 250), new Point2(900, 250)
            }, 1000.0);
            var list = new List<Post>();
            for (int i = 0; i < posts; i++)
            {
                var post = new Post("K" + (i + 1).ToString(CultureInfo.InvariantCulture),
                    1050 + i * 60, i % 2 == 0 ? 5.0 : -5.0);
                post.Add(new SignInstance(post.Id + "-S1", SignNumber.Parse("3.24"), "II", "speed", 40m));
                if (i % 3 == 0)
                    post.Add(new SignInstance(post.Id + "-S2", SignNumber.Parse("8.2.1"), "II", "distance", 50m));
                list.Add(post);
            }
            return new SchemeModel("DEMO-KERNEL-TEST", axis, list);
        }

        [Fact(DisplayName = "M11-D01: документ схемы имеет schemaVersion и устойчивые ID")]
        public void DocumentRoundTrip()
        {
            var scheme = Scheme();
            var doc = SchemeDocument.FromScheme(scheme, "ДЕМО");
            TestAssert.Equal(SchemeDocument.CurrentSchemaVersion, doc.SchemaVersion);
            TestAssert.Equal(scheme.Posts.Count, doc.Posts.Count);
            TestAssert.Equal(scheme.Posts[0].Id, doc.Posts[0].Id);
            var json = doc.ToJson();
            TestAssert.Contains("\"schemaVersion\": 1", json);
            var back = SchemeDocument.FromJson(json);
            TestAssert.Equal(doc.Posts.Count, back.Posts.Count);
            TestAssert.Equal(doc.Posts[0].Signs[0].Number, back.Posts[0].Signs[0].Number);
            var broken = json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99");
            TestAssert.Throws<FormatException>(() => SchemeDocument.FromJson(broken));
        }

        [Fact(DisplayName = "M11-D02: живая ведомость даёт дельту только по изменённым строкам")]
        public void LiveStatementDelta()
        {
            var cfg = ConfigLoader.Load(Repo.Root());
            var live = LiveStatement.Build(Scheme(), cfg.Catalog);
            var before = live.Rows();
            var first = Scheme().Posts[0].Mounts[0];
            var changed = new SignInstance(first.Id, first.Number, "III", "speed", 40m);
            var delta = live.Apply(first, changed);
            TestAssert.True(delta.Changes.Count <= 2, "дельта должна быть маленькой");
            TestAssert.Contains("| Было | Стало |", delta.ToMarkdown());
            var after = live.Rows();
            TestAssert.Equal(before.Count + 1, after.Count);
        }

        [Fact(DisplayName = "M11-D03: перф-тест — 1000 стоек, одно изменение < 50 мс")]
        public void LiveStatementPerformance()
        {
            var cfg = ConfigLoader.Load(Repo.Root());
            var live = LiveStatement.Build(Scheme(1000), cfg.Catalog);
            var first = Scheme(1000).Posts[0].Mounts[0];
            var changed = new SignInstance(first.Id, first.Number, "III", "speed", 40m);
            var watch = Stopwatch.StartNew();
            live.Apply(first, changed);
            watch.Stop();
            TestAssert.True(watch.Elapsed.TotalMilliseconds < 50.0,
                "пересчёт занял " + watch.Elapsed.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture) + " мс");
        }

        [Fact(DisplayName = "M11-D04: легенда содержит уникальные знаки по номеру")]
        public void LegendSorted()
        {
            var cfg = ConfigLoader.Load(Repo.Root());
            var legend = LegendModel.Build(Scheme(), cfg.Catalog);
            TestAssert.Equal(2, legend.Count);
            TestAssert.Equal("3.24", legend[0].Number.ToString());
            TestAssert.Contains("3.24", LegendModel.ToMarkdown(legend));
        }

        [Fact(DisplayName = "M11-D05: модель листа сериализуется детерминированно")]
        public void SheetModelJson()
        {
            var sheet = new SheetModel("A3", 1000.0, 10.0);
            TestAssert.Equal(sheet.ToJson(), sheet.ToJson());
            TestAssert.Contains("scaleDenominator", sheet.ToJson());
        }

        [Fact(DisplayName = "M11-D06: правила зон 8.2.x из конфига со статусом Unverified")]
        public void ZoneRulesLoad()
        {
            var zones = ZoneRuleSet.Load(Repo.Root());
            TestAssert.Equal(6, zones.Plates.Count);
            TestAssert.True(zones.IsZonePlate("8.2.1"));
            TestAssert.True(zones.Pairing.Count >= 5);
            TestAssert.True(zones.Plates.All(p => p.Status == "Unverified"));
            TestAssert.Contains("[ПРОВЕРИТЬ ПО ТЕКСТУ ГОСТ]", zones.Note);
        }

        [Fact(DisplayName = "M11-D07: реестр подсказок Р 52289 честно пуст")]
        public void HintsAreEmpty()
        {
            var hints = ReferenceHints.Load(Repo.Root());
            TestAssert.Equal(0, hints.Count);
        }

        [Fact(DisplayName = "M11-D08: импорт CSV различий строит правила по категориям")]
        public void DiffMapImport()
        {
            var template = Repo.ReadText("data/diff-2004-2024.template.csv");
            var empty = DiffMapImporter.Import(template);
            TestAssert.Equal(0, empty.Rules.Count); // строка Unchanged не создаёт правило
            var csv = "номер_2004;наименование_2004;номер_2024;наименование_2024;категория;основание (пункт);примечание;статус\n"
                + "3.24;Старое;3.24;Новое;Renamed;п. 3.2;переименован;Unverified\n"
                + "5.5;Старый;5.6;Новый;Replaced;п. 3.2;замена;Unverified\n"
                + "1.1;Отменён;;;Removed;п. 3.2;исключён;Unverified\n";
            var map = DiffMapImporter.Import(csv);
            TestAssert.Equal(3, map.Rules.Count);
            TestAssert.True(map.Rules.Any(r => r.Category == MigrationCategory.Replaced));
            TestAssert.True(map.Rules.Any(r => r.Category == MigrationCategory.Removed));
            TestAssert.True(map.Rules.All(r => r.Verification != Verification.StudentVerified));
            TestAssert.Contains("Renamed", map.Rules[0].Comment ?? "");
        }

        [Fact(DisplayName = "M11-D09: демо-артефакты ядра существуют и помечены ДЕМО")]
        public void KernelArtifactsExist()
        {
            foreach (var name in new[] { "kernel_document.json", "kernel_statement.md", "kernel_statement.csv",
                "kernel_delta.md", "kernel_journal_before.md", "kernel_journal_after.md",
                "kernel_journal_diff.txt", "kernel_legend.md", "kernel_sheet.json",
                "kernel_zones.md", "kernel_actualization.md", "kernel_actualization.csv",
                "kernel_diff_map.md" })
                TestAssert.True(Repo.Exists("docs/demo/" + name), "нет docs/demo/" + name);
            var doc = Repo.ReadText("docs/demo/kernel_document.json");
            TestAssert.Contains("DEMO-KERNEL-01", doc);
            TestAssert.Contains("ДЕМО", Repo.ReadText("docs/demo/kernel_zones.md"));
            TestAssert.Contains("X 50", Repo.ReadText("docs/demo/kernel_journal_diff.txt"));
        }
    }
}
