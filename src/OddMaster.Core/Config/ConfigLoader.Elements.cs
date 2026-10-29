using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using OddMaster.Core.Composition;

namespace OddMaster.Core.Config
{
    /// <summary>Загрузка библиотеки примитивов состава (CR-22).</summary>
    public static partial class ConfigLoader
    {
        /// <summary>Загружает config/elements/elements.json.</summary>
        public static CompositionLibrary LoadElements(string root)
        {
            var file = ConfigPaths.Elements;
            using var doc = JsonConfig.Load(root, file);
            var e = doc.RootElement;

            double defaults = 0;
            double Def(string name, double fallback)
            {
                var d = J.Optional(e, "defaults");
                if (d != null && d.Value.ValueKind == JsonValueKind.Object &&
                    d.Value.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number)
                    return v.GetDouble();
                return fallback;
            }

            // Статус элемента наследуется от библиотеки, если у элемента он не задан явно.
            var libraryVerification = ReadVerificationAt(e, file, "");
            var items = J.Property(e, "elements", file, "");
            var elements = new Dictionary<string, CompositionElement>(StringComparer.Ordinal);
            foreach (var p in items.EnumerateObject())
            {
                var pointer = "/elements/" + p.Name;
                if (p.Value.ValueKind != JsonValueKind.Object)
                    throw new ConfigException(file, pointer, "элемент должен быть объектом", "опишите элемент полями kind и color");
                var kindRaw = J.Str(p.Value, "kind", file, pointer);
                CompositionKind kind;
                switch (kindRaw)
                {
                    case "contour": kind = CompositionKind.Contour; break;
                    case "rectangle": kind = CompositionKind.Rectangle; break;
                    case "cross": kind = CompositionKind.Cross; break;
                    case "band": kind = CompositionKind.Band; break;
                    case "bundle": kind = CompositionKind.Bundle; break;
                    case "triangle": kind = CompositionKind.Triangle; break;
                    case "panel": kind = CompositionKind.Panel; break;
                    case "nested": kind = CompositionKind.Nested; break;
                    case "arrow": kind = CompositionKind.Arrow; break;
                    default:
                        throw new ConfigException(file, pointer + "/kind", "неизвестный вид элемента «" + kindRaw + "»",
                            "допустимо: contour, rectangle, cross, band, bundle, triangle, panel, nested, arrow");
                }
                var filled = kind != CompositionKind.Contour && kind != CompositionKind.Band &&
                             kind != CompositionKind.Bundle && kind != CompositionKind.Nested
                    ? J.OptBool(p.Value, "filled", true, file, pointer)
                    : J.OptBool(p.Value, "filled", false, file, pointer);
                var direction = J.OptStr(p.Value, "direction", file, pointer);
                if (kind == CompositionKind.Arrow && direction == null)
                    throw new ConfigException(file, pointer + "/direction", "у стрелки нет направления",
                        "укажите up, down, left или right");
                elements[p.Name] = new CompositionElement(
                    p.Name, kind, J.OptStr(p.Value, "color", file, pointer) ?? "Black", filled,
                    J.OptNum(p.Value, "xRatio", file, pointer) ?? Def("xRatio", 0),
                    J.OptNum(p.Value, "yRatio", file, pointer) ?? Def("yRatio", 0),
                    J.OptNum(p.Value, "widthRatio", file, pointer) ?? 0.5,
                    J.OptNum(p.Value, "heightRatio", file, pointer) ?? 0.2,
                    J.OptNum(p.Value, "bandRatio", file, pointer) ?? Def("bandRatio", 0.14),
                    J.OptNum(p.Value, "lengthRatio", file, pointer) ?? Def("lengthRatio", 1.0),
                    J.OptNum(p.Value, "spacingRatio", file, pointer) ?? Def("spacingRatio", 0.09),
                    (int)Math.Round(J.OptNum(p.Value, "count", file, pointer) ?? Def("count", 5)),
                    J.OptNum(p.Value, "angleDeg", file, pointer) ?? 0,
                    J.OptNum(p.Value, "strokeRatio", file, pointer) ?? Def("strokeRatio", 0.006),
                    J.OptNum(p.Value, "sizeRatio", file, pointer) ?? 0.3,
                    direction,
                    J.OptStr(p.Value, "note", file, pointer),
                    p.Value.TryGetProperty("verification", out _)
                        ? ReadVerificationAt(p.Value, file, pointer)
                        : libraryVerification,
                    Markers(p.Value, file, pointer));
                defaults++;
            }
            if (defaults == 0)
                throw new ConfigException(file, "/elements", "библиотека элементов пуста", "добавьте хотя бы один элемент");

            return new CompositionLibrary("elements.mvp-30",
                J.ReadNormRef(e, "normRef", file, ""), libraryVerification,
                J.OptStr(e, "source", file, "") ?? "", J.OptStr(e, "note", file, "") ?? "",
                J.StringList(e, "markers", file, ""), elements);
        }

        private static string? Markers(JsonElement element, string file, string pointer)
        {
            var list = J.OptArray(element, "markers", file, pointer);
            return list.Count > 0 ? list[0].GetString() : null;
        }
    }
}
