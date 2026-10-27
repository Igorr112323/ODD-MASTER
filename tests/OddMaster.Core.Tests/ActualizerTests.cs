using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OddMaster.Core.Actualizer;
using OddMaster.Core.Config;
using OddMaster.Core.Statement;
using OddMaster.TestKit;

namespace OddMaster.Core.Tests
{
    /// <summary>Актуализатор 2004 → 2024 на синтетической ДЕМО-карте (M7).</summary>
    public static class ActualizerTests
    {
        private const string DemoMap = "tests/fixtures/actualizer/map-demo.json";
        private const string Patterns = "tests/fixtures/actualizer/recognizer-patterns.json";
        private const string Legacy = "tests/fixtures/actualizer/legacy-scheme.json";

        private static (ActualizationEngine Engine, LegacyScheme Scheme, MigrationMap Map) Fixture()
        {
            var root = Repo.Root();
            var cfg = ConfigLoader.Load(root);
            var map = ConfigLoader.LoadMigrationMap(root, DemoMap);
            var recognizer = ConfigLoader.LoadRecognizer(root, Patterns);
            var scheme = ConfigLoader.LoadLegacyScheme(root, Legacy);
            return (new ActualizationEngine(recognizer, map, cfg.Catalog), scheme, map);
        }

        [OddFact("M7-A01: боевая карта замен пустая, ДЕМО-карта помечена")]
        public static void BattleMapIsEmpty()
        {
            var root = Repo.Root();
            var battle = ConfigLoader.Load(root).MigrationMap;
            Assert.Equal(0, battle.Rules.Count);
            Assert.False(battle.IsDemo);
            Assert.Contains("GOST-018", string.Join(" ", battle.Markers));
            var (_, _, demo) = Fixture();
            Assert.True(demo.IsDemo);
            Assert.Equal(4, demo.Rules.Count);
        }

        [OddFact("M7-A02: отчёт содержит все шесть категорий")]
        public static void AllSixCategories()
        {
            var (engine, scheme, _) = Fixture();
            var report = engine.Run(scheme);
            foreach (MigrationCategory category in Enum.GetValues(typeof(MigrationCategory)).Cast<MigrationCategory>())
                Assert.True(report.Counts[category] > 0, "нет категории " + category);
        }

        [OddFact("M7-A03: конфликт правил → NeedsDecision с обоих идентификаторами")]
        public static void ConflictNeedsDecision()
        {
            var (engine, scheme, _) = Fixture();
            var item = engine.Run(scheme).Items.First(i => i.ElementId == "E03");
            Assert.Equal(MigrationCategory.NeedsDecision, item.Category);
            Assert.Contains("DEMO-R2", item.RuleId ?? "");
            Assert.Contains("DEMO-R4", item.RuleId ?? "");
            Assert.Contains("Нужно решение человека", item.Note);
        }

        [OddFact("M7-A04: баннер прямо говорит о ДЕМО и несверенной карте")]
        public static void BannerIsExplicit()
        {
            var (engine, scheme, _) = Fixture();
            var report = engine.Run(scheme);
            Assert.Contains("ДЕМО", report.Banner);
            Assert.Contains("не сверена", report.Banner);
            Assert.Contains("ДЕМО", report.ToMarkdown());
        }

        [OddFact("M7-A05: с пустой боевой картой нет замен и отмен")]
        public static void EmptyBattleMapDoesNotReplace()
        {
            var root = Repo.Root();
            var cfg = ConfigLoader.Load(root);
            var recognizer = ConfigLoader.LoadRecognizer(root, Patterns);
            var scheme = ConfigLoader.LoadLegacyScheme(root, Legacy);
            var report = new ActualizationEngine(recognizer, cfg.MigrationMap, cfg.Catalog).Run(scheme);
            Assert.Equal(0, report.Counts[MigrationCategory.Replaced]);
            Assert.Equal(0, report.Counts[MigrationCategory.Removed]);
            Assert.Equal(0, report.Counts[MigrationCategory.NeedsDecision]);
            Assert.False(report.IsDemo);
            Assert.True(report.Counts[MigrationCategory.Unknown] > 0);
        }

        [OddFact("M7-A06: golden-файлы отчёта актуализатора")]
        public static void GoldenActualizationReport()
        {
            var (engine, scheme, _) = Fixture();
            var report = engine.Run(scheme);
            Golden.CheckText("tests/OddMaster.Core.Tests/Approved/actualization_report.md", report.ToMarkdown());
            Golden.CheckText("tests/OddMaster.Core.Tests/Approved/actualization_report.csv", report.ToCsv());
        }

        [OddFact("M7-A07: два прогона и две культуры дают одинаковый отчёт")]
        public static void DeterministicAndCultureInvariant()
        {
            var (engine, scheme, _) = Fixture();
            var saved = CultureInfo.CurrentCulture;
            try
            {
                var results = new List<string>();
                foreach (var name in new[] { "ru-RU", "en-US" })
                {
                    CultureInfo.CurrentCulture = new CultureInfo(name);
                    var report = engine.Run(scheme);
                    results.Add(report.ToMarkdown() + "|" + report.ToCsv());
                }
                Assert.Equal(results[0], results[1]);
                Assert.Equal(engine.Run(scheme).ToMarkdown(), engine.Run(scheme).ToMarkdown());
            }
            finally { CultureInfo.CurrentCulture = saved; }
        }

        [OddFact("M7-A08: CSV-отчёт с BOM и CRLF")]
        public static void ActualizationCsv()
        {
            var (engine, scheme, _) = Fixture();
            var csv = engine.Run(scheme).ToCsv();
            Assert.True(CsvWriter.HasBom(csv));
            Assert.Contains("\r\n", csv);
            Assert.Contains("NeedsDecision", csv);
            Assert.NotContains("2026-", csv);
        }

        [OddFact("M7-A09: неизвестный блок → Unknown, шаблон NONE")]
        public static void UnrecognizedBlock()
        {
            var (engine, scheme, _) = Fixture();
            var item = engine.Run(scheme).Items.First(i => i.ElementId == "E07");
            Assert.Equal(MigrationCategory.Unknown, item.Category);
            Assert.Equal("NONE", item.PatternId);
            Assert.Contains("Не распознано", item.Note);
        }

        [OddFact("M7-A10: номер из каталога 2024 без правила карты → Unchanged")]
        public static void KnownSignIsUnchanged()
        {
            var (engine, scheme, _) = Fixture();
            var item = engine.Run(scheme).Items.First(i => i.ElementId == "E05");
            Assert.Equal(MigrationCategory.Unchanged, item.Category);
            Assert.Equal("2.4", item.ResultSigns);
        }
    }
}
