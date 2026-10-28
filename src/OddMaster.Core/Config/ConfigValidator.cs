using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OddMaster.Core.Catalog;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Config
{
    /// <summary>Результат проверки конфигов: ошибки, предупреждения и нормативные пробелы.</summary>
    public sealed class ConfigValidationResult
    {
        /// <summary>Создаёт пустой результат.</summary>
        public ConfigValidationResult() { }

        /// <summary>Ошибки: битый конфиг, отсутствует normRef/verification.</summary>
        public List<string> Errors { get; } = new List<string>();
        /// <summary>Предупреждения: несостыковки стандарта, неоднозначности.</summary>
        public List<string> Warnings { get; } = new List<string>();
        /// <summary>Находки о пробелах в данных.</summary>
        public List<Finding> Gaps { get; } = new List<Finding>();

        /// <summary>Истина, если ошибок нет.</summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>Детерминированный Markdown-отчёт о проверке.</summary>
        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Проверка нормативных данных");
            sb.AppendLine();
            sb.AppendLine($"Ошибки: {Errors.Count}. Предупреждения: {Warnings.Count}. Пробелы данных: {Gaps.Count}.");
            sb.AppendLine();
            if (Errors.Count > 0)
            {
                sb.AppendLine("## Ошибки");
                foreach (var e in Errors) sb.AppendLine("- " + e);
                sb.AppendLine();
            }
            if (Warnings.Count > 0)
            {
                sb.AppendLine("## Предупреждения (несостыковки стандарта)");
                foreach (var w in Warnings) sb.AppendLine("- " + w);
                sb.AppendLine();
            }
            if (Gaps.Count > 0)
            {
                sb.AppendLine("## Пробелы в данных");
                foreach (var g in Gaps) sb.AppendLine("- " + g.Format());
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }

    /// <summary>Валидатор конфигов: русские сообщения, JSON Pointer, автоматические несостыковки 8.5.</summary>
    public static class ConfigValidator
    {
        private static readonly Regex MarkerPattern = new Regex(@"GOST-[0-9]{3}", RegexOptions.Compiled);

        /// <summary>Проверяет все конфиги в корне репозитория.</summary>
        public static ConfigValidationResult Validate(string root)
        {
            var result = new ConfigValidationResult();
            NormativeConfigSet set;
            try { set = ConfigLoader.Load(root); }
            catch (ConfigException ex) { result.Errors.Add(ex.Message); return result; }

            CheckFilesHaveProvenance(root, result);
            CheckSchemasAreParseable(root, result);
            CheckConfigsMatchSchemas(root, result);
            CheckCatalog(set, result);
            CheckMarkers(root, result);
            DetectDataGaps(set, result);
            return result;
        }

        private static void CheckFilesHaveProvenance(string root, ConfigValidationResult result)
        {
            foreach (var file in ConfigPaths.All)
            {
                try
                {
                    using var doc = JsonConfig.Load(root, file);
                    var e = doc.RootElement;
                    var normRef = J.Optional(e, "normRef");
                    if (normRef == null || normRef.Value.ValueKind != JsonValueKind.Object
                        || !normRef.Value.TryGetProperty("doc", out _) || !normRef.Value.TryGetProperty("clause", out _))
                        result.Errors.Add($"{file}: отсутствует normRef с полями doc и clause. Как исправить: добавьте ссылку на документ и пункт.");
                    if (!J.Optional(e, "verification").HasValue)
                        result.Errors.Add($"{file}: отсутствует verification. Как исправить: укажите статус проверки.");
                    else
                    {
                        try { J.ReadVerification(e, file, ""); }
                        catch (ConfigException ex) { result.Errors.Add(ex.Message); }
                    }
                }
                catch (ConfigException ex) { result.Errors.Add(ex.Message); }
            }
        }

        private static void CheckSchemasAreParseable(string root, ConfigValidationResult result)
        {
            var dir = Path.Combine(root, "config/schema");
            if (!Directory.Exists(dir))
            {
                result.Errors.Add("config/schema: папка не найдена. Как исправить: добавьте JSON Schema для всех конфигов.");
                return;
            }
            var files = Directory.GetFiles(dir, "*.schema.json").OrderBy(x => x, StringComparer.Ordinal);
            if (files.Count() < 12)
                result.Errors.Add($"config/schema: найдено {files.Count()} схем, ожидается 12. Как исправить: добавьте недостающие.");
            foreach (var full in files)
            {
                var name = Path.GetFileName(full);
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(full));
                    var e = doc.RootElement;
                    if (!e.TryGetProperty("$schema", out _) || !e.TryGetProperty("title", out _)
                        || !e.TryGetProperty("type", out _) || !e.TryGetProperty("required", out _))
                        result.Errors.Add($"config/schema/{name}: нет обязательных полей $schema/title/type/required.");
                }
                catch (JsonException ex) { result.Errors.Add($"config/schema/{name}: некорректный JSON: {ex.Message}."); }
            }
        }

        private static readonly (string Config, string Schema)[] ConfigSchemaPairs =
        {
            (ConfigPaths.MainProfile, "size-profile.schema.json"),
            (ConfigPaths.SecondProfile, "size-profile.schema.json"),
            (ConfigPaths.Borders, "borders.schema.json"),
            (ConfigPaths.Parameters, "parameters.schema.json"),
            (ConfigPaths.Variants, "variants.schema.json"),
            (ConfigPaths.Sources, "sources.schema.json"),
            (ConfigPaths.Catalog, "sign-catalog.schema.json"),
            (ConfigPaths.Controls, "controls.schema.json"),
            (ConfigPaths.Drawing, "drawing.schema.json"),
            (ConfigPaths.MigrationMap, "migration-map.schema.json"),
            (ConfigPaths.FontMetrics, "font-metrics.schema.json"),
            (ConfigPaths.StatementTable, "statement-table.schema.json")
        };

        /// <summary>
        /// Полноценного движка JSON Schema нет (нет NuGet), поэтому проверяем главное:
        /// корневой объект имеет все поля из required соответствующей схемы.
        /// </summary>
        private static void CheckConfigsMatchSchemas(string root, ConfigValidationResult result)
        {
            foreach (var pair in ConfigSchemaPairs)
            {
                var schemaPath = Path.Combine(root, "config/schema", pair.Schema);
                var configPath = Path.Combine(root, pair.Config);
                if (!File.Exists(schemaPath) || !File.Exists(configPath)) continue;
                using var schemaDoc = JsonDocument.Parse(File.ReadAllText(schemaPath));
                using var configDoc = JsonDocument.Parse(File.ReadAllText(configPath));
                if (!schemaDoc.RootElement.TryGetProperty("required", out var required)) continue;
                foreach (var field in required.EnumerateArray())
                {
                    var name = field.GetString();
                    if (name != null && !configDoc.RootElement.TryGetProperty(name, out _))
                        result.Errors.Add($"{pair.Config}: нет обязательного поля «{name}» из {pair.Schema}. " +
                            "Как исправить: добавьте поле по схеме.");
                }
            }
        }

        private static void CheckCatalog(NormativeConfigSet set, ConfigValidationResult result)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var sign in set.Catalog.Signs)
            {
                var id = sign.Number.ToString();
                if (!seen.Add(id))
                    result.Errors.Add($"config/catalog/signs.poc.json: дубль знака {id}. Как исправить: оставьте одну запись.");
                if (string.IsNullOrWhiteSpace(sign.Name))
                    result.Errors.Add($"config/catalog/signs.poc.json: у знака {id} пустое наименование.");
                if (sign.Verification == Verification.StudentVerified)
                    result.Warnings.Add($"config/catalog/signs.poc.json: знак {id} помечен StudentVerified, но агент не имеет права ставить этот статус.");
                if (!set.MainProfile.Tables.Any(t => t.Id == sign.SizeTableId))
                    result.Errors.Add($"config/catalog/signs.poc.json: у знака {id} неизвестная таблица размеров {sign.SizeTableId}.");
                var rows = SizeResolver.FindRowLocations(set.MainProfile, sign.Number);
                if (rows.Count == 0)
                    result.Gaps.Add(Finding.Warning("GAP-CATALOG-NONE",
                        $"Знак {id} не найден ни в одной строке таблиц размеров.", id, set.MainProfile.NormRef,
                        set.MainProfile.Verification, "GOST-003"));
                else if (rows.Count > 1)
                    result.Gaps.Add(Finding.Warning("GAP-CATALOG-MULTI",
                        $"Знак {id} найден в нескольких строках: {string.Join(", ", rows)}.", id,
                        set.MainProfile.NormRef, set.MainProfile.Verification, "GOST-010"));
            }
        }

        private static void CheckMarkers(string root, ConfigValidationResult result)
        {
            var queuePath = Path.Combine(root, "docs/VERIFICATION_QUEUE.md");
            if (!File.Exists(queuePath))
            {
                result.Errors.Add("docs/VERIFICATION_QUEUE.md: файл не найден. Как исправить: заведите реестр GOST-маркеров.");
                return;
            }
            var registryText = File.ReadAllText(queuePath);
            var registered = new HashSet<string>(MarkerPattern.Matches(registryText).Select(m => m.Value), StringComparer.Ordinal);
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in ConfigPaths.All)
            {
                var full = Path.Combine(root, file);
                if (!File.Exists(full)) continue;
                foreach (var marker in MarkerPattern.Matches(File.ReadAllText(full)).Cast<Match>()
                             .Select(m => m.Value)) used.Add(marker);
            }
            foreach (var marker in used.OrderBy(x => x, StringComparer.Ordinal))
                if (!registered.Contains(marker))
                    result.Errors.Add($"Маркер {marker} используется в конфиге, но отсутствует в docs/VERIFICATION_QUEUE.md.");
            foreach (var marker in registered.OrderBy(x => x, StringComparer.Ordinal))
                if (!used.Contains(marker))
                    result.Warnings.Add($"Маркер {marker} есть в очереди проверки, но не встречается в конфигах (возможно, он только в коде/документах).");
        }

        private static void DetectDataGaps(NormativeConfigSet set, ConfigValidationResult result)
        {
            var profile = set.MainProfile;
            // Несостыковка 1: п. 3.13 ссылается на Д.2–Д.8, но есть Д.9.
            foreach (var table in profile.Tables)
                if (!profile.ClauseReferencedTables.Contains(table.Id))
                {
                    result.Warnings.Add($"GAP-001: таблица {table.Id} есть в прил. Д, но не упомянута в п. 3.13 (список Д.2–Д.8).");
                    result.Gaps.Add(Finding.Warning("GAP-001",
                        $"Таблица {table.Id} не упомянута в перечне п. 3.13.", table.Id, profile.NormRef,
                        profile.Verification, "GOST-014"));
                }

            // Несостыковка 2: кайма для 7.21 и 8.27 не находится.
            CheckBorderCoverage(set, SignNumber.Parse("7.21"), "GAP-002A", result);
            CheckBorderCoverage(set, SignNumber.Parse("8.27"), "GAP-002B", result);

            // Несостыковка 3: скорость указана для 6.2, а 6.2.1 не упомянут.
            var n621 = SignNumber.Parse("6.2.1");
            if (set.Parameters.Match(n621).Count == 0)
            {
                result.Warnings.Add("GAP-003: для знака 6.2.1 нет правила параметра (скорость указана только для 6.2).");
                result.Gaps.Add(Finding.Warning("GAP-003",
                    "В табл. Д.1 скорость указана для 6.2, но 6.2.1 не упомянут.", "6.2.1",
                    set.Parameters.NormRef, set.Parameters.Verification, "GOST-009"));
            }

            // Несостыковка 4: 6.14.1 есть в Д.5 и Д.8 и он же индивидуального проектирования.
            var n6141 = SignNumber.Parse("6.14.1");
            var places = SizeResolver.FindRowLocations(profile, n6141);
            if (places.Count > 1 || set.Variants.IndividualDesign.Contains(n6141))
            {
                result.Warnings.Add("GAP-004: знак 6.14.1 встречается в нескольких таблицах (" +
                    string.Join(", ", places) + ") и одновременно отнесён к индивидуальному проектированию.");
                result.Gaps.Add(Finding.Warning("GAP-004",
                    "Знак 6.14.1 есть в Д.5 и Д.8 и является знаком индивидуального проектирования.",
                    "6.14.1", profile.NormRef, profile.Verification, "GOST-010"));
            }

            // Пробел: ширина внутренней каймы не извлечена из текста стандарта.
            if (!set.Borders.InnerBorderWidthMm.HasValue)
                result.Gaps.Add(Finding.Warning("GAP-INNER-BORDER",
                    set.Borders.InnerBorderWidthNote, "config/norms/borders.json",
                    set.Borders.NormRef, set.Borders.Verification, "GOST-001"));
        }

        private static void CheckBorderCoverage(NormativeConfigSet set, SignNumber number, string id, ConfigValidationResult result)
        {
            var outer = set.Borders.MatchOuter(number);
            var plate = set.Borders.MatchPlate(number);
            if (outer.Count == 0 && plate.Count == 0)
            {
                result.Warnings.Add($"{id}: для знака {number} не найдено правило каймы (п. 3.14 указывает 7.1–7.20 и 8.1.1–8.26).");
                result.Gaps.Add(Finding.Warning(id,
                    $"Правило каймы для {number} не находится.", number.ToString(),
                    set.Borders.NormRef, set.Borders.Verification, "GOST-008"));
            }
        }
    }
}
