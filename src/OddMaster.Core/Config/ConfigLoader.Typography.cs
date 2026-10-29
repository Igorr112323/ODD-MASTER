using System;
using System.Collections.Generic;
using System.Text.Json;
using OddMaster.Core.Typography;

namespace OddMaster.Core.Config
{
    /// <summary>Загрузка метрик шрифта (CR-07).</summary>
    public static partial class ConfigLoader
    {
        /// <summary>Читает config/typography/fonts.json.</summary>
        public static FontMetrics LoadFontMetrics(string root)
        {
            var file = ConfigPaths.FontMetrics;
            using var doc = JsonConfig.Load(root, file);
            var e = doc.RootElement;
            var ratios = new Dictionary<string, double>(StringComparer.Ordinal);
            var element = J.Property(e, "advanceRatios", file, "");
            foreach (var p in element.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.Number) ratios[p.Name] = p.Value.GetDouble();
            return new FontMetrics(
                J.Str(e, "fontId", file, ""), J.Str(e, "fontFamily", file, ""),
                J.Num(e, "capHeightRatio", file, ""), J.Num(e, "defaultAdvanceRatio", file, ""),
                J.Num(e, "maxHeightRatio", file, ""), J.Num(e, "paddingRatio", file, ""),
                J.Num(e, "lineGapRatio", file, ""), ratios, J.Num(e, "minCapHeightMm", file, ""),
                J.ReadNormRef(e, "normRef", file, ""), J.ReadVerification(e, file, ""),
                J.OptStr(e, "note", file, ""));
        }
    }
}
