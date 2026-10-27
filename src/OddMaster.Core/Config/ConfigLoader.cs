using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using OddMaster.Core.Actualizer;
using OddMaster.Core.Catalog;
using OddMaster.Core.Checks;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Config
{
    /// <summary>Загрузка всех конфигов в типизированную модель. Никаких нормативных чисел в коде.</summary>
    public static partial class ConfigLoader
    {
        /// <summary>Загружает все файлы config/ из корня репозитория.</summary>
        public static NormativeConfigSet Load(string root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            return new NormativeConfigSet(
                LoadProfile(root, ConfigPaths.MainProfile),
                LoadProfile(root, ConfigPaths.SecondProfile),
                LoadBorders(root),
                LoadParameters(root),
                LoadVariants(root),
                LoadSources(root),
                LoadCatalog(root),
                LoadControls(root),
                LoadDrawing(root),
                LoadMigrationMap(root, ConfigPaths.MigrationMap));
        }

        /// <summary>Загружает профиль типоразмеров.</summary>
        public static SizeProfile LoadProfile(string root, string file)
        {
            using var doc = JsonConfig.Load(root, file);
            var e = doc.RootElement;
            var normRef = J.ReadNormRef(e, "normRef", file, "");
            var verification = J.ReadVerification(e, file, "");
            var classes = J.StringList(e, "sizeClasses", file, "");
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            var namesElement = J.Optional(e, "sizeClassNames");
            if (namesElement != null && namesElement.Value.ValueKind == JsonValueKind.Object)
                foreach (var p in namesElement.Value.EnumerateObject())
                    names[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? "" : "";

            var tables = new List<SizeTable>();
            var tableElements = J.Array(e, "tables", file, "");
            for (int ti = 0; ti < tableElements.Count; ti++)
            {
                var t = tableElements[ti];
                var tp = "/tables/" + ti.ToString(CultureInfo.InvariantCulture);
                var rows = new List<SizeRow>();
                var rowElements = J.Array(t, "rows", file, tp);
                for (int ri = 0; ri < rowElements.Count; ri++)
                {
                    var r = rowElements[ri];
                    var rp = tp + "/rows/" + ri.ToString(CultureInfo.InvariantCulture);
                    var signs = J.SignsSet(r, "signs", file, rp);
                    var sizes = ParseSizes(r, file, rp);
                    double? radius = J.OptNum(r, "r", file, rp);
                    var classless = new Dictionary<string, double>(StringComparer.Ordinal);
                    var cl = J.Optional(r, "classless");
                    if (cl != null && cl.Value.ValueKind == JsonValueKind.Object)
                        foreach (var p in cl.Value.EnumerateObject())
                            if (p.Value.ValueKind == JsonValueKind.Number) classless[p.Name] = p.Value.GetDouble();
                    rows.Add(new SizeRow(ri, signs, sizes, radius, classless));
                }
                tables.Add(new SizeTable(
                    J.Str(t, "id", file, tp), J.Str(t, "figure", file, tp),
                    J.Str(t, "shapeHint", file, tp), J.StringList(t, "dims", file, tp),
                    rows, J.StringList(t, "notes", file, tp)));
            }

            return new SizeProfile(
                J.Str(e, "profile", file, ""), J.Str(e, "title", file, ""),
                classes, names, normRef, verification,
                J.StringList(e, "clauseReferencedTables", file, ""),
                J.StringList(e, "markers", file, ""),
                J.StringList(e, "notes", file, ""), tables);
        }

        private static Dictionary<string, SizeSpec> ParseSizes(JsonElement row, string file, string pointer)
        {
            var result = new Dictionary<string, SizeSpec>(StringComparer.Ordinal);
            var sizes = J.Property(row, "sizes", file, pointer);
            if (sizes.ValueKind != JsonValueKind.Object)
                throw new ConfigException(file, pointer + "/sizes", "ожидался объект", "укажите объект {} с типоразмерами");
            foreach (var sizeClass in sizes.EnumerateObject())
            {
                var values = new Dictionary<string, double>(StringComparer.Ordinal);
                var options = new Dictionary<string, List<double>>(StringComparer.Ordinal);
                if (sizeClass.Value.ValueKind != JsonValueKind.Object)
                    throw new ConfigException(file, pointer + "/sizes/" + sizeClass.Name, "ожидался объект", "укажите объект размерностей");
                foreach (var dim in sizeClass.Value.EnumerateObject())
                {
                    if (dim.Value.ValueKind == JsonValueKind.Number)
                        values[dim.Name] = dim.Value.GetDouble();
                    else if (dim.Value.ValueKind == JsonValueKind.Array)
                        options[dim.Name] = dim.Value.EnumerateArray()
                            .Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetDouble()).ToList();
                }
                result[sizeClass.Name] = new SizeSpec(values, options);
            }
            return result;
        }

        /// <summary>Загружает каймы.</summary>
        public static BordersNorms LoadBorders(string root)
        {
            var file = ConfigPaths.Borders;
            using var doc = JsonConfig.Load(root, file);
            var e = doc.RootElement;
            var inner = J.Optional(e, "innerBorderWidthMm");
            double? innerWidth = inner != null && inner.Value.ValueKind == JsonValueKind.Number
                ? inner.Value.GetDouble() : (double?)null;
            var tolerances = new Dictionary<string, double>(StringComparer.Ordinal);
            var tol = J.Optional(e, "tolerances");
            if (tol != null && tol.Value.ValueKind == JsonValueKind.Object)
                foreach (var p in tol.Value.EnumerateObject())
                    if (p.Value.ValueKind == JsonValueKind.Number) tolerances[p.Name] = p.Value.GetDouble();
            var omitted = J.Property(e, "outerBorderMayBeOmitted", file, "");
            return new BordersNorms(
                J.ReadNormRef(e, "normRef", file, ""), J.ReadVerification(e, file, ""),
                ParseValueRules(e, "outerBorderWidthMm", file),
                ParseValueRules(e, "plateBorderWidthMm", file),
                ParseValueRules(e, "redBorderInnerCornerRadiusMm", file),
                J.SignsSet(omitted, "signs", file, "/outerBorderMayBeOmitted"),
                innerWidth, J.OptStr(e, "innerBorderWidthNote", file, "") ?? "",
                tolerances, J.StringList(e, "markers", file, ""));
        }

        private static List<ValueRule> ParseValueRules(JsonElement e, string name, string file)
        {
            var result = new List<ValueRule>();
            var items = J.OptArray(e, name, file, "");
            for (int i = 0; i < items.Count; i++)
            {
                var p = "/" + name + "/" + i.ToString(CultureInfo.InvariantCulture);
                result.Add(new ValueRule(J.SignsSet(items[i], "signs", file, p), J.Num(items[i], "value", file, p)));
            }
            return result;
        }

        /// <summary>Загружает параметры табл. Д.1.</summary>
        public static ParameterNorms LoadParameters(string root)
        {
            var file = ConfigPaths.Parameters;
            using var doc = JsonConfig.Load(root, file);
            var e = doc.RootElement;
            var rules = new List<ParameterRule>();
            var items = J.Array(e, "rules", file, "");
            for (int i = 0; i < items.Count; i++)
            {
                var r = items[i];
                var p = "/rules/" + i.ToString(CultureInfo.InvariantCulture);
                var ranges = new List<KmRange>();
                foreach (var kr in J.OptArray(r, "kmRanges", file, p))
                {
                    ranges.Add(new KmRange(
                        J.OptNum(kr, "fromKm", file, p), J.OptNum(kr, "toKm", file, p),
                        J.OptBool(kr, "toInclusive", false, file, p), J.OptNum(kr, "aboveKm", file, p),
                        J.OptNum(kr, "multipleOfKm", file, p) ?? 0.0));
                }
                var oneOf = Numbers(r, "oneOf", file, p).Select(x => (decimal)x).ToList();
                rules.Add(new ParameterRule(
                    J.SignsSet(r, "signs", file, p), J.Str(r, "parameter", file, p),
                    J.OptStr(r, "unit", file, p),
                    J.OptNum(r, "multipleOf", file, p) is double m ? (decimal)m : (decimal?)null,
                    oneOf,
                    J.OptNum(r, "above20MultipleOf", file, p) is double a ? (decimal)a : (decimal?)null,
                    ranges,
                    J.OptNum(r, "stepMinutes", file, p) is double s ? (int)s : (int?)null,
                    Numbers(r, "oneOfMinutes", file, p).Select(x => (int)x).ToList(),
                    J.OptNum(r, "fromHours", file, p) is double h ? (decimal)h : (decimal?)null,
                    J.OptNum(r, "multipleOfHours", file, p) is double mh ? (decimal)mh : (decimal?)null));
            }
            return new ParameterNorms(
                J.ReadNormRef(e, "normRef", file, ""), J.ReadVerification(e, file, ""),
                rules, J.StringList(e, "notes", file, ""), J.StringList(e, "markers", file, ""));
        }

        private static List<double> Numbers(JsonElement e, string name, string file, string pointer)
        {
            return J.OptArray(e, name, file, pointer)
                .Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetDouble()).ToList();
        }

        /// <summary>Загружает варианты исполнения.</summary>
        public static VariantNorms LoadVariants(string root)
        {
            var file = ConfigPaths.Variants;
            using var doc = JsonConfig.Load(root, file);
            var e = doc.RootElement;
            var sym = J.Optional(e, "sign64OptionalSymbols");
            var symbols = sym != null && sym.Value.ValueKind == JsonValueKind.Object
                ? J.OptStr(sym.Value, "symbolsLike", file, "/sign64OptionalSymbols") ?? "" : "";
            return new VariantNorms(
                J.ReadNormRef(e, "normRef", file, ""), J.ReadVerification(e, file, ""),
                J.SignsSet(e, "yellowBackgroundAllowed", file, ""),
                J.SignsSet(e, "individualDesign", file, ""),
                symbols, J.StringList(e, "markers", file, ""));
        }

        /// <summary>Загружает реестр нормативных документов.</summary>
        public static SourceRegistry LoadSources(string root)
        {
            var file = ConfigPaths.Sources;
            using var doc = JsonConfig.Load(root, file);
            var e = doc.RootElement;
            var sources = new List<NormativeSource>();
            var items = J.Array(e, "sources", file, "");
            for (int i = 0; i < items.Count; i++)
            {
                var s = items[i];
                var p = "/sources/" + i.ToString(CultureInfo.InvariantCulture);
                sources.Add(new NormativeSource(
                    J.Str(s, "id", file, p), J.Str(s, "doc", file, p), J.Str(s, "title", file, p),
                    J.Str(s, "approvedBy", file, p), J.OptStr(s, "effectiveFrom", file, p),
                    J.OptStr(s, "replaces", file, p), J.StringList(s, "usedFor", file, p),
                    ReadVerificationAt(s, file, p)));
            }
            return new SourceRegistry(J.ReadNormRef(e, "normRef", file, ""),
                J.ReadVerification(e, file, ""), sources, J.StringList(e, "markers", file, ""));
        }

        private static Verification ReadVerificationAt(JsonElement e, string file, string pointer)
        {
            var raw = J.Str(e, "verification", file, pointer);
            try { return VerificationParser.Parse(raw); }
            catch (FormatException ex)
            {
                throw new ConfigException(file, pointer + "/verification", ex.Message, "используйте допустимый статус");
            }
        }

        /// <summary>Загружает каталог знаков.</summary>
        public static SignCatalog LoadCatalog(string root)
        {
            var file = ConfigPaths.Catalog;
            using var doc = JsonConfig.Load(root, file);
            var e = doc.RootElement;
            var signs = new List<SignDefinition>();
            var items = J.Array(e, "signs", file, "");
            for (int i = 0; i < items.Count; i++)
            {
                var s = items[i];
                var p = "/signs/" + i.ToString(CultureInfo.InvariantCulture);
                var number = SignNumber.Parse(J.Str(s, "number", file, p));
                var colors = new Dictionary<string, string>(StringComparer.Ordinal);
                var colorElement = J.Property(s, "colors", file, p);
                foreach (var c in colorElement.EnumerateObject())
                    colors[c.Name] = c.Value.ValueKind == JsonValueKind.String ? c.Value.GetString() ?? "" : "";
                var markers = new List<string> { "GOST-012" };
                var pic = J.Optional(s, "pictogram");
                SignPictogram? pictogram = null;
                if (pic != null && pic.Value.ValueKind == JsonValueKind.Object)
                {
                    var pp = p + "/pictogram";
                    var marker = J.OptStr(pic.Value, "marker", file, pp);
                    if (marker != null) markers.Add(marker);
                    pictogram = new SignPictogram(J.Str(pic.Value, "id", file, pp),
                        J.OptBool(pic.Value, "placeholder", false, file, pp),
                        marker, ReadVerificationAt(pic.Value, file, pp));
                }
                var txt = J.Optional(s, "text");
                SignTextSpec? text = null;
                if (txt != null && txt.Value.ValueKind == JsonValueKind.Object)
                {
                    var tp = p + "/text";
                    var marker = J.OptStr(txt.Value, "marker", file, tp);
                    if (marker != null) markers.Add(marker);
                    text = new SignTextSpec(J.Str(txt.Value, "kind", file, tp),
                        J.Str(txt.Value, "value", file, tp), ReadVerificationAt(txt.Value, file, tp), marker);
                }
                var shapeRaw = J.Str(s, "shape", file, p);
                CarrierShape shape;
                try { shape = CarrierShapes.Parse(shapeRaw); }
                catch (FormatException ex) { throw new ConfigException(file, p + "/shape", ex.Message, "используйте имя из глоссария 7.3"); }

                signs.Add(new SignDefinition(number, J.Str(s, "name", file, p),
                    (int)J.Num(s, "group", file, p), shape, J.Str(s, "sizeTableId", file, p),
                    J.OptBool(s, "isPlate", false, file, p), colors, pictogram, text,
                    J.OptStr(s, "parameterKind", file, p), J.OptStr(s, "note", file, p),
                    J.ReadNormRef(s, "normRef", file, p), ReadVerificationAt(s, file, p), markers));
            }
            return new SignCatalog(J.Str(e, "catalog", file, ""), J.Str(e, "title", file, ""),
                J.ReadNormRef(e, "normRef", file, ""), J.ReadVerification(e, file, ""),
                ReadVerificationRaw(e, "colorStatus", file), J.OptStr(e, "colorNote", file, "") ?? "",
                J.StringList(e, "markers", file, ""), signs);
        }

        private static Verification ReadVerificationRaw(JsonElement e, string name, string file)
        {
            var raw = J.Str(e, name, file, "");
            try { return VerificationParser.Parse(raw); }
            catch (FormatException ex) { throw new ConfigException(file, "/" + name, ex.Message, "используйте допустимый статус"); }
        }
    }
}
