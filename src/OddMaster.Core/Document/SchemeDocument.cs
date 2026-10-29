using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OddMaster.Core.Scheme;

namespace OddMaster.Core.Document
{
    /// <summary>DTO документа схемы: будущая начинка пользовательских объектов DWG (P3). Механизм хранения McCustomBase — [ПРОВЕРИТЬ В SDK].</summary>
    public sealed class SchemeDocument
    {
        /// <summary>Версия схемы данных — миграция при смене формата.</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>Создаёт документ.</summary>
        public SchemeDocument() { }

        /// <summary>Идентификатор документа (устойчивый).</summary>
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>Идентификатор.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        /// <summary>Название.</summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        /// <summary>Оси.</summary>
        [JsonPropertyName("axes")]
        public List<DocumentAxis> Axes { get; set; } = new List<DocumentAxis>();

        /// <summary>Стойки.</summary>
        [JsonPropertyName("posts")]
        public List<DocumentPost> Posts { get; set; } = new List<DocumentPost>();

        /// <summary>Зоны действия.</summary>
        [JsonPropertyName("zones")]
        public List<DocumentZone> Zones { get; set; } = new List<DocumentZone>();

        /// <summary>Строит документ из модели ядра.</summary>
        public static SchemeDocument FromScheme(SchemeModel scheme, string title)
        {
            if (scheme == null) throw new ArgumentNullException(nameof(scheme));
            var doc = new SchemeDocument { Id = scheme.Id, Title = title };
            doc.Axes.Add(new DocumentAxis
            {
                Id = scheme.Id + "-axis-1",
                InitialStationMeters = scheme.Alignment.InitialStationMeters,
                Vertices = scheme.Alignment.Vertices
                    .Select(v => new[] { v.X, v.Y }).ToList()
            });
            foreach (var post in scheme.Posts)
            {
                var dp = new DocumentPost
                {
                    Id = post.Id, StationMeters = post.StationMeters,
                    OffsetMeters = post.OffsetMeters, Side = post.Side
                };
                foreach (var sign in post.Mounts)
                    dp.Signs.Add(new DocumentSign
                    {
                        Id = sign.Id, Number = sign.Number.ToString(),
                        SizeClass = sign.SizeClass, ParameterKind = sign.ParameterKind,
                        ParameterValue = sign.ParameterValue.HasValue
                            ? (double?)decimal.ToDouble(sign.ParameterValue.Value) : null
                    });
                doc.Posts.Add(dp);
            }
            return doc;
        }

        /// <summary>Детерминированный JSON (UTF-8, без времени и случайности).</summary>
        public string ToJson() => JsonSerializer.Serialize(this,
            new JsonSerializerOptions { WriteIndented = true }) + "\n";

        /// <summary>Разбирает документ. Несовместимая version — FormatException.</summary>
        public static SchemeDocument FromJson(string json)
        {
            var doc = JsonSerializer.Deserialize<SchemeDocument>(json ?? "{}");
            if (doc == null) throw new FormatException("Пустой документ схемы.");
            if (doc.SchemaVersion != CurrentSchemaVersion)
                throw new FormatException("Неизвестная schemaVersion: " +
                    doc.SchemaVersion.ToString(CultureInfo.InvariantCulture));
            return doc;
        }
    }

    /// <summary>Ось документа.</summary>
    public sealed class DocumentAxis
    {
        /// <summary>Идентификатор.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        /// <summary>Начальный пикет, м.</summary>
        [JsonPropertyName("initialStationMeters")]
        public double InitialStationMeters { get; set; }
        /// <summary>Вершины полилинии [[x,y],…].</summary>
        [JsonPropertyName("vertices")]
        public List<double[]> Vertices { get; set; } = new List<double[]>();
    }

    /// <summary>Стойка документа.</summary>
    public sealed class DocumentPost
    {
        /// <summary>Идентификатор.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        /// <summary>Пикет, м.</summary>
        [JsonPropertyName("stationMeters")]
        public double StationMeters { get; set; }
        /// <summary>Смещение, м.</summary>
        [JsonPropertyName("offsetMeters")]
        public double OffsetMeters { get; set; }
        /// <summary>Сторона.</summary>
        [JsonPropertyName("side")]
        public string Side { get; set; } = "";
        /// <summary>Знаки сверху вниз.</summary>
        [JsonPropertyName("signs")]
        public List<DocumentSign> Signs { get; set; } = new List<DocumentSign>();
    }

    /// <summary>Знак на стойке.</summary>
    public sealed class DocumentSign
    {
        /// <summary>Идентификатор.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        /// <summary>Номер.</summary>
        [JsonPropertyName("number")]
        public string Number { get; set; } = "";
        /// <summary>Типоразмер.</summary>
        [JsonPropertyName("sizeClass")]
        public string SizeClass { get; set; } = "";
        /// <summary>Имя параметра.</summary>
        [JsonPropertyName("parameterKind")]
        public string? ParameterKind { get; set; }
        /// <summary>Значение параметра.</summary>
        [JsonPropertyName("parameterValue")]
        public double? ParameterValue { get; set; }
    }

    /// <summary>Зона действия: начало, конец, протяжённость, таблички.</summary>
    public sealed class DocumentZone
    {
        /// <summary>Идентификатор.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        /// <summary>Знак начала.</summary>
        [JsonPropertyName("startSign")]
        public string StartSign { get; set; } = "";
        /// <summary>Знак конца.</summary>
        [JsonPropertyName("endSign")]
        public string EndSign { get; set; } = "";
        /// <summary>Пикет начала, м.</summary>
        [JsonPropertyName("startStationMeters")]
        public double StartStationMeters { get; set; }
        /// <summary>Пикет конца, м.</summary>
        [JsonPropertyName("endStationMeters")]
        public double EndStationMeters { get; set; }
        /// <summary>Таблички зоны.</summary>
        [JsonPropertyName("plates")]
        public List<string> Plates { get; set; } = new List<string>();

        /// <summary>Протяжённость, м.</summary>
        [JsonIgnore]
        public double LengthMeters => EndStationMeters - StartStationMeters;
    }
}
