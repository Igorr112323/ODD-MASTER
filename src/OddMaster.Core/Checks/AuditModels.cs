using System;
using System.Collections.Generic;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Checks
{
    /// <summary>Запись о знаке на проверяемой схеме (из CSV-фикстуры или адаптера САПР).</summary>
    public sealed class SchemeEntry
    {
        /// <summary>Создаёт запись.</summary>
        public SchemeEntry(string id, SignNumber number, string sizeClass, string? parameterKind,
            decimal? parameterValue, double stationMeters, double offsetMeters, string side)
        {
            Id = id; Number = number; SizeClass = sizeClass; ParameterKind = parameterKind;
            ParameterValue = parameterValue; StationMeters = stationMeters;
            OffsetMeters = offsetMeters; Side = side;
        }

        /// <summary>Идентификатор записи.</summary>
        public string Id { get; }
        /// <summary>Номер знака.</summary>
        public SignNumber Number { get; }
        /// <summary>Типоразмер.</summary>
        public string SizeClass { get; }
        /// <summary>Имя параметра.</summary>
        public string? ParameterKind { get; }
        /// <summary>Значение параметра.</summary>
        public decimal? ParameterValue { get; }
        /// <summary>Пикет, м.</summary>
        public double StationMeters { get; }
        /// <summary>Смещение, м.</summary>
        public double OffsetMeters { get; }
        /// <summary>Сторона: справа/слева.</summary>
        public string Side { get; }

        /// <summary>Местоположение в отчёте: пикет + сторона + смещение.</summary>
        public string Location => "ПК" + StationMeters.ToString("0.##",
            System.Globalization.CultureInfo.InvariantCulture) + " / " + Side + " / " +
            OffsetMeters.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " м / " + Id;
    }

    /// <summary>Вход аудитора: имя схемы и записи о знаках.</summary>
    public sealed class AuditRequest
    {
        /// <summary>Создаёт запрос.</summary>
        public AuditRequest(string name, IEnumerable<SchemeEntry> entries)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Entries = entries == null ? new List<SchemeEntry>() : new List<SchemeEntry>(entries);
        }

        /// <summary>Название схемы.</summary>
        public string Name { get; }
        /// <summary>Записи о знаках.</summary>
        public IReadOnlyList<SchemeEntry> Entries { get; }
    }

    /// <summary>Находка аудитора: правило, важность, место, сообщение, норма, статус.</summary>
    public sealed class AuditFinding
    {
        /// <summary>Создаёт находку.</summary>
        public AuditFinding(string ruleId, FindingSeverity severity, double stationMeters,
            string location, string message, NormRef? normRef, Verification verification,
            string? marker = null)
        {
            RuleId = ruleId; Severity = severity; StationMeters = stationMeters;
            Location = location; Message = message; NormRef = normRef;
            Verification = verification; Marker = marker;
        }

        /// <summary>Идентификатор правила R001…R007.</summary>
        public string RuleId { get; }
        /// <summary>Важность.</summary>
        public FindingSeverity Severity { get; }
        /// <summary>Пикет для сортировки.</summary>
        public double StationMeters { get; }
        /// <summary>Место: пикет / сторона / смещение / запись.</summary>
        public string Location { get; }
        /// <summary>Сообщение на русском.</summary>
        public string Message { get; }
        /// <summary>Ссылка на норму.</summary>
        public NormRef? NormRef { get; }
        /// <summary>Статус проверки правила.</summary>
        public Verification Verification { get; }
        /// <summary>Маркер GOST-NNN.</summary>
        public string? Marker { get; }
        /// <summary>
        /// Истина только для Placeholder/Unverified: CuratorExtracted — проверенная выписка,
        /// пометка «не проверено по ГОСТ» к нему не относится (CR-12).
        /// </summary>
        public bool IsDraft => Verification == Verification.Placeholder || Verification == Verification.Unverified;

        /// <summary>Строка отчёта с пометкой «не проверено по ГОСТ» при необходимости.</summary>
        public string Format()
        {
            var prefix = IsDraft ? "[не проверено по ГОСТ] " : "";
            return prefix + RuleId + " / " + Severity + " / " + Location + " / " + Message
                + (NormRef != null ? " / " + NormRef : "") + (Marker != null ? " / " + Marker : "");
        }
    }
}
