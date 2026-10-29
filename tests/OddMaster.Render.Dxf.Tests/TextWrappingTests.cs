using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OddMaster.Core.Config;
using OddMaster.Core.Document;
using OddMaster.Core.Geometry;
using OddMaster.Core.Norms;
using OddMaster.Core.Scheme;
using OddMaster.Core.Statement;
using OddMaster.Core.Typography;
using OddMaster.Demo;
using OddMaster.Render.Dxf;
using OddMaster.TestKit;
using Xunit;
using IoPath = System.IO.Path;

namespace OddMaster.Render.Dxf.Tests
{
    /// <summary>
    /// CR-17: перенос текста по словам, «живой» пересчёт ведомости и контроль на чертеже.
    /// Проверки M15-W01…W08.
    /// </summary>
    public sealed class TextWrappingTests
    {
        private static FontMetrics Font => Stand.Config.Typography;

        [Fact(DisplayName = "M15-W01 Блок из нескольких строк целиком влезает в прямоугольник")]
        public void W01_BlockFitsHeight()
        {
            const double boxWidth = 120.0;
            const double boxHeight = 40.0;
            var block = TextEngine.LayoutBlock(
                new[] { "ЧЕРНОВИК:", "есть непроверенные параметры" }, boxWidth, boxHeight, Font,
                new Point2(0, 0));
            TestAssert.Equal(2, block.Count);
            var total = block.Max(l => l.Box().MaxY) - block.Min(l => l.Box().MinY);
            TestAssert.True(total <= boxHeight + 1e-6,
                "высота блока " + total.ToString("0.##", CultureInfo.InvariantCulture) +
                " мм больше отведённых " + boxHeight + " мм");
            foreach (var line in block)
                TestAssert.True(line.WidthMm <= boxWidth + 1e-6, "строка шире прямоугольника");
        }

        [Fact(DisplayName = "M15-W02 Перенос по словам: каждая строка влезает, кегль один")]
        public void W02_WrapInColumn()
        {
            var lines = TextEngine.LayoutWrapped("Номер знака по ГОСТ Р 52290-2024", 40, 20, Font,
                new Point2(0, 0));
            TestAssert.True(lines.Count >= 2, "длинный заголовок должен переноситься, строк: " + lines.Count);
            foreach (var line in lines)
                TestAssert.True(line.WidthMm <= 40 + 1e-6,
                    "строка «" + line.Value + "» шире колонки: " +
                    line.WidthMm.ToString("0.##", CultureInfo.InvariantCulture));
            TestAssert.Equal(1, lines.Select(l => Math.Round(l.HeightMm, 6)).Distinct().Count(),
                "кегль должен быть одинаковым во всех строках");
            TestAssert.Equal("Номер знака по ГОСТ Р 52290-2024",
                string.Join(" ", lines.Select(l => l.Value)), "слова не должны теряться");
        }

        [Fact(DisplayName = "M15-W03 Слово без пробелов режется по символам, а не вылезает")]
        public void W03_LongWordSplits()
        {
            var lines = TextEngine.LayoutWrapped("ЧЕРНОВИКДАННЫЕНЕСВЕРЕНЫ", 25, 15, Font, new Point2(0, 0));
            TestAssert.True(lines.Count >= 2, "длинное слово обязано разрезаться");
            foreach (var line in lines) TestAssert.True(line.WidthMm <= 25 + 1e-6, "кусок шире колонки");
            TestAssert.Equal("ЧЕРНОВИКДАННЫЕНЕСВЕРЕНЫ", string.Join("", lines.Select(l => l.Value)),
                "при разрыве символы не теряются и не дублируются");
        }

        [Fact(DisplayName = "M15-W04 Пустой текст и одиночное слово: без падения и без пустых строк")]
        public void W04_Degenerate()
        {
            TestAssert.Equal(0, TextEngine.LayoutWrapped("", 40, 20, Font, new Point2(0, 0)).Count);
            TestAssert.Equal(0, TextEngine.LayoutWrapped("   ", 40, 20, Font, new Point2(0, 0)).Count);
            var one = TextEngine.LayoutWrapped("Парковка", 60, 20, Font, new Point2(0, 0));
            TestAssert.Equal(1, one.Count);
            TestAssert.True(one[0].WidthMm <= 60 + 1e-6);
        }

