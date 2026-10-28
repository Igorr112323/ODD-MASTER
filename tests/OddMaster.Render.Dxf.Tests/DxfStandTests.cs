using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OddMaster.Core.Generation;
using OddMaster.Core.Geometry;
using OddMaster.Core.Scheme;
using OddMaster.Core.Statement;
using OddMaster.Demo;
using OddMaster.Render.Dxf;
using OddMaster.TestKit;
using Xunit;
using IoPath = System.IO.Path;

namespace OddMaster.Render.Dxf.Tests
{
    /// <summary>Стенд целиком: схема, таблица ведомости, палитра и правила зависимостей (M13-D07…D10, D13).</summary>
    public sealed class DxfStandTests
    {
        private static Dictionary<string, SignGeometry> Geometries(SchemeModel scheme)
        {
            var generator = new SignGenerator(Stand.Config);
            var result = new Dictionary<string, SignGeometry>(StringComparer.Ordinal);
            foreach (var post in scheme.Posts)
                foreach (var sign in post.Mounts)
                {
                    if (result.ContainsKey(sign.StatementKey)) continue;
                    var parameters = sign.ParameterKind != null && sign.ParameterValue.HasValue
                        ? new Dictionary<string, decimal> { [sign.ParameterKind] = sign.ParameterValue.Value }
                        : null;
                    var generated = generator.Generate(sign.Number.ToString(), sign.SizeClass, parameters);
                    if (generated.IsSuccess) result[sign.StatementKey] = generated.Geometry!;
                }
            return result;
        }

        [Fact(DisplayName = "M13-D07 Render.Dxf не знает CAD-API: только строки DXF")]
        public void D07_NoCadApi()
        {
            var root = IoPath.Combine(Repo.Root(), "src/OddMaster.Render.Dxf");
            var files = Directory.GetFiles(root, "*.cs");
            TestAssert.True(files.Length >= 6, "в проекте Render.Dxf должно быть минимум 6 файлов");
            // Запрещены обращения к API САПР, а не упоминания в прозе: ядро и рендер не должны
            // ссылаться на пространства имён MultiCAD/Teigha/Autodesk и на Avalonia.
            var forbidden = new[]
            {
                "using MultiCAD", "using Multicad", "using Teigha", "using Autodesk",
                "MultiCAD.DatabaseServices", "MultiCAD.Geometry", "Avalonia.Controls"
            };
            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                foreach (var word in forbidden)
                    TestAssert.NotContains(word, text, "файл " + IoPath.GetFileName(file) + " не должен ссылаться на " + word);
            }
            var ports = File.ReadAllText(IoPath.Combine(Repo.Root(), "src/OddMaster.Core/Ports/ICadBackend.cs"));
            foreach (var word in forbidden) TestAssert.NotContains(word, ports);
        }

        [Fact(DisplayName = "M13-D08 Текст каждой ячейки ведомости помещается в свою ячейку")]
        public void D08_TableTextFits()
        {
            var (scheme, _) = DemoSchemeLoader.Load(Repo.Root());
            var demo = new DemoRunner(Repo.Root(), Stand.Config);
            var statement = TsoddStatement.Build(scheme, Stand.Config.Catalog);
            var table = demo.BuildStatementTable(statement);
            TestAssert.True(table.Cells.Count > 0, "таблица должна содержать ячейки");
            foreach (var cell in table.Cells)
            {
                var box = cell.Layout.Box();
                TestAssert.True(box.MinX >= cell.Box.MinX - 1e-6,
                    "текст «" + cell.Text + "» выходит левее ячейки");
                TestAssert.True(box.MaxX <= cell.Box.MaxX + 1e-6,
                    "текст «" + cell.Text + "» выходит правее ячейки");
                TestAssert.True(box.MinY >= cell.Box.MinY - 1e-6,
                    "текст «" + cell.Text + "» выходит ниже ячейки");
                TestAssert.True(box.MaxY <= cell.Box.MaxY + 1e-6,
                    "текст «" + cell.Text + "» выходит выше ячейки");
            }
        }

