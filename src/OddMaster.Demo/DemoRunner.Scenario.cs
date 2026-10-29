using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OddMaster.Core.Checks;
using OddMaster.Core.Document;
using OddMaster.Core.Geometry;
using OddMaster.Core.Norms;
using OddMaster.Core.Scheme;
using OddMaster.Core.Statement;
using OddMaster.Render.Dxf;

namespace OddMaster.Demo
{
    /// <summary>
    /// Команда «scenario»: живой сценарий брифа на Linux-стенде.
    /// D2 — добавляем знак на опору, ведомость пересчитывается и попадает в чертёж.
    /// D5 — заведомо ошибочная схема, находки правил рисуются на чертеже.
    /// Это не nanoCAD, а доказательство логики и чертёжной выдачи (G1/G2 по-прежнему закрыты).
    /// </summary>
    public sealed partial class DemoRunner
    {
        /// <summary>
        /// Пишет live-before.dxf, live-after.dxf, control.dxf и README-scenario.md.
        /// Возвращает записи для манифеста DXF-стенда: сценарий проверяется тем же независимым читателем.
        /// </summary>
        public List<DxfFileRecord> Scenario()
        {
            var (scheme, _) = DemoSchemeLoader.Load(_root);
            var geometries = BuildGeometries(scheme);

            // --- D2: было ---
            var before = BuildSchemeWithStatement(scheme, geometries, "live-before.dxf",
                out var beforeRows, out var beforeSigns);
            // --- D2: правим схему — ставим на опору P2 знак 3.24 (40) над 2.4 ---
            var newSign = new SignInstance("P2-NEW", SignNumber.Parse("3.24"), "II", "speed", 40m);
            var editedPosts = new List<Post>();
            foreach (var post in scheme.Posts)
            {
                if (post.Id != "P2")
                {
                    editedPosts.Add(post);
                    continue;
                }
                var mounts = new List<SignInstance> { newSign };
                mounts.AddRange(post.Mounts);
                editedPosts.Add(new Post(post.Id, post.StationMeters, post.OffsetMeters, mounts));
            }
            var edited = new SchemeModel(scheme.Id + "-EDIT", scheme.Alignment, editedPosts);

            var live = LiveStatement.Build(scheme, _config.Catalog);
            var delta = live.Apply(null, newSign);
            var editedGeometries = BuildGeometries(edited);
            var after = BuildSchemeWithStatement(edited, editedGeometries, "live-after.dxf",
                out var afterRows, out var afterSigns);

            // --- D5: контроль заведомо ошибочной схемы ---
            var control = BuildControlDrawing();
            BuildScenarioReadme(before, after, beforeRows, afterRows, beforeSigns, afterSigns, delta, control,
                scheme.Posts.Count, edited.Posts.Count);
            return new List<DxfFileRecord>
            {
                new DxfFileRecord("dxf/live-before.dxf", "scenario", "до правки", "", before,
                    before.Document.Bounds(), null),
                new DxfFileRecord("dxf/live-after.dxf", "scenario", "после правки", "", after,
                    after.Document.Bounds(), null),
                new DxfFileRecord("dxf/control.dxf", "scenario", "контроль", "", control,
                    control.Document.Bounds(), null)
            };
        }

        private DxfBackend BuildSchemeWithStatement(SchemeModel scheme,
            IReadOnlyDictionary<string, Core.Generation.SignGeometry> geometries, string fileName,
            out int statementRows, out int totalSigns)
        {
            var backend = SchemeDxfWriter.Build(scheme, geometries, _config.Drawing, _config.Typography);
            var statement = TsoddStatement.Build(scheme, _config.Catalog);
            statementRows = statement.Rows.Count;
            totalSigns = statement.Rows.Sum(r => r.Count);

            // Ведомость ставим под схемой в масштабе листа: 1 мм бумаги = SheetScaleDenominator/1000
            // единиц чертежа (для 1:1000 это ровно 1 м). Так таблица читается как на настоящем листе.
            var k = _config.Drawing.SheetScaleDenominator / 1000.0;
            var bounds = backend.Document.Bounds();
            var minX = bounds?.MinX ?? 0;
            var minY = bounds?.MinY ?? 0;
            var probe = BuildStatementTableAt(statement, new Point2(0, 0));
            var tableOriginMm = new Point2(minX / k, (minY - 40.0) / k - probe.Bounds.Height);
            var table = BuildStatementTableAt(statement, tableOriginMm);
            StatementDxfWriter.Write(backend, table, k);
            backend.AddLayer("TABLE_TITLE", DxfColorMap.Aci("Black"));
            backend.AddText("ЧЕРНОВИК: ведомость пересчитана ядром, форма не сверена (GOST-011)",
                new Point2(minX, (minY - 40.0) - 30 * k), 3.0, "TABLE_TITLE");
            backend.SaveDocument(System.IO.Path.Combine(_root, "docs/demo/dxf/" + fileName));
            return backend;
        }

        private TableLayout BuildStatementTableAt(TsoddStatement statement, Point2 originMm)
        {
            var spec = _config.StatementTable;
            var headers = spec.Columns.Select(c => c.Title).ToList();
            var rows = new List<IReadOnlyList<string>>();
            foreach (var row in statement.Rows)
                rows.Add(new[]
                {
                    row.Index.ToString(CultureInfo.InvariantCulture), row.Number.ToString(), row.Name,
                    row.SizeClass, row.Parameter, row.Count.ToString(CultureInfo.InvariantCulture), row.Note
                });
            return TableLayout.Build(spec, headers, rows, _config.Typography, originMm);
        }

