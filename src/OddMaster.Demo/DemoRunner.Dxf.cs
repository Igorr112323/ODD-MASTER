using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using OddMaster.Core.Catalog;
using OddMaster.Core.Config;
using OddMaster.Core.Geometry;
using OddMaster.Core.Statement;
using OddMaster.Render.Dxf;

namespace OddMaster.Demo
{
    /// <summary>Одна запись манифеста: файл, тип, ожидаемые факты для независимой проверки.</summary>
    internal sealed class DxfFileRecord
    {
        public DxfFileRecord(string path, string kind, string sign, string sizeClass, DxfBackend backend,
            BoundingBox? contentBounds, BoundingBox? nominalBounds)
        {
            Path = path; Kind = kind; Sign = sign; SizeClass = sizeClass;
            Backend = backend; ContentBounds = contentBounds; NominalBounds = nominalBounds;
        }

        public string Path { get; }
        public string Kind { get; }
        public string Sign { get; }
        public string SizeClass { get; }
        public DxfBackend Backend { get; }
        /// <summary>Фактический габарит сущностей в файле.</summary>
        public BoundingBox? ContentBounds { get; }
        /// <summary>Номинальный габарит знака по прил. Д (только для знаков).</summary>
        public BoundingBox? NominalBounds { get; }
    }

    /// <summary>
    /// Команда «dxf»: Linux-стенд. Пишет чертежи DXF, манифест ожидаемых фактов и README.
    /// Проверку манифеста независимым читателем делает scripts/dxf_verify.py (ezdxf).
    /// </summary>
    public sealed partial class DemoRunner
    {
        /// <summary>Пишет набор DXF-чертежей и манифест docs/demo/dxf/expected.json.</summary>
        public void Dxf()
        {
            var files = new List<DxfFileRecord>();
            var texts = new List<string>();

            // 1. Hello-чертёж: один параметрический объект ядра (знак 3.1 «Направление поворота направо», типоразмер I).
            var helloDefinition = _config.Catalog.Find("3.1");
            if (helloDefinition == null)
                throw new InvalidOperationException("В каталоге нет знака 3.1: hello-чертёж невозможен.");
            var hello = _generator.Generate(helloDefinition, "I", DefaultParameters(helloDefinition));
            if (hello.IsSuccess)
            {
                var backend = SignDxfWriter.Build(hello.Geometry!, _config.Drawing, _config.Typography);
                Add(files, "dxf/hello-3.1-I.dxf", "sign", "3.1", "I", backend,
                    backend.Document.Bounds(), hello.Geometry!.Bounds);
                texts.Add("hello-3.1-I.dxf: знак 3.1, типоразмер I, " + Num(hello.Geometry!.WidthMm) + "×" +
                    Num(hello.Geometry.HeightMm) + " мм");
            }

            // 2. По одному представителю каждой формы носителя: круг, треугольник, восьмиугольник, ромб, прямоугольник.
            var seenShapes = new HashSet<string>(StringComparer.Ordinal) { "Circle" };
            foreach (var sign in _config.Catalog.Signs)
            {
                if (seenShapes.Contains(sign.Shape.ToString())) continue;
                var sizeClass = AvailableClasses(_config.MainProfile, sign.Number).FirstOrDefault();
                if (sizeClass == null) continue;
                var result = _generator.Generate(sign, sizeClass, DefaultParameters(sign));
                if (!result.IsSuccess) continue;
                seenShapes.Add(sign.Shape.ToString());
                var name = "dxf/sign-" + sign.Number.ToString().Replace('.', '-') + "-" + sizeClass + ".dxf";
                var backend = SignDxfWriter.Build(result.Geometry!, _config.Drawing, _config.Typography);
                Add(files, name, "sign", sign.Number.ToString(), sizeClass, backend,
                    backend.Document.Bounds(), result.Geometry!.Bounds);
                texts.Add(name + ": знак " + sign.Number + ", форма " + sign.Shape + ", типоразмер " + sizeClass);
            }

            // 3. Схема дислокации с опорами и стопками знаков (координаты — метры).
            var (scheme, _) = DemoSchemeLoader.Load(_root);
            var geometries = BuildGeometries(scheme);
            var schemeBackend = SchemeDxfWriter.Build(scheme, geometries, _config.Drawing, _config.Typography);
            Add(files, "dxf/scheme.dxf", "scheme", scheme.Id, "", schemeBackend,
                schemeBackend.Document.Bounds(), null);
            texts.Add("scheme.dxf: ось " + Num(scheme.Alignment.Length) + " м, опор " +
                scheme.Posts.Count.ToString(CultureInfo.InvariantCulture) + ", знаков " +
                scheme.Posts.Sum(p => p.Mounts.Count).ToString(CultureInfo.InvariantCulture));

            // 4. Ведомость ТС ОДД как таблица чертежа.
            var statement = TsoddStatement.Build(scheme, _config.Catalog);
            var table = BuildStatementTable(statement);
            var statementBackend = StatementDxfWriter.Build(table, _config.StatementTable, new Point2(0, 0),
                _config.Drawing.DxfFlattenSagittaMm);
            Add(files, "dxf/statement.dxf", "statement", "ТС ОДД", "", statementBackend,
                statementBackend.Document.Bounds(), null);
            texts.Add("statement.dxf: строк " + statement.Rows.Count.ToString(CultureInfo.InvariantCulture) +
                ", колонок " + _config.StatementTable.Columns.Count.ToString(CultureInfo.InvariantCulture));

            foreach (var record in files)
                DemoFiles.WriteText(_root, "docs/demo/" + record.Path, record.Backend.Document.Build());
            DemoFiles.WriteText(_root, "docs/demo/dxf/expected.json", Manifest(files));
            Readme(files, texts);
        }

