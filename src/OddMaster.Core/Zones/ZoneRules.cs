using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OddMaster.Core.Config;

namespace OddMaster.Core.Zones
{
    /// <summary>Правило таблички зоны действия: какие знаки начинают/заканчивают зону.</summary>
    public sealed class PlateZoneRule
    {
        /// <summary>Создаёт правило.</summary>
        public PlateZoneRule(string number, string role, List<string> startSigns,
            List<string> endSigns, string status)
        {
            Number = number; Role = role; StartSigns = startSigns; EndSigns = endSigns; Status = status;
        }

        /// <summary>Номер таблички.</summary>
        public string Number { get; }
        /// <summary>Роль (Зона действия).</summary>
        public string Role { get; }
        /// <summary>Знаки начала зоны.</summary>
        public IReadOnlyList<string> StartSigns { get; }
        /// <summary>Знаки конца зоны.</summary>
        public IReadOnlyList<string> EndSigns { get; }
        /// <summary>Статус проверки.</summary>
        public string Status { get; }
    }

    /// <summary>Набор правил зон и парности из config/rules/zones.json (P3, G3).</summary>
    public sealed class ZoneRuleSet
    {
        /// <summary>Создаёт набор.</summary>
        public ZoneRuleSet(List<PlateZoneRule> plates, List<string> pairing, string note)
        {
            Plates = plates; Pairing = pairing; Note = note;
        }

        /// <summary>Правила табличек.</summary>
        public IReadOnlyList<PlateZoneRule> Plates { get; }
        /// <summary>Пары «начало → конец» в виде строк.</summary>
        public IReadOnlyList<string> Pairing { get; }
        /// <summary>Пояснение.</summary>
        public string Note { get; }

        /// <summary>Истина, если табличка объявлена как задающая зону.</summary>
        public bool IsZonePlate(string number) => Plates.Any(p => p.Number == number);

        /// <summary>Загружает правила. Файл должен иметь normRef и verification.</summary>
        public static ZoneRuleSet Load(string root)
        {
            var file = "config/rules/zones.json";
            using var doc = JsonConfig.Load(root, file);
            var e = doc.RootElement;
            if (!e.TryGetProperty("normRef", out _) || !e.TryGetProperty("verification", out _))
                throw new ConfigException(file, "", "нет normRef/verification", "добавьте ссылку на норму и статус");
            var plates = new List<PlateZoneRule>();
            foreach (var p in e.GetProperty("plates").EnumerateArray())
            {
                plates.Add(new PlateZoneRule(
                    p.GetProperty("number").GetString() ?? "",
                    p.GetProperty("role").GetString() ?? "",
                    Strings(p, "startSigns"), Strings(p, "endSigns"),
                    p.GetProperty("status").GetString() ?? "Unverified"));
            }
            var pairing = new List<string>();
            foreach (var p in e.GetProperty("pairing").EnumerateArray())
                pairing.Add((p.GetProperty("start").GetString() ?? "") + " → "
                    + (p.GetProperty("end").GetString() ?? ""));
            return new ZoneRuleSet(plates, pairing,
                e.TryGetProperty("note", out var n) ? n.GetString() ?? "" : "");
        }

        private static List<string> Strings(JsonElement e, string name)
        {
            var result = new List<string>();
            if (e.TryGetProperty(name, out var array))
                foreach (var item in array.EnumerateArray()) result.Add(item.GetString() ?? "");
            return result;
        }
    }

    /// <summary>Реестр справочных подсказок Р 52289; текст вносит студент (G3).</summary>
    public sealed class ReferenceHints
    {
        /// <summary>Загружает реестр. Пустой список — честный ответ, что данных пока нет.</summary>
        public static List<string> Load(string root)
        {
            var file = "config/rules/hints-52289.json";
            using var doc = JsonConfig.Load(root, file);
            var result = new List<string>();
            var e = doc.RootElement;
            if (e.TryGetProperty("hints", out var hints))
                foreach (var h in hints.EnumerateArray())
                    result.Add((h.TryGetProperty("sign", out var s) ? s.GetString() ?? "" : "")
                        + " " + (h.TryGetProperty("clause", out var c) ? c.GetString() ?? "" : ""));
            return result;
        }
    }
}
