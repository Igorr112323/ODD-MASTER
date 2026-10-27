using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using OddMaster.Core.Actualizer;
using OddMaster.Core.Checks;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Config
{
    /// <summary>Вторая часть загрузчика: контроль, условные изображения и карта замен.</summary>
    public static partial class ConfigLoader
    {
        /// <summary>Загружает настройки правил контроля R001–R007.</summary>
        public static ControlSettings LoadControls(string root)
        {
            var file = ConfigPaths.Controls;
            using var doc = JsonConfig.Load(root, file);
            var e = doc.RootElement;
            var rules = new List<ControlRule>();
            var items = J.Array(e, "rules", file, "");
            for (int i = 0; i < items.Count; i++)
            {
                var r = items[i];
                var p = "/rules/" + i.ToString(CultureInfo.InvariantCulture);
                var parameters = new Dictionary<string, double>(StringComparer.Ordinal);
                var pars = J.Optional(r, "params");
                if (pars != null && pars.Value.ValueKind == JsonValueKind.Object)
                    foreach (var x in pars.Value.EnumerateObject())
                        if (x.Value.ValueKind == JsonValueKind.Number) parameters[x.Name] = x.Value.GetDouble();
                var sevRaw = J.Str(r, "severity", file, p);
                if (!Enum.TryParse<FindingSeverity>(sevRaw, out var severity))
                    throw new ConfigException(file, p + "/severity", "неизвестная важность «" + sevRaw + "»",
                        "используйте Error, Warning или Info");
                rules.Add(new ControlRule(J.Str(r, "id", file, p), J.OptBool(r, "enabled", true, file, p),
                    severity, J.Str(r, "title", file, p), parameters,
                    ReadVerificationAt(r, file, p), J.ReadNormRef(r, "normRef", file, p)));
            }
            var pairing = new List<PairingRule>();
            var pairItems = J.OptArray(e, "pairing", file, "");
            for (int i = 0; i < pairItems.Count; i++)
            {
                var r = pairItems[i];
                var p = "/pairing/" + i.ToString(CultureInfo.InvariantCulture);
                pairing.Add(new PairingRule(J.Str(r, "start", file, p), J.Str(r, "end", file, p),
                    J.Str(r, "repeat", file, p), J.StringList(r, "plates", file, p),
                    ReadVerificationAt(r, file, p), J.OptStr(r, "marker", file, p),
                    J.ReadNormRef(r, "normRef", file, p)));
            }
            return new ControlSettings(J.ReadNormRef(e, "normRef", file, ""),
                J.ReadVerification(e, file, ""), rules, pairing, J.StringList(e, "markers", file, ""));
        }

        /// <summary>Загружает соглашения об условном изображении знаков на схеме.</summary>
        public static DrawingConventions LoadDrawing(string root)
        {
            var file = ConfigPaths.Drawing;
            using var doc = JsonConfig.Load(root, file);
            var e = doc.RootElement;
            return new DrawingConventions(
                J.ReadNormRef(e, "normRef", file, ""), J.ReadVerification(e, file, ""),
                J.Num(e, "miniSignOnPaperMm", file, ""), J.Num(e, "sheetScaleDenominator", file, ""),
                J.Num(e, "stationTickEveryM", file, ""), J.Num(e, "postSymbolRadiusMm", file, ""),
                J.Num(e, "leaderLineMm", file, ""), J.Str(e, "draftBanner", file, ""),
                J.OptStr(e, "note", file, "") ?? "", J.StringList(e, "markers", file, ""));
        }

        /// <summary>Загружает карту замен. Принимает путь — боевая карта или ДЕМО-фикстура.</summary>
        public static MigrationMap LoadMigrationMap(string root, string file)
        {
            using var doc = JsonConfig.Load(root, file);
            var e = doc.RootElement;
            var rules = new List<MigrationRule>();
            var items = J.Array(e, "rules", file, "");
            for (int i = 0; i < items.Count; i++)
            {
                var r = items[i];
                var p = "/rules/" + i.ToString(CultureInfo.InvariantCulture);
                var condition = J.Property(r, "condition", file, p);
                var result = J.Property(r, "result", file, p);
                var categoryRaw = J.Str(r, "category", file, p);
                if (!Enum.TryParse<MigrationCategory>(categoryRaw, out var category))
                    throw new ConfigException(file, p + "/category", "неизвестная категория «" + categoryRaw + "»",
                        "используйте Replaced, Removed, NeedsDecision или AllowedSimplify");
                var signsElement = J.Optional(result, "signs");
                SignNumberSet? resultSigns = signsElement != null && signsElement.Value.ValueKind == JsonValueKind.String
                    ? SignNumberSet.Parse(signsElement.Value.GetString() ?? "") : null;
                rules.Add(new MigrationRule(
                    J.Str(r, "id", file, p),
                    J.SignsSet(condition, "signs", file, p + "/condition"),
                    J.StringList(condition, "plates", file, p + "/condition"),
                    resultSigns,
                    J.StringList(result, "plates", file, p + "/result"),
                    J.OptStr(result, "note", file, p + "/result"),
                    category, J.ReadNormRef(r, "normRef", file, p),
                    ReadVerificationAt(r, file, p), J.OptStr(r, "comment", file, p)));
            }
            return new MigrationMap(J.Str(e, "map", file, ""), J.Str(e, "title", file, ""),
                J.ReadNormRef(e, "normRef", file, ""), J.ReadVerification(e, file, ""),
                J.OptStr(e, "banner", file, "") ?? "", J.OptStr(e, "note", file, "") ?? "",
                J.OptBool(e, "isDemo", false, file, ""), rules, J.StringList(e, "markers", file, ""));
        }

        /// <summary>Загружает шаблоны распознавателя из ДЕМО-фикстуры.</summary>
        public static PatternRecognizer LoadRecognizer(string root, string file)
        {
            using var doc = JsonConfig.Load(root, file);
            var e = doc.RootElement;
            var patterns = new List<RecognitionPattern>();
            var items = J.Array(e, "patterns", file, "");
            for (int i = 0; i < items.Count; i++)
            {
                var r = items[i];
                var p = "/patterns/" + i.ToString(CultureInfo.InvariantCulture);
                var regexRaw = J.Str(r, "regex", file, p);
                Regex regex;
                try { regex = new Regex(regexRaw, RegexOptions.Compiled | RegexOptions.CultureInvariant); }
                catch (ArgumentException ex)
                {
                    throw new ConfigException(file, p + "/regex", "некорректное регулярное выражение: " + ex.Message,
                        "проверьте экранирование");
                }
                patterns.Add(new RecognitionPattern(J.Str(r, "id", file, p),
                    J.Str(r, "field", file, p), regex, J.OptStr(r, "number", file, p),
                    J.StringList(r, "plates", file, p), J.Num(r, "confidence", file, p)));
            }
            return new PatternRecognizer(patterns);
        }

        /// <summary>Загружает старую схему из ДЕМО-фикстуры.</summary>
        public static LegacyScheme LoadLegacyScheme(string root, string file)
        {
            using var doc = JsonConfig.Load(root, file);
            var e = doc.RootElement;
            var elements = new List<LegacyElement>();
            var items = J.Array(e, "elements", file, "");
            for (int i = 0; i < items.Count; i++)
            {
                var r = items[i];
                var p = "/elements/" + i.ToString(CultureInfo.InvariantCulture);
                var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
                var attr = J.Optional(r, "attributes");
                if (attr != null && attr.Value.ValueKind == JsonValueKind.Object)
                    foreach (var a in attr.Value.EnumerateObject())
                        if (a.Value.ValueKind == JsonValueKind.String) attributes[a.Name] = a.Value.GetString() ?? "";
                elements.Add(new LegacyElement(J.Str(r, "id", file, p), J.Str(r, "blockName", file, p),
                    attributes, J.StringList(r, "texts", file, p),
                    J.Num(r, "x", file, p), J.Num(r, "y", file, p)));
            }
            return new LegacyScheme(elements);
        }
    }
}
