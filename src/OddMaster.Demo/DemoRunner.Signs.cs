using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OddMaster.Core.Catalog;
using OddMaster.Core.Config;
using OddMaster.Core.Generation;
using OddMaster.Core.Norms;
using OddMaster.Render.Svg;

namespace OddMaster.Demo
{
    /// <summary>Сквозной сценарий демо. Галерея знаков — этап M3; остальные команды — M8.</summary>
    public sealed partial class DemoRunner
    {
        private readonly string _root;
        private readonly NormativeConfigSet _config;
        private readonly SignGenerator _generator;
        private readonly SvgSignRenderer _signRenderer;

        /// <summary>Создаёт исполнитель демо.</summary>
        public DemoRunner(string root, NormativeConfigSet config)
        {
            _root = root;
            _config = config;
            _generator = new SignGenerator(config);
            _signRenderer = new SvgSignRenderer(config.Drawing, config.Typography);
        }

        /// <summary>Генерирует галерею POC-знаков во всех предусмотренных типоразмерах.</summary>
        public void Signs()
        {
            var rows = new List<string>();
            foreach (var sign in _config.Catalog.Signs)
            {
                foreach (var sizeClass in AvailableClasses(_config.MainProfile, sign.Number))
                {
                    var parameters = DefaultParameters(sign);
                    var result = _generator.Generate(sign, sizeClass, parameters);
                    if (!result.IsSuccess) continue;
                    var geometry = result.Geometry!;
                    var name = sign.Number.ToString().Replace('.', '-') + "_" + sizeClass + ".svg";
                    DemoFiles.WriteText(_root, "docs/demo/signs/" + name, _signRenderer.Render(geometry));
                    rows.Add("| " + sign.Number + " | " + sizeClass + " | " +
                        geometry.WidthMm.ToString("0.##", CultureInfo.InvariantCulture) + " × " +
                        geometry.HeightMm.ToString("0.##", CultureInfo.InvariantCulture) + " мм | " +
                        geometry.Verification.Describe() + " | рис. " + geometry.Sign.NormRef.Figure +
                        " / прил. Б |");
                }
            }
            var sb = new StringBuilder();
            sb.AppendLine("# Галерея POC-знаков (ЧЕРНОВИК)");
            sb.AppendLine();
            sb.AppendLine("Сгенерировано командой `dotnet run --project src/OddMaster.Demo -- signs`. " +
                "Геометрия построена из `config/`; размеры — по прил. Д ГОСТ Р 52290-2024 " +
                "(статус `CuratorExtracted`), пиктограммы, высота надписей и внутренняя кайма — заглушки " +
                "(`Placeholder`). Цвета — только отображение, не колориметрия ГОСТ.");
            sb.AppendLine();
            sb.AppendLine("| Знак | Типоразмер | Габарит | Статус проверки | Что сверить |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var row in rows) sb.AppendLine(row);
            sb.AppendLine();
            sb.AppendLine("Все файлы детерминированы: два прогона дают одинаковые байты.");
            DemoFiles.WriteText(_root, "docs/demo/signs/README.md", sb.ToString());
        }

        /// <summary>Значения параметров по умолчанию для демо-знаков.</summary>
        public static Dictionary<string, decimal>? DefaultParameters(SignDefinition sign)
        {
            if (sign.ParameterKind == null) return null;
            return new Dictionary<string, decimal>
            {
                [sign.ParameterKind] = sign.ParameterKind == "speed" ? 40m : 50m
            };
        }

        /// <summary>Типоразмеры, предусмотренные таблицами прил. Д для номера знака.</summary>
        public static List<string> AvailableClasses(SizeProfile profile, SignNumber number)
        {
            var result = new List<string>();
            foreach (var table in profile.Tables)
                foreach (var row in table.Rows)
                {
                    if (!row.Signs.Contains(number)) continue;
                    foreach (var key in row.Sizes.Keys.OrderBy(x => x, System.StringComparer.Ordinal))
                        if (row.Sizes[key].Values.Count > 0 && !result.Contains(key)) result.Add(key);
                }
            return result;
        }
    }
}
