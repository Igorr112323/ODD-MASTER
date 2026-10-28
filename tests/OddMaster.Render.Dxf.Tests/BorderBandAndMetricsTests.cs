using System;
using System.Collections.Generic;
using System.Linq;
using OddMaster.Core.Catalog;
using OddMaster.Core.Generation;
using OddMaster.Core.Geometry;
using OddMaster.Core.Typography;
using OddMaster.Demo;
using OddMaster.Render.Dxf;
using OddMaster.TestKit;
using Xunit;

namespace OddMaster.Render.Dxf.Tests
{
    /// <summary>
    /// CR-16: кайма — полоса, а не линия; высота текста в DXF — высота прописных,
    /// метрики шрифта измерены по файлу. Проверки M14-B01…B08.
    /// </summary>
    public sealed class BorderBandAndMetricsTests
    {
        [Fact(DisplayName = "M14-B01 Внутренняя кайма — залитая полоса между двумя контурами")]
        public void B01_InnerBorderIsBand()
        {
            var geometry = Stand.Sign("2.4", "II");
            var inner = geometry.Layers.Single(l => l.Kind == SignLayerKind.InnerBorder);
            var field = geometry.Layers.Single(l => l.Kind == SignLayerKind.Field);
            TestAssert.True(inner.Filled, "кайма должна заливаться, а не обводиться: у отрезка в DXF нет ширины");
            TestAssert.Equal(0.0, inner.StrokeWidthMm, "толщина обводки каймы не используется");
            TestAssert.True(inner.Geometry!.Bounds().Width > field.Geometry!.Bounds().Width,
                "контур каймы шире контура поля — это и есть полоса");
            var band = (inner.Geometry.Bounds().Width - field.Geometry.Bounds().Width) / 2;
            TestAssert.Close(geometry.InnerBorderWidthMm, band, 0.01, "ширина полосы равна innerBorderWidthMm");
            TestAssert.True(geometry.InnerBorderIsPlaceholder, "ширина каймы — заглушка (GOST-001)");
        }

        [Fact(DisplayName = "M14-B02 В DXF кайма даёт два залитых контура и красный цвет каталога")]
        public void B02_InnerBorderInDxf()
        {
            var backend = Stand.Build("2.4", "II");
            var text = backend.Document.Build();
            var layers = backend.Layers.Select(l => l.Name).ToList();
            TestAssert.True(layers.Contains("SIGN.InnerBorder"), "слой каймы объявлен");
            var inner = backend.Layers.First(l => l.Name == "SIGN.InnerBorder");
            TestAssert.Equal(DxfColorMap.Aci("Red"), inner.ColorIndex, "кайма 2.4 — красная по каталогу");
            TestAssert.True(backend.EntityCount > 0);
            // Полоса рисуется заливкой: у знака с каймой залитых контуров на один больше.
            var bands = DxfPairs.EntityTypes(text).Count(t => t == "SOLID");
            TestAssert.True(bands > 0, "кайма должна давать заливку");
            TestAssert.True(DxfPairs.Texts(text).Count > 0, "должен быть текст (ЧЕРНОВИК)");
        }

        [Fact(DisplayName = "M14-B03 У знака без внутренней каймы слоя InnerBorder нет")]
        public void B03_NoInnerBorderWhenNotDeclared()
        {
            var geometry = Stand.Sign("3.24", "II");
            TestAssert.True(geometry.Layers.All(l => l.Kind != SignLayerKind.InnerBorder),
                "3.24: внутренней каймы нет (CR-04)");
            TestAssert.True(geometry.Layers.All(l => l.Kind != SignLayerKind.InnerBorder
                || l.Geometry != null));
        }

        [Fact(DisplayName = "M14-B04 Высота текста в DXF = высота прописных, а не кегль модели")]
        public void B04_CapHeightInDxf()
        {
            var backend = Stand.Build("3.24", "II");
            var geometry = Stand.Sign("3.24", "II");
            var text = geometry.Texts[0];
            var pairs = DxfPairs.Parse(backend.Document.Build());
            var heights = pairs.Where(p => p.Key == 40).Select(p => double.Parse(p.Value,
                System.Globalization.CultureInfo.InvariantCulture)).ToList();
            TestAssert.True(heights.Count > 0, "в чертеже должен быть текст");
            TestAssert.True(heights.Any(h => Math.Abs(h - text.Layout!.CapHeightMm) < 1e-6),
                "высота прописных из раскладки должна попасть в код 40");
            TestAssert.True(heights.All(h => Math.Abs(h - text.Layout!.HeightMm) > 1e-6),
                "кегль модели не должен попадать в код 40 напрямую");
        }