        [Fact(DisplayName = "M13-D09 Сетка ведомости: полный комплект линий и подписей в DXF")]
        public void D09_TableGrid()
        {
            var (scheme, _) = DemoSchemeLoader.Load(Repo.Root());
            var demo = new DemoRunner(Repo.Root(), Stand.Config);
            var statement = TsoddStatement.Build(scheme, Stand.Config.Catalog);
            var table = demo.BuildStatementTable(statement);
            var spec = Stand.Config.StatementTable;
            var expectedLines = (table.RowCount + 1) + (table.ColumnCount + 1);
            TestAssert.Equal(expectedLines, table.Lines.Count, "линий сетки: горизонтали + вертикали");
            TestAssert.Equal((statement.Rows.Count + 1) * spec.Columns.Count, table.Cells.Count,
                "ячеек: шапка + строки данных");

            var backend = StatementDxfWriter.Build(table, spec, new Point2(0, 0), Stand.Config.Drawing.DxfFlattenSagittaMm);
            var text = backend.Document.Build();
            var types = DxfPairs.EntityTypes(text);
            TestAssert.Equal(expectedLines, types.Count(t => t == "LINE"), "каждая линия сетки — сущность LINE");
            var texts = DxfPairs.Texts(text).Select(DxfTextCodec.Decode).ToList();
            var nonEmptyCells = table.Cells.Count(c => c.Text.Length > 0);
            var expectedTexts = nonEmptyCells + (table.Title.Length > 0 ? 1 : 0);
            TestAssert.Equal(expectedTexts, texts.Count, "подписи непустых ячеек + заголовок таблицы");
            TestAssert.Equal(table.Cells.Count, table.Cells.Count(c => c.Layout.HeightMm > 0),
                "кегль рассчитывается для всех ячеек, даже пустых");
            TestAssert.True(texts.Contains("№"), "в шапке должен быть столбец «№»");
            TestAssert.True(texts.Any(t => t.Contains("ведомость", StringComparison.OrdinalIgnoreCase))
                || texts.Any(t => t.Contains("Ведомость", StringComparison.Ordinal)),
                "заголовок ведомости должен присутствовать");
        }

        [Fact(DisplayName = "M13-D10 Схема в DXF: ось, опоры, стопки знаков и баннер ЧЕРНОВИК")]
        public void D10_Scheme()
        {
            var (scheme, _) = DemoSchemeLoader.Load(Repo.Root());
            var geometries = Geometries(scheme);
            TestAssert.True(geometries.Count > 0, "геометрии знаков должны построиться");
            var backend = SchemeDxfWriter.Build(scheme, geometries, Stand.Config.Drawing, Stand.Config.Typography);
            var text = backend.Document.Build();
            var layers = backend.Layers.Select(l => l.Name).ToList();
            foreach (var layer in new[] { "AXIS", "STATION", "POST", "LABEL", "BANNER" })
                TestAssert.True(layers.Contains(layer), "в схеме должен быть слой " + layer);
            var types = DxfPairs.EntityTypes(text);
            TestAssert.True(types.Count(t => t == "POLYLINE") >= 1, "ось — полилиния");
            var decoded = DxfPairs.Texts(text).Select(DxfTextCodec.Decode).ToList();
            TestAssert.True(decoded.Any(t => t.Contains("ЧЕРНОВИК", StringComparison.Ordinal)),
                "баннер ЧЕРНОВИК обязателен");
            foreach (var post in scheme.Posts)
                TestAssert.True(decoded.Any(t => t.StartsWith(post.Id + ":", StringComparison.Ordinal)),
                    "подпись опоры " + post.Id + " должна быть в чертеже");

            var fewer = new SchemeModel(scheme.Id, scheme.Alignment, scheme.Posts.Take(2));
            var small = SchemeDxfWriter.Build(fewer, geometries, Stand.Config.Drawing, Stand.Config.Typography);
            TestAssert.True(small.EntityCount < backend.EntityCount,
                "меньше опор — меньше сущностей: " + small.EntityCount + " < " + backend.EntityCount);
        }

        [Fact(DisplayName = "M13-D13 Палитра ACI покрывает все цвета каталога знаков")]
        public void D13_PaletteCoversCatalog()
        {
            var missing = new List<string>();
            foreach (var sign in Stand.Config.Catalog.Signs)
                foreach (var color in sign.Colors.Values)
                    if (!DxfColorMap.IsKnown(color) && !missing.Contains(color)) missing.Add(color);
            TestAssert.Equal(0, missing.Count, "нет цвета в палитре ACI: " + string.Join(", ", missing));
            TestAssert.True(DxfColorMap.Names.Count >= 6, "палитра должна содержать основные цвета");
            TestAssert.Equal(0, DxfColorMap.Normalize(0), "0 — цвет «по слою»");
            TestAssert.Equal(255, DxfColorMap.Normalize(255), "255 — верхняя граница ACI");
            TestAssert.Equal(7, DxfColorMap.Normalize(300), "выход за диапазон ACI приводится к «по фону»");
        }
    }
}