        private DxfBackend BuildControlDrawing()
        {
            var csv = Core.Config.JsonConfig.ReadUtf8(_root, "tests/fixtures/checks/legacy-scheme.csv");
            var request = AuditCsvParser.Parse(csv, "ДЕМО: чужая схема 2004");
            var report = new SchemeAuditor(_config).Audit(request);

            var backend = new DxfBackend(_config.Drawing.DxfFlattenSagittaMm) { InsUnits = 6 };
            backend.AddLayer("AXIS", DxfColorMap.Aci("Grey"));
            backend.AddLayer("SIGN_PLACE", DxfColorMap.Aci("Black"));
            backend.AddLayer("FINDING", DxfColorMap.Aci("Red"));
            backend.AddLayer("FINDING_TEXT", DxfColorMap.Aci("Red"));

            // Раскладываем записи по прямой оси: X — пикет, Y — смещение (масштаб условный).
            var stations = request.Entries.Select(e => e.StationMeters).ToList();
            var minStation = stations.Count == 0 ? 0 : stations.Min() - 20;
            var maxStation = stations.Count == 0 ? 100 : stations.Max() + 20;
            backend.AddLine(new Point2(minStation, 0), new Point2(maxStation, 0), "AXIS");
            foreach (var entry in request.Entries)
            {
                backend.AddCircle(new Point2(entry.StationMeters, entry.OffsetMeters * 0.2), 1.2, "SIGN_PLACE");
                backend.AddText(entry.Id + " " + entry.Number,
                    new Point2(entry.StationMeters - 4, entry.OffsetMeters * 0.2 + 2.2), 1.0, "SIGN_PLACE");
            }

            // Находки: маркер + правило + короткое сообщение рядом с местом нарушения.
            var line = 0;
            foreach (var finding in report.Findings)
            {
                var y = 12 + line * 4.0;
                line++;
                var x = finding.StationMeters;
                backend.AddText(finding.RuleId + " (" + finding.Severity + "): " + Shorten(finding.Message, 70),
                    new Point2(minStation, y), 1.1, "FINDING_TEXT");
                backend.AddLine(new Point2(minStation + 74, y + 0.4), new Point2(x, y + 0.4), "FINDING");
                backend.AddCircle(new Point2(x, y + 0.4), 0.8, "FINDING");
            }
            backend.AddText(_config.Drawing.DraftBanner + " · находок: " +
                report.Findings.Count.ToString(CultureInfo.InvariantCulture) + " · " + report.Banner,
                new Point2(minStation, 5), 1.2, "FINDING_TEXT");
            backend.SaveDocument(System.IO.Path.Combine(_root, "docs/demo/dxf/control.dxf"));
            return backend;
        }

        private void BuildScenarioReadme(DxfBackend before, DxfBackend after, int beforeRows, int afterRows,
            int beforeSigns, int afterSigns, StatementDelta delta, DxfBackend control,
            int beforePostCount, int afterPostCount)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Живой сценарий на стенде (ДЕМО, ЧЕРНОВИК)");
            sb.AppendLine();
            sb.AppendLine("Сгенерировано командой `dotnet run --project src/OddMaster.Demo -- scenario`. " +
                "Схема синтетическая, стенд — DXF, не nanoCAD.");
            sb.AppendLine();
            sb.AppendLine("## D2 · Правка схемы → ведомость");
            sb.AppendLine();
            sb.AppendLine("| Файл | Опор | Знаков | Строк ведомости | Сущностей DXF |");
            sb.AppendLine("|---|---|---|---|---|");
            sb.AppendLine("| `live-before.dxf` | " + beforePostCount + " | " + beforeSigns + " | " + beforeRows +
                " | " + before.EntityCount + " |");
            sb.AppendLine("| `live-after.dxf` | " + afterPostCount + " | " + afterSigns + " | " + afterRows +
                " | " + after.EntityCount + " |");
            sb.AppendLine();
            sb.AppendLine("Дельта ведомости на одно событие «добавлен знак 3.24 (40) на опору P2»:");
            sb.AppendLine();
            sb.AppendLine(delta.ToMarkdown().TrimEnd());
            sb.AppendLine();
            sb.AppendLine("## D5 · Контроль → находки на чертеже");
            sb.AppendLine();
            sb.AppendLine("`control.dxf` — заведомо ошибочная схема из `tests/fixtures/checks/legacy-scheme.csv`: " +
                "маркеры находок и подписи правил R001–R007 стоят на местах нарушений.");
            sb.AppendLine();
            sb.AppendLine("## Чего эти файлы не доказывают");
            sb.AppendLine();
            sb.AppendLine("- Работу в nanoCAD: адаптер не собран (гейт G1).");
            sb.AppendLine("- Реакторы и живую ведомость **внутри** САПР: в стенде пересчёт сделан ядром по событию.");
            sb.AppendLine("- Соответствие пиктограмм и надписей прил. Б/В/Г: это заглушки, GOST-002/GOST-019.");
            var path = "docs/demo/dxf/README-scenario.md";
            DemoFiles.WriteText(_root, path, sb.ToString());
        }

        private static string Shorten(string text, int limit) =>
            text.Length <= limit ? text : text.Substring(0, limit - 1) + "…";
    }
}
