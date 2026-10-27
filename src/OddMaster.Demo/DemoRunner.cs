using System.Collections.Generic;
using System.Linq;
using System.Text;
using OddMaster.Core.Actualizer;
using OddMaster.Core.Checks;
using OddMaster.Core.Generation;
using OddMaster.Core.Scheme;
using OddMaster.Core.Statement;
using OddMaster.Render.Svg;

namespace OddMaster.Demo
{
    /// <summary>Сквозные демо-сценарии M8: схема, проверки, актуализация, реактор.</summary>
    public sealed partial class DemoRunner
    {
        /// <summary>Строит схему, ведомость, журнал координат и демонстрацию реактора.</summary>
        public void Scheme()
        {
            var (scheme, _) = DemoSchemeLoader.Load(_root);
            var geometries = BuildGeometries(scheme);
            var schemeSvg = new SvgSchemeRenderer(_config.Drawing).Render(scheme, geometries);
            DemoFiles.WriteText(_root, "docs/demo/scheme.svg", schemeSvg);

            var log = SchemeReactor.Compute(scheme);
            DemoFiles.WriteText(_root, "docs/demo/coordinates.csv", log.ToCsv());
            DemoFiles.WriteText(_root, "docs/demo/coordinates.md", log.ToMarkdown());

            var statement = TsoddStatement.Build(scheme, _config.Catalog);
            DemoFiles.WriteText(_root, "docs/demo/statement.csv", statement.ToCsv());
            DemoFiles.WriteText(_root, "docs/demo/statement.md", statement.ToMarkdown());

            var shifted = new SchemeModel(scheme.Id, scheme.Alignment.Shift(new OddMaster.Core.Geometry.Vector2(50, 0)), scheme.Posts);
            var logAfter = SchemeReactor.Compute(shifted);
            DemoFiles.WriteText(_root, "docs/demo/reactor_before.md", "# Журнал до изменения оси\n\n" + log.ToMarkdown());
            DemoFiles.WriteText(_root, "docs/demo/reactor_after.md", "# Журнал после сдвига оси на (50; 0)\n\n" + logAfter.ToMarkdown());
            DemoFiles.WriteText(_root, "docs/demo/reactor_diff.txt", log.Diff(logAfter));
            WriteDemoReadme(scheme, statement, log);
        }

        private Dictionary<string, SignGeometry> BuildGeometries(SchemeModel scheme)
        {
            var result = new Dictionary<string, SignGeometry>(System.StringComparer.Ordinal);
            foreach (var post in scheme.Posts)
                foreach (var sign in post.Mounts)
                {
                    if (result.ContainsKey(sign.StatementKey)) continue;
                    var parameters = sign.ParameterKind != null && sign.ParameterValue.HasValue
                        ? new Dictionary<string, decimal> { [sign.ParameterKind] = sign.ParameterValue.Value }
                        : null;
                    var generated = _generator.Generate(sign.Number.ToString(), sign.SizeClass, parameters);
                    if (generated.IsSuccess) result[sign.StatementKey] = generated.Geometry!;
                }
            return result;
        }

        private void WriteDemoReadme(SchemeModel scheme, TsoddStatement statement, CoordinateLog log)
        {
            var validation = OddMaster.Core.Config.ConfigValidator.Validate(_root);
            var sb = new StringBuilder();
            sb.AppendLine("# Демонстрационные артефакты (ДЕМО, ЧЕРНОВИК)");
            sb.AppendLine();
            sb.AppendLine("Сгенерировано командой `dotnet run --project src/OddMaster.Demo -- all`. " +
                "Схема синтетическая (`tests/fixtures/demo/scheme.json`), не реальный объект. " +
                "Все числа из ГОСТ приходят из `config/`; пиктограммы, высота надписей и внутренняя кайма — заглушки.");
            sb.AppendLine();
            sb.AppendLine("| Файл | Содержание |");
            sb.AppendLine("|---|---|");
            sb.AppendLine("| `signs/*.svg` | Галерея POC-знаков во всех типоразмерах |");
            sb.AppendLine("| `scheme.svg` | Схема дислокации: ось, пикеты, опоры, мини-знаки, баннер ЧЕРНОВИК/ДЕМО |");
            sb.AppendLine("| `statement.csv` / `statement.md` | Ведомость ТС ОДД (форма — черновик, GOST-011) |");
            sb.AppendLine("| `coordinates.csv` / `coordinates.md` | Журнал координат |");
            sb.AppendLine("| `reactor_before.md` / `reactor_after.md` / `reactor_diff.txt` | Реактор: сдвиг оси на (50; 0) |");
            sb.AppendLine("| `audit_report.csv` / `audit_report.md` | Проверка чужой схемы правилами R001–R007 |");
            sb.AppendLine("| `actualization_report.csv` / `actualization_report.md` | Актуализация на ДЕМО-карте замен |");
            sb.AppendLine("| `config_validation.md` | Находки валидатора конфигов и несостыковки стандарта |");
            sb.AppendLine();
            sb.AppendLine("Опор: " + scheme.Posts.Count + "; строк ведомости: " + statement.Rows.Count + "; строк журнала: " + log.Rows.Count + ".");
            sb.AppendLine();
            sb.AppendLine("## Проверка конфигурации");
            sb.AppendLine();
            sb.AppendLine(validation.ToMarkdown());
            DemoFiles.WriteText(_root, "docs/demo/README.md", sb.ToString());
            DemoFiles.WriteText(_root, "docs/demo/config_validation.md", validation.ToMarkdown());
        }

        /// <summary>Проверяет чужую схему из CSV-фикстуры и пишет отчёт.</summary>
        public void Check()
        {
            var csv = OddMaster.Core.Config.JsonConfig.ReadUtf8(_root, "tests/fixtures/checks/legacy-scheme.csv");
            var request = AuditCsvParser.Parse(csv, "ДЕМО: чужая схема 2004");
            var report = new SchemeAuditor(_config).Audit(request);
            DemoFiles.WriteText(_root, "docs/demo/audit_report.md", report.ToMarkdown());
            DemoFiles.WriteText(_root, "docs/demo/audit_report.csv", report.ToCsv());
        }

        /// <summary>Прогоняет актуализатор на синтетической ДЕМО-карте замен.</summary>
        public void Actualize()
        {
            var map = OddMaster.Core.Config.ConfigLoader.LoadMigrationMap(
                _root, "tests/fixtures/actualizer/map-demo.json");
            var recognizer = OddMaster.Core.Config.ConfigLoader.LoadRecognizer(
                _root, "tests/fixtures/actualizer/recognizer-patterns.json");
            var legacy = OddMaster.Core.Config.ConfigLoader.LoadLegacyScheme(
                _root, "tests/fixtures/actualizer/legacy-scheme.json");
            var report = new ActualizationEngine(recognizer, map, _config.Catalog).Run(legacy);
            DemoFiles.WriteText(_root, "docs/demo/actualization_report.md", report.ToMarkdown());
            DemoFiles.WriteText(_root, "docs/demo/actualization_report.csv", report.ToCsv());
        }
    }
}
