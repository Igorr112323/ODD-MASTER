using Xunit;
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
    public class ActualizerTests
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

        [Fact(DisplayName = "M7-A01: боевая карта замен пустая, ДЕМО-карта помечена")]
        public void BattleMapIsEmpty()
        {
            var root = Repo.Root();
            var battle = ConfigLoader.Load(root).MigrationMap;
            TestAssert.Equal(0, battle.Rules.Count);
            TestAssert.False(battle.IsDemo);
            TestAssert.Contains("GOST-018", string.Join(" ", battle.Markers));
            var (_, _, demo) = Fixture();
            TestAssert.True(demo.IsDemo);
            TestAssert.Equal(4, demo.Rules.Count);
        }

        [Fact(DisplayName = "M7-A11 (CR-11): >=12 элементов и >=2 в каждой категории")]
        public void FixtureHasTwoPerCategory()
        {
            var (engine, scheme, _) = Fixture();
            TestAssert.True(scheme.Elements.Count >= 12,
                "нужно >=12 элементов, есть " + scheme.Elements.Count.ToString(CultureInfo.InvariantCulture));
            var report = engine.Run(scheme);
            foreach (MigrationCategory category in Enum.GetValues(typeof(MigrationCategory)).Cast<MigrationCategory>())
                TestAssert.True(report.Counts[category] >= 2,
                    "в категории " + category + " меньше двух элементов: "
                    + report.Counts[category].ToString(CultureInfo.InvariantCulture));
            TestAssert.True(report.Items.Count >= 12);
        }

        [Fact(DisplayName = "M7-A02: отчёт содержит все шесть категорий")]
        public void AllSixCategories()
        {
            var (engine, scheme, _) = Fixture();
            var report = engine.Run(scheme);
            foreach (MigrationCategory category in Enum.GetValues(typeof(MigrationCategory)).Cast<MigrationCategory>())
                TestAssert.True(report.Counts[category] > 0, "нет категории " + category);
        }

        [Fact(DisplayName = "M7-A03: конфликт правил → NeedsDecision с обоих идентификаторами")]
        public void ConflictNeedsDecision()
        {
            var (engine, scheme, _) = Fixture();
            var item = engine.Run(scheme).Items.First(i => i.ElementId == "E03");
            TestAssert.Equal(MigrationCategory.NeedsDecision, item.Category);
            TestAssert.Contains("DEMO-R2", item.RuleId ?? "");
            TestAssert.Contains("DEMO-R4", item.RuleId ?? "");
            TestAssert.Contains("Нужно решение человека", item.Note);
        }

        [Fact(DisplayName = "M7-A04: баннер прямо говорит о ДЕМО и несверенной карте")]
        public void BannerIsExplicit()
        {
            var (engine, scheme, _) = Fixture();
            var report = engine.Run(scheme);
            TestAssert.Contains("ДЕМО", report.Banner);
            TestAssert.Contains("не сверена", report.Banner);
            TestAssert.Contains("ДЕМО", report.ToMarkdown());
        }

        [Fact(DisplayName = "M7-A05: с пустой боевой картой нет замен и отмен")]
        public void EmptyBattleMapDoesNotReplace()
        {
            var root = Repo.Root();
            var cfg = ConfigLoader.Load(root);
            var recognizer = ConfigLoader.LoadRecognizer(root, Patterns);
            var scheme = ConfigLoader.LoadLegacyScheme(root, Legacy);
            var report = new ActualizationEngine(recognizer, cfg.MigrationMap, cfg.Catalog).Run(scheme);
            TestAssert.Equal(0, report.Counts[MigrationCategory.Replaced]);
            TestAssert.Equal(0, report.Counts[MigrationCategory.Removed]);
            TestAssert.Equal(0, report.Counts[MigrationCategory.NeedsDecision]);
            TestAssert.False(report.IsDemo);
            TestAssert.True(report.Counts[MigrationCategory.Unknown] > 0);
        }

        [Fact(DisplayName = "M7-A06: golden-файлы отчёта актуализатора")]
        public void GoldenActualizationReport()
        {
            var (engine, scheme, _) = Fixture();
            var report = engine.Run(scheme);
            Golden.CheckText("tests/OddMaster.Core.Tests/Approved/actualization_report.md", report.ToMarkdown());
            Golden.CheckText("tests/OddMaster.Core.Tests/Approved/actualization_report.csv", report.ToCsv());
        }

        [Fact(DisplayName = "M7-A07: два прогона и две культуры дают одинаковый отчёт")]
        public void DeterministicAndCultureInvariant()
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
                TestAssert.Equal(results[0], results[1]);
                TestAssert.Equal(engine.Run(scheme).ToMarkdown(), engine.Run(scheme).ToMarkdown());
            }
            finally { CultureInfo.CurrentCulture = saved; }
        }

        [Fact(DisplayName = "M7-A08: CSV-отчёт с BOM и CRLF")]
        public void ActualizationCsv()
        {
            var (engine, scheme, _) = Fixture();
            var csv = engine.Run(scheme).ToCsv();
            TestAssert.True(CsvWriter.HasBom(csv));
            TestAssert.Contains("\r\n", csv);
            TestAssert.Contains("NeedsDecision", csv);
            TestAssert.NotContains("2026-", csv);
        }

        [Fact(DisplayName = "M7-A09: неизвестный блок → Unknown, шаблон NONE")]
        public void UnrecognizedBlock()
        {
            var (engine, scheme, _) = Fixture();
            var item = engine.Run(scheme).Items.First(i => i.ElementId == "E07");
            TestAssert.Equal(MigrationCategory.Unknown, item.Category);
            TestAssert.Equal("NONE", item.PatternId);
            TestAssert.Contains("Не распознано", item.Note);
        }

        [Fact(DisplayName = "M7-A10: номер из каталога 2024 без правила карты → Unchanged")]
        public void KnownSignIsUnchanged()
        {
            var (engine, scheme, _) = Fixture();
            var item = engine.Run(scheme).Items.First(i => i.ElementId == "E05");
            TestAssert.Equal(MigrationCategory.Unchanged, item.Category);
            TestAssert.Equal("2.4", item.ResultSigns);
        }
    }
}
