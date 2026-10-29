using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using OddMaster.Core.Ports;

namespace OddMaster.Host.Emulator
{
    /// <summary>
    /// Хранение документа в JSON (правило 6 эмулятора). Формат версионирован:
    /// v1 — без слоя, v2 — текущий. Более новая версия открывается только для чтения.
    /// Режим «модуль не загружен» отдаёт кэш графики: тип и слой без свойств.
    /// </summary>
    public sealed class EmulatorPersistence : ICadPersistence
    {
        /// <summary>Текущая версия схемы данных.</summary>
        public const int CurrentSchemaVersion = 2;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            // Чтение разбирает ключи вручную (camelCase): имена должны совпадать с записью.
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private readonly EmulatorDocument _document;

        /// <summary>Создаёт хранилище для документа.</summary>
        public EmulatorPersistence(EmulatorDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <inheritdoc />
        public string Save()
        {
            var dto = new DocumentDto { SchemaVersion = CurrentSchemaVersion };
            foreach (var snapshot in _document.Export())
            {
                var item = new ObjectDto
                {
                    Handle = snapshot.Handle.Value,
                    Type = snapshot.Type,
                    Layer = snapshot.Layer,
                };
                foreach (var property in snapshot.Properties) item.Properties[property.Name] = property.Value;
                dto.Objects.Add(item);
            }
            return JsonSerializer.Serialize(dto, Options);
        }

        /// <inheritdoc />
        public CadLoadResult Load(string json, bool moduleAvailable = true)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var warnings = new List<string>();
            var objects = new List<CadObjectSnapshot>();
            var normalized = json.Replace("\r\n", "\n");

            using (var document = JsonDocument.Parse(normalized))
            {
                var root = document.RootElement;
                var version = root.TryGetProperty("schemaVersion", out var versionElement)
                    ? versionElement.GetInt32()
                    : 1;
                var readOnly = false;

                if (version > CurrentSchemaVersion)
                {
                    readOnly = true;
                    warnings.Add("Документ создан более новой версией хранилища (" + version +
                                 " > " + CurrentSchemaVersion + "): открыт только для чтения.");
                }
                else if (version < CurrentSchemaVersion)
                {
                    warnings.Add("Выполнена миграция данных v" + version + " → v" + CurrentSchemaVersion + ".");
                }

                if (root.TryGetProperty("objects", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        var handle = new CadHandle(item.GetProperty("handle").GetInt32());
                        var type = item.GetProperty("type").GetString() ?? "Unknown";
                        var layer = version >= 2 && item.TryGetProperty("layer", out var layerElement)
                            ? layerElement.GetString() ?? "0"
                            : "0";
                        var properties = new List<CadProperty>();
                        if (moduleAvailable && item.TryGetProperty("properties", out var props) &&
                            props.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var property in props.EnumerateObject())
                                properties.Add(new CadProperty(property.Name, property.Value.GetString() ?? string.Empty));
                        }
                        objects.Add(new CadObjectSnapshot(handle, type, layer, properties));
                    }
                }

                if (!moduleAvailable)
                {
                    readOnly = true;
                    warnings.Add("Модуль не загружен: показан кэш графики (тип и слой без свойств).");
                }

                if (!readOnly) _document.Import(objects);
                return new CadLoadResult(objects, warnings, readOnly);
            }
        }

        /// <inheritdoc />
        public void SaveToFile(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            var full = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(full, Save(), new UTF8Encoding(false));
        }

        /// <inheritdoc />
        public CadLoadResult LoadFromFile(string path, bool moduleAvailable = true)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            var resolved = ResolveCaseInsensitive(path);
            if (resolved == null) throw new FileNotFoundException("Файл не найден: " + path, path);
            return Load(File.ReadAllText(resolved, Encoding.UTF8), moduleAvailable);
        }

        /// <summary>
        /// Ищет файл без учёта регистра (правило 8: Windows и Linux не должны расходиться).
        /// Возвращает null, если файла нет.
        /// </summary>
        public static string? ResolveCaseInsensitive(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            var full = Path.GetFullPath(path);
            if (File.Exists(full)) return full;
            var directory = Path.GetDirectoryName(full);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return null;
            var name = Path.GetFileName(full);
            var match = Directory.GetFiles(directory)
                .Where(candidate => string.Equals(Path.GetFileName(candidate), name,
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(candidate => candidate, StringComparer.Ordinal)
                .FirstOrDefault();
            return match;
        }

        private sealed class DocumentDto
        {
            public int SchemaVersion { get; set; }

            public List<ObjectDto> Objects { get; } = new List<ObjectDto>();
        }

        private sealed class ObjectDto
        {
            public int Handle { get; set; }

            public string Type { get; set; } = string.Empty;

            public string Layer { get; set; } = "0";

            public SortedDictionary<string, string> Properties { get; } =
                new SortedDictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