        [Fact(DisplayName = "M15-W05 Ведомость: узкая колонка не даёт кегль ниже 2 мм")]
        public void W05_TableLegibility()
        {
            var (scheme, _) = DemoSchemeLoader.Load(Repo.Root());
            var demo = new DemoRunner(Repo.Root(), Stand.Config);
            var statement = TsoddStatement.Build(scheme, Stand.Config.Catalog);
            var table = demo.BuildStatementTable(statement);
            var smallest = table.Cells.SelectMany(c => c.Lines).Min(l => l.CapHeightMm);
            TestAssert.True(smallest >= 2.0,
                "высота прописных в таблице " + smallest.ToString("0.##", CultureInfo.InvariantCulture) +
                " мм: подпись нечитаема (нужен перенос, а не уменьшение кегля)");
        }

        [Fact(DisplayName = "M15-W06 DXF-стенд: live-before/live-after/control и их отчёт")]
        public void W06_ScenarioArtifacts()
        {
            var root = Repo.Root();
            var demo = new DemoRunner(root, ConfigLoader.Load(root));
            demo.Scenario();
            foreach (var file in new[] { "live-before.dxf", "live-after.dxf", "control.dxf", "README-scenario.md" })
                TestAssert.True(Repo.Exists("docs/demo/dxf/" + file), "нет docs/demo/dxf/" + file);
            var before = DxfPairs.EntityTypes(Repo.ReadText("docs/demo/dxf/live-before.dxf"));
            var after = DxfPairs.EntityTypes(Repo.ReadText("docs/demo/dxf/live-after.dxf"));
            TestAssert.True(after.Count > before.Count,
                "после добавления знака сущностей должно стать больше: " + before.Count + " → " + after.Count);
            TestAssert.True(before.Count(t => t == "TEXT") > 0, "в живой схеме есть подписи");
            var control = Repo.ReadText("docs/demo/dxf/control.dxf");
            var controlTexts = DxfPairs.Texts(control).Select(DxfTextCodec.Decode).ToList();
            TestAssert.True(controlTexts.Any(t => t.StartsWith("R00", StringComparison.Ordinal)),
                "на чертеже контроля должны быть подписи правил R001–R007");
            TestAssert.True(controlTexts.Any(t => t.Contains("ЧЕРНОВИК", StringComparison.Ordinal)),
                "чертёж контроля обязан нести баннер ЧЕРНОВИК");
            TestAssert.True(DxfPairs.EntityTypes(control).Count(t => t == "LINE") > 0,
                "выноски находок рисуются линиями");
        }

        [Fact(DisplayName = "M15-W07 Живой пересчёт: дельта на одно событие")]
        public void W07_LiveStatementDelta()
        {
            var (scheme, _) = DemoSchemeLoader.Load(Repo.Root());
            var catalog = Stand.Config.Catalog;
            var live = LiveStatement.Build(scheme, catalog);
            var before = live.Rows().Sum(r => r.Count);
            var added = new SignInstance("P2-NEW", SignNumber.Parse("3.24"), "II", "speed", 40m);
            var delta = live.Apply(null, added);
            TestAssert.Equal(1, delta.Changes.Count, "одно событие меняет одну строку");
            TestAssert.Equal(1, delta.Changes[0].After - delta.Changes[0].Before, "количество выросло на 1");
            TestAssert.Equal(before + 1, live.Rows().Sum(r => r.Count), "итог ведомости вырос на один знак");
            TestAssert.Contains("3.24|II|40", delta.Changes[0].Key);
        }

        [Fact(DisplayName = "M15-W08 Сценарий детерминирован: повторный прогон даёт те же байты")]
        public void W08_ScenarioDeterminism()
        {
            var root = Repo.Root();
            var demo = new DemoRunner(root, ConfigLoader.Load(root));
            demo.Scenario();
            var first = Snapshot();
            demo.Scenario();
            var second = Snapshot();
            TestAssert.Equal(first.Count, second.Count);
            foreach (var pair in first) TestAssert.Equal(pair.Value, second[pair.Key], pair.Key);
        }

        private static Dictionary<string, string> Snapshot()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in new[] { "live-before.dxf", "live-after.dxf", "control.dxf", "README-scenario.md" })
            {
                var bytes = File.ReadAllBytes(IoPath.Combine(Repo.Root(), "docs/demo/dxf", file));
                using var sha = System.Security.Cryptography.SHA256.Create();
                result[file] = Convert.ToBase64String(sha.ComputeHash(bytes));
            }
            return result;
        }
    }
}
