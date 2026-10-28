using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using OddMaster.Core.Config;
using OddMaster.Core.Statement;
using OddMaster.Demo;
using OddMaster.TestKit;
using IoPath = System.IO.Path;

namespace OddMaster.Render.Svg.Tests
{
    /// <summary>Сквозные демо-артефакты и детерминизм (M8).</summary>
    public class DemoArtifactsTests
    {
        private static readonly string[] Expected =
        {
            "README.md", "scheme.svg", "statement.csv", "statement.md",
            "coordinates.csv", "coordinates.md", "reactor_before.md", "reactor_after.md",
            "reactor_diff.txt", "audit_report.csv", "audit_report.md",
            "actualization_report.csv", "actualization_report.md", "config_validation.md"
        };

        private static string Demo(string relative) => IoPath.Combine(Repo.Root(), "docs/demo", relative);

        private static void RunDemo()
        {
            var root = Repo.Root();
            var demo = new DemoRunner(root, ConfigLoader.Load(root));
            demo.Signs();
            demo.Scheme();
            demo.Check();
            demo.Actualize();
        }

        [Fact(DisplayName = "M8-S01: все ожидаемые артефакты существуют")]
        public void AllArtifactsExist()
        {
            foreach (var file in Expected)
                TestAssert.True(File.Exists(Demo(file)), "нет файла docs/demo/" + file);
            TestAssert.True(Directory.GetFiles(Demo("signs"), "*.svg").Length >= 20);
        }

        [Fact(DisplayName = "M8-S02: scheme.svg — корректный XML со слоями и баннером")]
        public void SchemeSvgStructure()
        {
            var svg = File.ReadAllText(Demo("scheme.svg"));
            var doc = XDocument.Parse(svg);
            TestAssert.Equal("svg", doc.Root!.Name.LocalName);
            TestAssert.Contains("id=\"Alignment\"", svg);
            TestAssert.Contains("id=\"Post-P1\"", svg);
            TestAssert.Contains("id=\"StationTicks\"", svg);
            TestAssert.Contains("id=\"DraftBanner\"", svg);
            TestAssert.Contains("ЧЕРНОВИК", svg);
            TestAssert.Contains("odd:scheme", svg);
            TestAssert.Contains("ПК", svg);
            TestAssert.NotContains("2026-", svg);
        }

        [Fact(DisplayName = "M8-S03: повторный прогон all даёт байт-в-байт те же файлы")]
        public void DemoIsByteDeterministic()
        {
            var hashes = Snapshot();
            RunDemo();
            var second = Snapshot();
            TestAssert.Equal(hashes.Count, second.Count);
            foreach (var pair in hashes)
                TestAssert.Equal(pair.Value, second[pair.Key]);
        }

        private static Dictionary<string, string> Snapshot()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var root = Demo("");
            foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
            {
                using var sha = SHA256.Create();
                var bytes = File.ReadAllBytes(file);
                result[file.Substring(root.Length).Replace('\\', '/')] =
                    BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "", StringComparison.Ordinal);
            }
            return result;
        }

        [Fact(DisplayName = "M8-S04: reactor_diff показывает сдвиг X на +50 без смены пикетов")]
        public void ReactorDiffContent()
        {
            var diff = File.ReadAllText(Demo("reactor_diff.txt"));
            TestAssert.Contains("X 50 → 100", diff);
            TestAssert.Contains("X 350 → 400", diff);
            TestAssert.NotContains("пикет", diff);
        }

        [Fact(DisplayName = "M8-S05: golden scheme.svg, reactor_diff.txt и config_validation.md")]
        public void GoldenDemoArtifacts()
        {
            Golden.CheckText("tests/OddMaster.Render.Svg.Tests/Approved/scheme.svg",
                File.ReadAllText(Demo("scheme.svg")));
            Golden.CheckText("tests/OddMaster.Render.Svg.Tests/Approved/reactor_diff.txt",
                File.ReadAllText(Demo("reactor_diff.txt")));
            Golden.CheckText("tests/OddMaster.Render.Svg.Tests/Approved/config_validation.md",
                File.ReadAllText(Demo("config_validation.md")));
        }

        [Fact(DisplayName = "M8-S06: ведомость и журнал координат — CSV с BOM")]
        public void CsvArtifacts()
        {
            foreach (var file in new[] { "statement.csv", "coordinates.csv", "audit_report.csv", "actualization_report.csv" })
            {
                var bytes = File.ReadAllBytes(Demo(file));
                TestAssert.True(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                    file + " без BOM");
                var csv = Encoding.UTF8.GetString(bytes);
                TestAssert.Contains(";", csv);
            }
            TestAssert.Contains("GOST-011", File.ReadAllText(Demo("statement.md")));
        }

        [Fact(DisplayName = "M8-S07: config_validation.md находит четыре несостыковки и не содержит ошибок конфигов")]
        public void ConfigValidationReport()
        {
            var text = File.ReadAllText(Demo("config_validation.md"));
            TestAssert.Contains("Ошибки: 0", text);
            TestAssert.Contains("GAP-001", text);
            TestAssert.Contains("GAP-002A", text);
            TestAssert.Contains("GAP-002B", text);
            TestAssert.Contains("GAP-003", text);
            TestAssert.Contains("GAP-004", text);
        }

        [Fact(DisplayName = "M8-S08: отчёты аудитора и актуализатора честно помечены")]
        public void ReportsAreHonest()
        {
            TestAssert.Contains("ЧЕРНОВИК", File.ReadAllText(Demo("audit_report.md")));
            TestAssert.Contains("не проверено по ГОСТ", File.ReadAllText(Demo("audit_report.csv")));
            TestAssert.Contains("ДЕМО", File.ReadAllText(Demo("actualization_report.md")));
            TestAssert.Contains("NeedsDecision", File.ReadAllText(Demo("actualization_report.csv")));
        }

        [Fact(DisplayName = "M8-S09: в артефактах нет дат, GUID и случайных чисел")]
        public void NoTimestampsOrGuids()
        {
            foreach (var file in Directory.GetFiles(Demo(""), "*", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                TestAssert.NotContains("2026-", text, IoPath.GetFileName(file));
                TestAssert.NotContains("Guid.NewGuid", text, IoPath.GetFileName(file));
            }
        }
    }
}