        /// <summary>Строит таблицу ведомости в координатах чертежа (мм).</summary>
        public TableLayout BuildStatementTable(TsoddStatement statement)
        {
            if (statement == null) throw new ArgumentNullException(nameof(statement));
            var spec = _config.StatementTable;
            var headers = spec.Columns.Select(c => c.Title).ToList();
            var rows = new List<IReadOnlyList<string>>();
            foreach (var row in statement.Rows)
                rows.Add(new[]
                {
                    row.Index.ToString(CultureInfo.InvariantCulture), row.Number.ToString(), row.Name,
                    row.SizeClass, row.Parameter, row.Count.ToString(CultureInfo.InvariantCulture), row.Note
                });
            return TableLayout.Build(spec, headers, rows, _config.Typography, new Point2(0, 0));
        }

        private static void Add(List<DxfFileRecord> files, string path, string kind, string sign, string sizeClass,
            DxfBackend backend, BoundingBox? contentBounds, BoundingBox? nominalBounds) =>
            files.Add(new DxfFileRecord(path, kind, sign, sizeClass, backend, contentBounds, nominalBounds));

        private static string Manifest(IEnumerable<DxfFileRecord> files)
        {
            var sb = new StringBuilder();
            using (var stream = new System.IO.MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    writer.WriteString("format", "DXF R12 (AC1009)");
                    writer.WriteString("generatedBy", "dotnet run --project src/OddMaster.Demo -- dxf");
                    writer.WriteString("status", "ДЕМО/ЧЕРНОВИК: геометрия и логика проверены, колориметрия и пиктограммы — нет");
                    writer.WriteString("verifier", "scripts/dxf_verify.py (ezdxf, независимая перечитка)");
                    writer.WriteStartArray("files");
                    foreach (var file in files)
                    {
                        var bounds = file.ContentBounds;
                        writer.WriteStartObject();
                        writer.WriteString("path", file.Path);
                        writer.WriteString("kind", file.Kind);
                        writer.WriteString("sign", file.Sign);
                        writer.WriteString("sizeClass", file.SizeClass);
                        writer.WriteString("units", file.Backend.InsUnits == 6 ? "m" : "mm");
                        writer.WriteNumber("entityCount", file.Backend.EntityCount);
                        writer.WriteStartArray("layers");
                        foreach (var layer in file.Backend.Layers.OrderBy(l => l.Name, StringComparer.Ordinal))
                            writer.WriteStringValue(layer.Name);
                        writer.WriteEndArray();
                        if (bounds != null)
                        {
                            writer.WriteStartObject("bounds");
                            writer.WriteNumber("minX", Math.Round(bounds.Value.MinX, 3));
                            writer.WriteNumber("minY", Math.Round(bounds.Value.MinY, 3));
                            writer.WriteNumber("maxX", Math.Round(bounds.Value.MaxX, 3));
                            writer.WriteNumber("maxY", Math.Round(bounds.Value.MaxY, 3));
                            writer.WriteEndObject();
                        }
                        if (file.NominalBounds != null)
                        {
                            writer.WriteStartObject("nominal");
                            writer.WriteNumber("widthMm", Math.Round(file.NominalBounds.Value.Width, 3));
                            writer.WriteNumber("heightMm", Math.Round(file.NominalBounds.Value.Height, 3));
                            writer.WriteEndObject();
                        }
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }
                sb.Append(Encoding.UTF8.GetString(stream.ToArray()));
            }
            return sb.ToString() + "\n";
        }

