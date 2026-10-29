using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OddMaster.Core.Catalog;
using OddMaster.Core.Norms;
using OddMaster.Core.Scheme;

namespace OddMaster.Core.Document
{
    /// <summary>Модель листа схемы: формат, масштаб, поля (P3).</summary>
    public sealed class SheetModel
    {
        /// <summary>Создаёт лист.</summary>
        public SheetModel(string format, double scaleDenominator, double marginMm)
        {
            Format = format; ScaleDenominator = scaleDenominator; MarginMm = marginMm;
        }

        /// <summary>Формат листа.</summary>
        [JsonPropertyName("format")]
        public string Format { get; }
        /// <summary>Знаменатель масштаба.</summary>
        [JsonPropertyName("scaleDenominator")]
        public double ScaleDenominator { get; }
        /// <summary>Поля, мм на бумаге.</summary>
        [JsonPropertyName("marginMm")]
        public double MarginMm { get; }

        /// <summary>JSON листа (рамка из СПДС — [ПРОВЕРИТЬ В SDK], пока своя из конфига).</summary>
        public string ToJson() => JsonSerializer.Serialize(this,
            new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    /// <summary>Строка легенды.</summary>
    public sealed class LegendEntry
    {
        /// <summary>Создаёт строку.</summary>
        public LegendEntry(SignNumber number, string name, int count)
        {
            Number = number; Name = name; Count = count;
        }

        /// <summary>Номер знака.</summary>
        public SignNumber Number { get; }
        /// <summary>Наименование.</summary>
        public string Name { get; }
        /// <summary>Сколько раз использован.</summary>
        public int Count { get; }
    }

    /// <summary>Легенда: уникальные знаки схемы, порядок по номеру (D6).</summary>
    public static class LegendModel
    {
        /// <summary>Собирает легенду.</summary>
        public static IReadOnlyList<LegendEntry> Build(SchemeModel scheme, SignCatalog catalog)
        {
            if (scheme == null) throw new ArgumentNullException(nameof(scheme));
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var post in scheme.Posts)
                foreach (var sign in post.Mounts)
                {
                    var key = sign.Number.ToString();
                    counts.TryGetValue(key, out var current);
                    counts[key] = current + 1;
                }
            return counts.OrderBy(p => SignNumber.Parse(p.Key))
                .Select(p => new LegendEntry(SignNumber.Parse(p.Key),
                    catalog.Find(p.Key)?.Name ?? "Нет в каталоге", p.Value)).ToList();
        }

        /// <summary>Markdown легенды.</summary>
        public static string ToMarkdown(IReadOnlyList<LegendEntry> entries)
        {
            var lines = new List<string>
            {
                "# Легенда схемы (ЧЕРНОВИК)", "",
                "| Номер знака | Наименование | Количество |", "|---|---|---|"
            };
            foreach (var e in entries)
                lines.Add("| " + e.Number + " | " + e.Name + " | "
                    + e.Count.ToString(CultureInfo.InvariantCulture) + " |");
            return string.Join("\n", lines) + "\n";
        }
    }
}
