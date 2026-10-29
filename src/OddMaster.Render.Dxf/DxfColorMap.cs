using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OddMaster.Render.Dxf
{
    /// <summary>
    /// Соответствие семантических имён цветов каталога индексам ACI.
    /// ACI — палитра AutoCAD/DXF, а не колориметрия ГОСТ: статус DISPLAY_ONLY.
    /// </summary>
    public static class DxfColorMap
    {
        private static readonly Dictionary<string, int> Map = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            // 7 — «по фону»: на тёмном фоне белый, на светлом чёрный; так ведёт себя САПР.
            ["Black"] = 7,
            // 255 — чистый белый из расширенной палитры ACI.
            ["White"] = 255,
            ["Red"] = 1,
            ["Yellow"] = 2,
            ["Blue"] = 5,
            ["Green"] = 3,
            // Наружная кайма без подтверждённого цвета — серый, чтобы отличать от чёрного.
            ["OuterBorderUnverified"] = 8,
            ["Grey"] = 8
        };

        /// <summary>Индекс ACI по семантическому имени. Неизвестное имя — 7 («по фону»).</summary>
        public static int Aci(string colorName)
        {
            if (colorName == null) return 7;
            return Map.TryGetValue(colorName, out var value) ? value : 7;
        }

        /// <summary>Есть ли имя в палитре стенда (для проверок полноты каталога).</summary>
        public static bool IsKnown(string colorName) => colorName != null && Map.ContainsKey(colorName);

        /// <summary>Все имена палитры в устойчивом порядке.</summary>
        public static IReadOnlyCollection<string> Names => new List<string>(Map.Keys);

        /// <summary>Приводит индекс к допустимому диапазону ACI: 0 — «по слою», иначе 1…255.</summary>
        public static int Normalize(int colorIndex)
        {
            if (colorIndex <= 0) return 0;
            return colorIndex > 255 ? 7 : colorIndex;
        }

        /// <summary>Читаемое описание палитры для отчётов.</summary>
        public static string Describe()
        {
            var sb = new StringBuilder();
            foreach (var pair in new SortedDictionary<string, int>(Map, StringComparer.Ordinal))
                sb.Append(pair.Key).Append('=').Append(pair.Value.ToString(CultureInfo.InvariantCulture)).Append("; ");
            return sb.ToString().TrimEnd();
        }
    }
}
