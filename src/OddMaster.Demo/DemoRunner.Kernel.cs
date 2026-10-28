using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OddMaster.Core.Actualizer;
using OddMaster.Core.Config;
using OddMaster.Core.Document;
using OddMaster.Core.Geometry;
using OddMaster.Core.Norms;
using OddMaster.Core.Scheme;
using OddMaster.Core.Zones;

namespace OddMaster.Demo
{
    /// <summary>P3: демо-артефакты модели ядра. Сценарий синтетический, помечен ДЕМО.</summary>
    public sealed partial class DemoRunner
    {
        /// <summary>Строит ядерные демо-артефакты для синтетического участка.</summary>
        public void Kernel()
        {
            var scheme = BuildKernelScheme();
            var doc = SchemeDocument.FromScheme(scheme, "ДЕМО: центральная улица Краснодара (синтетика)");
            DemoFiles.WriteText(_root, "docs/demo/kernel_document.json", doc.ToJson());

            var live = LiveStatement.Build(scheme, _config.Catalog);
            DemoFiles.WriteText(_root, "docs/demo/kernel_statement.md", live.ToMarkdown());
            DemoFiles.WriteText(_root, "docs/demo/kernel_statement.csv", live.ToCsv());

            // Одно событие: меняем типоразмер одного знака — дельта должна затронуть 1–2 строки.
            var firstPost = scheme.Posts[0];
            var oldSign = firstPost.Mounts[0];
            var newSign = new SignInstance(oldSign.Id, oldSign.Number, "III",
                oldSign.ParameterKind, oldSign.ParameterValue, oldSign.Direction);
            var delta = live.Apply(oldSign, newSign);
            DemoFiles.WriteText(_root, "docs/demo/kernel_delta.md",
                "# Дельта ведомости при одном изменении (ДЕМО)\n\n" + delta.ToMarkdown());

            var before = SchemeReactor.Compute(scheme);
            var shifted = new SchemeModel(scheme.Id,
                scheme.Alignment.Shift(new Vector2(50, 0)), scheme.Posts);
            var after = SchemeReactor.Compute(shifted);
            DemoFiles.WriteText(_root, "docs/demo/kernel_journal_before.md",
                "# Журнал координат до сдвига оси (ДЕМО)\n\n" + before.ToMarkdown());
            DemoFiles.WriteText(_root, "docs/demo/kernel_journal_after.md",
                "# Журнал координат после сдвига оси на (50; 0) (ДЕМО)\n\n" + after.ToMarkdown());
            DemoFiles.WriteText(_root, "docs/demo/kernel_journal_diff.txt", before.Diff(after));

            DemoFiles.WriteText(_root, "docs/demo/kernel_legend.md",
                LegendModel.ToMarkdown(LegendModel.Build(scheme, _config.Catalog)));
            DemoFiles.WriteText(_root, "docs/demo/kernel_sheet.json",
                new SheetModel("A3", 1000.0, 10.0).ToJson());

            var zones = ZoneRuleSet.Load(_root);
            var zoneSb = new StringBuilder();
            zoneSb.AppendLine("# Зоны действия и парность (ДЕМО, данные Unverified)");
            zoneSb.AppendLine();
            zoneSb.AppendLine("> " + zones.Note);
            zoneSb.AppendLine();
            zoneSb.AppendLine("| Табличка | Роль | Начало | Конец | Статус |");
            zoneSb.AppendLine("|---|---|---|---|---|");
            foreach (var plate in zones.Plates)
                zoneSb.AppendLine("| " + plate.Number + " | " + plate.Role + " | "
                    + string.Join(", ", plate.StartSigns) + " | " + string.Join(", ", plate.EndSigns)
                    + " | " + plate.Status + " |");
            zoneSb.AppendLine();
            zoneSb.AppendLine("Пары: " + string.Join("; ", zones.Pairing) + ".");
            DemoFiles.WriteText(_root, "docs/demo/kernel_zones.md", zoneSb.ToString());

            var map = ConfigLoader.LoadMigrationMap(_root, "tests/fixtures/actualizer/map-demo.json");
            var recognizer = ConfigLoader.LoadRecognizer(_root, "tests/fixtures/actualizer/recognizer-patterns.json");
            var legacy = ConfigLoader.LoadLegacyScheme(_root, "tests/fixtures/actualizer/legacy-scheme.json");
            var actualization = new ActualizationEngine(recognizer, map, _config.Catalog).Run(legacy);
            DemoFiles.WriteText(_root, "docs/demo/kernel_actualization.md", actualization.ToMarkdown());
            DemoFiles.WriteText(_root, "docs/demo/kernel_actualization.csv", actualization.ToCsv());

            var diffCsv = JsonConfig.ReadUtf8IfExists(_root, "data/diff-2004-2024.template.csv");
            var diffMap = DiffMapImporter.Import(diffCsv);
            DemoFiles.WriteText(_root, "docs/demo/kernel_diff_map.md",
                "# Импорт таблицы различий (ДЕМО-шаблон)\n\nПравил из CSV: "
                + diffMap.Rules.Count.ToString(CultureInfo.InvariantCulture) + ".\n\n> "
                + diffMap.Banner + "\n");
        }

        private static SchemeModel BuildKernelScheme()
        {
            var axis = new OddMaster.Core.Alignment.Alignment(new[]
            {
                new Point2(0, 0), new Point2(400, 0), new Point2(400, 250), new Point2(900, 250)
            }, 1000.0);
            var posts = new List<Post>();
            var signs = new[]
            {
                ("3.24", "speed", 40m), ("3.25", "speed", 40m), ("2.4", null, 0m),
                ("2.5", null, 0m), ("5.19.1", null, 0m), ("8.2.1", "distance", 50m),
                ("3.27", null, 0m), ("6.4", null, 0m), ("5.41", null, 0m),
                ("5.42", null, 0m), ("6.2.1", "speed", 30m), ("8.1.1", "distance", 100m)
            };
            for (int i = 0; i < signs.Length; i++)
            {
                var post = new Post("K" + (i + 1).ToString(CultureInfo.InvariantCulture),
                    1050 + i * 60, (i % 2 == 0 ? 5.0 : -5.0));
                var (number, kind, value) = signs[i];
                post.Add(new SignInstance(post.Id + "-S1", SignNumber.Parse(number), "II",
                    kind, kind == null ? (decimal?)null : value));
                posts.Add(post);
            }
            return new SchemeModel("DEMO-KERNEL-01", axis, posts);
        }
    }
}
