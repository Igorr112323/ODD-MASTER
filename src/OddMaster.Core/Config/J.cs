using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Config
{
    /// <summary>Маленькие помощники для чтения JSON с предсказуемыми русскими ошибками.</summary>
    internal static class J
    {
        public static JsonElement Property(JsonElement element, string name, string file, string pointer)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
                throw new ConfigException(file, pointer + "/" + name, "отсутствует обязательное поле", "добавьте поле «" + name + "»");
            return value;
        }

        public static JsonElement? Optional(JsonElement element, string name)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)) return value;
            return null;
        }

        public static string Str(JsonElement element, string name, string file, string pointer)
        {
            var value = Property(element, name, file, pointer);
            if (value.ValueKind != JsonValueKind.String)
                throw new ConfigException(file, pointer + "/" + name, "ожидалась строка", "укажите строковое значение");
            return value.GetString() ?? string.Empty;
        }

        public static string? OptStr(JsonElement element, string name, string file, string pointer)
        {
            var value = Optional(element, name);
            if (value == null || value.Value.ValueKind == JsonValueKind.Null) return null;
            if (value.Value.ValueKind != JsonValueKind.String)
                throw new ConfigException(file, pointer + "/" + name, "ожидалась строка или null", "укажите строку или null");
            return value.Value.GetString();
        }

        public static double Num(JsonElement element, string name, string file, string pointer)
        {
            var value = Property(element, name, file, pointer);
            if (value.ValueKind != JsonValueKind.Number)
                throw new ConfigException(file, pointer + "/" + name, "ожидалось число", "укажите число без кавычек");
            return value.GetDouble();
        }

        public static double? OptNum(JsonElement element, string name, string file, string pointer)
        {
            var value = Optional(element, name);
            if (value == null || value.Value.ValueKind == JsonValueKind.Null) return null;
            if (value.Value.ValueKind != JsonValueKind.Number)
                throw new ConfigException(file, pointer + "/" + name, "ожидалось число или null", "укажите число или null");
            return value.Value.GetDouble();
        }

        public static bool OptBool(JsonElement element, string name, bool fallback, string file, string pointer)
        {
            var value = Optional(element, name);
            if (value == null) return fallback;
            if (value.Value.ValueKind == JsonValueKind.True) return true;
            if (value.Value.ValueKind == JsonValueKind.False) return false;
            throw new ConfigException(file, pointer + "/" + name, "ожидалось true/false", "укажите булево значение");
        }

        public static List<JsonElement> Array(JsonElement element, string name, string file, string pointer)
        {
            var value = Property(element, name, file, pointer);
            if (value.ValueKind != JsonValueKind.Array)
                throw new ConfigException(file, pointer + "/" + name, "ожидался массив", "укажите массив []");
            return value.EnumerateArray().ToList();
        }

        public static List<JsonElement> OptArray(JsonElement element, string name, string file, string pointer)
        {
            var value = Optional(element, name);
            if (value == null) return new List<JsonElement>();
            if (value.Value.ValueKind != JsonValueKind.Array)
                throw new ConfigException(file, pointer + "/" + name, "ожидался массив", "укажите массив []");
            return value.Value.EnumerateArray().ToList();
        }

        public static List<string> StringList(JsonElement element, string name, string file, string pointer) =>
            OptArray(element, name, file, pointer).Select((x, i) =>
            {
                if (x.ValueKind != JsonValueKind.String)
                    throw new ConfigException(file, pointer + "/" + name + "/" + i, "ожидалась строка", "замените элемент строкой");
                return x.GetString() ?? string.Empty;
            }).ToList();

        public static NormRef NormRefAt(JsonElement element, string file, string pointer)
        {
            var doc = Str(element, "doc", file, pointer);
            var clause = Str(element, "clause", file, pointer);
            return new NormRef(doc, clause, OptStr(element, "table", file, pointer), OptStr(element, "figure", file, pointer));
        }

        public static NormRef ReadNormRef(JsonElement element, string name, string file, string pointer) =>
            NormRefAt(Property(element, name, file, pointer), file, pointer + "/" + name);

        public static Verification ReadVerification(JsonElement element, string file, string pointer)
        {
            var raw = Str(element, "verification", file, pointer);
            try { return VerificationParser.Parse(raw); }
            catch (FormatException ex)
            {
                throw new ConfigException(file, pointer + "/verification", ex.Message,
                    "используйте Placeholder, Unverified, CuratorExtracted или StudentVerified");
            }
        }

        public static SignNumberSet SignsSet(JsonElement element, string name, string file, string pointer)
        {
            var raw = Str(element, name, file, pointer);
            try { return SignNumberSet.Parse(raw); }
            catch (FormatException ex)
            {
                throw new ConfigException(file, pointer + "/" + name, ex.Message,
                    "проверьте номера и диапазоны, например «1.5 - 1.7, 2.4»");
            }
        }

        /// <summary>Число в виде decimal без потери точности (важно для кратности 0,1).</summary>
        public static decimal Dec(JsonElement element, string file, string pointer)
        {
            if (element.ValueKind != JsonValueKind.Number)
                throw new ConfigException(file, pointer, "ожидалось число", "укажите число без кавычек");
            return decimal.Parse(element.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
