using System;
using System.Collections.Generic;

namespace OddMaster.Render.Svg
{
    /// <summary>
    /// Семантические цвета → экранный RGB. Это ТОЛЬКО отображение, не колориметрия ГОСТ (GOST-012).
    /// </summary>
    public static class SvgPalette
    {
        private static readonly Dictionary<string, string> Colors = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Red"] = "#D7191C",
            ["White"] = "#FFFFFF",
            ["Blue"] = "#0057B8",
            ["Yellow"] = "#FFD200",
            ["Black"] = "#1A1A1A",
            ["Green"] = "#00843D",
            ["Grey"] = "#808080"
        };

        /// <summary>Возвращает HEX-цвет для семантического имени. Неизвестное имя — серый.</summary>
        public static string Resolve(string semanticName)
        {
            if (semanticName != null && Colors.TryGetValue(semanticName, out var hex)) return hex;
            return "#808080";
        }

        /// <summary>Проверяет, известно ли имя цвета.</summary>
        public static bool IsKnown(string semanticName) =>
            semanticName != null && Colors.ContainsKey(semanticName);
    }
}
