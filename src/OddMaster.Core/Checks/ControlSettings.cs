using System.Collections.Generic;
using System.Linq;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Checks
{
    /// <summary>Настройка одного правила контроля из config/rules/controls.json.</summary>
    public sealed class ControlRule
    {
        /// <summary>Создаёт правило.</summary>
        public ControlRule(string id, bool enabled, FindingSeverity severity, string title,
            Dictionary<string, double> parameters, Verification verification, NormRef normRef)
        {
            Id = id; Enabled = enabled; Severity = severity; Title = title;
            Parameters = parameters; Verification = verification; NormRef = normRef;
        }

        /// <summary>Идентификатор R001…R007.</summary>
        public string Id { get; }
        /// <summary>Включено ли правило.</summary>
        public bool Enabled { get; }
        /// <summary>Важность находки.</summary>
        public FindingSeverity Severity { get; }
        /// <summary>Название на русском.</summary>
        public string Title { get; }
        /// <summary>Числовые параметры (допуск, коридор).</summary>
        public IReadOnlyDictionary<string, double> Parameters { get; }
        /// <summary>Статус проверки правила.</summary>
        public Verification Verification { get; }
        /// <summary>Ссылка на норму.</summary>
        public NormRef NormRef { get; }

        /// <summary>Читает числовой параметр.</summary>
        public double Param(string name, double fallback) =>
            Parameters.TryGetValue(name, out var v) ? v : fallback;
    }

    /// <summary>Правило парности/зоны: 3.24 → 3.25 / 3.24 / табличка 8.2.1 (статус Unverified).</summary>
    public sealed class PairingRule
    {
        /// <summary>Создаёт правило парности.</summary>
        public PairingRule(string start, string end, string repeat, List<string> plates,
            Verification verification, string? marker, NormRef normRef)
        {
            Start = start; End = end; Repeat = repeat; Plates = plates;
            Verification = verification; Marker = marker; NormRef = normRef;
        }

        /// <summary>Номер знака начала зоны.</summary>
        public string Start { get; }
        /// <summary>Номер знака конца зоны.</summary>
        public string End { get; }
        /// <summary>Номер знака повтора.</summary>
        public string Repeat { get; }
        /// <summary>Таблички, участвующие в правиле.</summary>
        public IReadOnlyList<string> Plates { get; }
        /// <summary>Статус проверки.</summary>
        public Verification Verification { get; }
        /// <summary>Маркер GOST-NNN.</summary>
        public string? Marker { get; }
        /// <summary>Ссылка на норму.</summary>
        public NormRef NormRef { get; }
    }

    /// <summary>Все настройки контроля.</summary>
    public sealed class ControlSettings
    {
        /// <summary>Создаёт настройки.</summary>
        public ControlSettings(NormRef normRef, Verification verification, List<ControlRule> rules,
            List<PairingRule> pairing, List<string> markers)
        {
            NormRef = normRef; Verification = verification; Rules = rules; Pairing = pairing; Markers = markers;
        }

        /// <summary>Ссылка на норму.</summary>
        public NormRef NormRef { get; }
        /// <summary>Статус проверки.</summary>
        public Verification Verification { get; }
        /// <summary>Правила R001–R007.</summary>
        public IReadOnlyList<ControlRule> Rules { get; }
        /// <summary>Правила парности.</summary>
        public IReadOnlyList<PairingRule> Pairing { get; }
        /// <summary>Маркеры GOST-NNN.</summary>
        public IReadOnlyList<string> Markers { get; }

        /// <summary>Ищет правило по идентификатору.</summary>
        public ControlRule? Find(string id) => Rules.FirstOrDefault(r => r.Id == id);
    }
}
