using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using OddMaster.Core.Catalog;

namespace OddMaster.Core.Pictograms
{
    /// <summary>Сетка прил. Б: сколько клеток покрывает размер носителя.</summary>
    public sealed class PictogramGrid
    {
        /// <summary>Создаёт сетку.</summary>
        public PictogramGrid(int cellsAcross, string spans) { CellsAcross = cellsAcross; Spans = spans; }

        /// <summary>Число клеток на размер A/B/D/H.</summary>
        public int CellsAcross { get; }
        /// <summary>Какой размер носителя покрывают клетки.</summary>
        public string Spans { get; }
    }

    /// <summary>Описание пиктограммы в клетках сетки (ADR-0023).</summary>
    public sealed class PictogramDefinition
    {
        /// <summary>Создаёт описание.</summary>
        public PictogramDefinition(string id, string sign, string status, string source,
            PictogramGrid grid, List<string> layers, string? note)
        {
            Id = id; Sign = sign; Status = status; Source = source;
            Grid = grid; Layers = layers; Note = note;
        }

        /// <summary>Идентификатор.</summary>
        public string Id { get; }
        /// <summary>Номер знака.</summary>
        public string Sign { get; }
        /// <summary>Статус: Placeholder / StudentTranscribed / StudentVerified / Unverified.</summary>
        public string Status { get; }
        /// <summary>Источник (страница PDF прил. Б).</summary>
        public string Source { get; }
        /// <summary>Сетка.</summary>
        public PictogramGrid Grid { get; }
        /// <summary>Слои в виде JSON-строк (роль/цвет/фигуры).</summary>
        public IReadOnlyList<string> Layers { get; }
        /// <summary>Примечание.</summary>
        public string? Note { get; }

        /// <summary>Сериализует в JSON по схеме pictogram.schema.json.</summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"id\": " + Quote(Id) + ",");
            sb.AppendLine("  \"sign\": " + Quote(Sign) + ",");
            sb.AppendLine("  \"status\": " + Quote(Status) + ",");
            sb.AppendLine("  \"source\": " + Quote(Source) + ",");
            if (Note != null) sb.AppendLine("  \"note\": " + Quote(Note) + ",");
            sb.AppendLine("  \"grid\": { \"cellsAcross\": " + Grid.CellsAcross.ToString(CultureInfo.InvariantCulture)
                + ", \"spans\": " + Quote(Grid.Spans) + ", \"origin\": \"center\", \"yAxis\": \"up\" },");
            sb.AppendLine("  \"layers\": [");
            for (int i = 0; i < Layers.Count; i++)
                sb.Append("    " + Layers[i] + (i < Layers.Count - 1 ? "," : "") + "\n");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString().Replace("\r\n", "\n");
        }

        private static string Quote(string value) => JsonSerializer.Serialize(value);
    }

    /// <summary>Загрузка и проверка реестра пиктограмм (P2).</summary>
    public static class PictogramRegistry
    {
        /// <summary>Читает все *.json из config/pictograms.</summary>
        public static List<PictogramDefinition> Load(string root, out List<string> errors)
        {
            errors = new List<string>();
            var result = new List<PictogramDefinition>();
            var dir = Path.Combine(root, "config/pictograms");
            if (!Directory.Exists(dir)) { errors.Add("нет папки config/pictograms"); return result; }
            foreach (var file in Directory.GetFiles(dir, "*.json").OrderBy(x => x, StringComparer.Ordinal))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var e = doc.RootElement;
                if (!e.TryGetProperty("pictograms", out var items)) continue;
                foreach (var item in items.EnumerateArray())
                {
                    var id = item.TryGetProperty("id", out var v) ? v.GetString() ?? "" : "";
                    if (id.Length == 0) { errors.Add(Path.GetFileName(file) + ": нет id"); continue; }
                    var sign = item.TryGetProperty("sign", out var s) ? s.GetString() ?? "" : "";
                    var status = item.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
                    var source = item.TryGetProperty("source", out var src) ? src.GetString() ?? "" : "";
                    var note = item.TryGetProperty("note", out var n) ? n.GetString() : null;
                    var gridElement = item.TryGetProperty("grid", out var g) ? g : default;
                    var cells = gridElement.ValueKind == JsonValueKind.Object
                        && gridElement.TryGetProperty("cellsAcross", out var ca) ? ca.GetInt32() : 0;
                    var spans = gridElement.ValueKind == JsonValueKind.Object
                        && gridElement.TryGetProperty("spans", out var sp) ? sp.GetString() ?? "" : "";
                    var layers = new List<string>();
                    if (item.TryGetProperty("layers", out var ls))
                        foreach (var layer in ls.EnumerateArray()) layers.Add(layer.GetRawText());
                    result.Add(new PictogramDefinition(id, sign, status, source,
                        new PictogramGrid(cells, spans), layers, note));
                }
            }
            return result;
        }

        /// <summary>Проверяет реестр: статус, сетка, слои, существование знака в каталоге.</summary>
        public static List<string> Validate(string root, SignCatalog catalog)
        {
            var loaded = Load(root, out var errors);
            var validStatuses = new HashSet<string>(StringComparer.Ordinal)
            {
                "Placeholder", "StudentTranscribed", "StudentVerified", "Unverified"
            };
            var validSpans = new HashSet<string>(StringComparer.Ordinal) { "A", "B", "D", "H" };
            foreach (var p in loaded)
            {
                if (!validStatuses.Contains(p.Status)) errors.Add(p.Id + ": неизвестный статус «" + p.Status + "»");
                if (p.Source.Length == 0) errors.Add(p.Id + ": нет source");
                if (p.Grid.CellsAcross <= 0) errors.Add(p.Id + ": cellsAcross должен быть > 0");
                if (!validSpans.Contains(p.Grid.Spans)) errors.Add(p.Id + ": spans должен быть A/B/D/H");
                if (p.Layers.Count == 0) errors.Add(p.Id + ": нет слоёв");
                if (catalog.Find(p.Sign) == null) errors.Add(p.Id + ": знак " + p.Sign + " отсутствует в каталоге");
            }
            return errors;
        }
    }
}