        private string Readme(IReadOnlyList<DxfFileRecord> files, IReadOnlyList<string> notes)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# DXF-стенд (ДЕМО, ЧЕРНОВИК)");
            sb.AppendLine();
            sb.AppendLine("Сгенерировано командой `dotnet run --project src/OddMaster.Demo -- dxf`. " +
                "Формат — DXF R12 (AC1009), единицы в HEADER, кириллица через `\\U+XXXX` (файл ASCII). " +
                "Проверка — независимая перечитка `scripts/dxf_verify.py` на ezdxf: файлы читаются заново, " +
                "сверяются слои, число сущностей, габарит и тексты с `expected.json`.");
            sb.AppendLine();
            sb.AppendLine("**Что это доказывает:** ядро отдаёт примитивы через `ICadBackend`/`IDrawingPort` и получает " +
                "настоящий чертёж, который открывается сторонним ПО. **Чего это не доказывает:** работу в nanoCAD " +
                "(G1/G2 закрыты), колориметрию ГОСТ, соответствие пиктограмм прил. Б.");
            sb.AppendLine();
            sb.AppendLine("| Файл | Тип | Что внутри | Сущностей | Слоёв |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var file in files)
                sb.AppendLine("| `" + file.Path + "` | " + file.Kind + " | " + file.Sign +
                    (file.SizeClass.Length > 0 ? " " + file.SizeClass : "") + " | " +
                    file.Backend.EntityCount.ToString(CultureInfo.InvariantCulture) + " | " +
                    file.Backend.Layers.Count.ToString(CultureInfo.InvariantCulture) + " |");
            sb.AppendLine();
            sb.AppendLine("## Состав");
            sb.AppendLine();
            foreach (var note in notes) sb.AppendLine("- " + note);
            sb.AppendLine();
            sb.AppendLine("## Ограничения стенда");
            sb.AppendLine();
            sb.AppendLine("- Дуги заменяются хордами с допуском `dxfFlattenSagittaMm` из `config/conventions/drawing.json` " +
                "(0,2 мм): это отображение, а не норма.");
            sb.AppendLine("- Заливка — веер треугольников `SOLID`, потому что в DXF R12 нет `HATCH`.");
            sb.AppendLine("- Цвета — индексы ACI, это палитра САПР, а не колориметрия ГОСТ (`DISPLAY_ONLY`).");
            sb.AppendLine("- Пиктограммы остаются заглушками: реальная графика прил. Б ждёт оцифровки студентом.");
            DemoFiles.WriteText(_root, "docs/demo/dxf/README.md", sb.ToString());
            return sb.ToString();
        }

        private static string Num(double value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