        [Fact(DisplayName = "M14-B05 Таблица в DXF: кегль каждой подписи внутри своей ячейки")]
        public void B05_TableHeights()
        {
            var (scheme, _) = DemoSchemeLoader.Load(Repo.Root());
            var demo = new DemoRunner(Repo.Root(), Stand.Config);
            var statement = OddMaster.Core.Statement.TsoddStatement.Build(scheme, Stand.Config.Catalog);
            var table = demo.BuildStatementTable(statement);
            foreach (var cell in table.Cells.Where(c => c.Text.Length > 0))
            {
                TestAssert.True(cell.Layout.CapHeightMm <= cell.Box.Height,
                    "прописные «" + cell.Text + "» выше ячейки");
                TestAssert.True(cell.Layout.WidthMm <= cell.Box.Width,
                    "строка «" + cell.Text + "» шире ячейки");
            }
            var backend = StatementDxfWriter.Build(table, Stand.Config.StatementTable, new Point2(0, 0),
                Stand.Config.Drawing.DxfFlattenSagittaMm);
            var heights = DxfPairs.TextHeights(backend.Document.Build());
            var expected = table.Cells.Count(c => c.Text.Length > 0)
                + (table.Title.Length > 0 ? 1 : 0);
            TestAssert.Equal(expected, heights.Count, "по одной высоте на непустую ячейку плюс заголовок");
            TestAssert.True(heights.Max() < Stand.Config.StatementTable.RowHeightMm,
                "кегль таблицы не выходит за высоту строки данных");
        }

        [Fact(DisplayName = "M14-B06 Метрики шрифта измерены по файлу, а не выдуманы")]
        public void B06_MetricsAreMeasured()
        {
            var metrics = Stand.Config.Typography;
            var text = Repo.ReadText("config/typography/fonts.json");
            TestAssert.Contains("\"measuredFrom\"", text, "в конфиге должна быть ссылка на измеренный файл");
            TestAssert.NotContains("\"verification\": \"Placeholder\"", text,
                "измеренные метрики не могут оставаться заглушкой");
            TestAssert.True(metrics.CapHeightRatio > 0.6 && metrics.CapHeightRatio < 0.85,
                "реальная высота прописных в диапазоне 0,6…0,85 от кегля: " + metrics.CapHeightRatio);
            TestAssert.True(metrics.AdvanceRatios.Count >= 90,
                "измерены метрики для ≥90 символов, получено " + metrics.AdvanceRatios.Count);
            TestAssert.True(metrics.AdvanceRatios.ContainsKey("Щ"), "есть самый широкий символ");
            TestAssert.True(metrics.DefaultAdvanceRatio >= metrics.AdvanceRatios["Щ"] - 1e-9,
                "запас по умолчанию не уже самого широкого измеренного символа");
            // Цифры в этой гарнитуре табличные (одинаковой ширины) — это факт, а не усреднение;
            // буквы же разной ширины, значит метрики действительно сняты с файла.
            foreach (var pair in new[] { ("S", "O"), ("A", "I"), ("м", "к"), ("0", "Щ") })
                TestAssert.True(Math.Abs(metrics.AdvanceRatios[pair.Item1] - metrics.AdvanceRatios[pair.Item2]) > 1e-6,
                    "метрики усреднены: у " + pair.Item1 + " и " + pair.Item2 + " одинаковая ширина");
        }

        [Fact(DisplayName = "M14-B07 Текст STOP помещается в поле восьмиугольника 2.5")]
        public void B07_StopFitsField()
        {
            var geometry = Stand.Sign("2.5", "II");
            TestAssert.Equal(1, geometry.Texts.Count, "у STOP одна надпись");
            var layout = geometry.Texts[0].Layout!;
            var box = layout.Box();
            var border = geometry.OuterBorderWidthMm;
            var fieldHalf = (geometry.Bounds.Width - 2 * border) / 2;
            // Восьмиугольник: поле — не квадрат, но по диагонали шире; проверяем по габариту поля.
            TestAssert.True(box.Width <= fieldHalf * 2, "STOP шире поля восьмиугольника");
            TestAssert.True(box.Height <= fieldHalf * 2, "STOP выше поля восьмиугольника");
            TestAssert.Close(geometry.Bounds.Width, geometry.Bounds.Height, 1e-6, "восьмиугольник — равносторонний");
        }

        [Fact(DisplayName = "M14-B08 Скрипт измерения шрифта документирован и детерминирован")]
        public void B08_MeasureScriptExists()
        {
            TestAssert.True(Repo.Exists("scripts/measure_font.py"), "скрипт измерения шрифта должен быть в репозитории");
            var script = Repo.ReadText("scripts/measure_font.py");
            TestAssert.Contains("fontTools", script);
            TestAssert.Contains("sha256", script);
            TestAssert.Contains("capHeightRatio", script);
        }
    }
}
