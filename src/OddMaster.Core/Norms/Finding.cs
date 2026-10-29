using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OddMaster.Core.Norms
{
    /// <summary>Важность находки.</summary>
    public enum FindingSeverity
    {
        /// <summary>Информация.</summary>
        Info = 0,
        /// <summary>Предупреждение: требует внимания, но не блокирует.</summary>
        Warning = 1,
        /// <summary>Ошибка: нормативное нарушение.</summary>
        Error = 2
    }

    /// <summary>
    /// Находка: результат проверки или генерации. Нормативные проблемы возвращаются находками,
    /// а не исключениями (исключения — только для ошибок программиста).
    /// </summary>
    public sealed class Finding
    {
        /// <summary>Создаёт находку.</summary>
        public Finding(
            string ruleId,
            FindingSeverity severity,
            string message,
            string? location = null,
            NormRef? normRef = null,
            Verification verification = Verification.CuratorExtracted,
            string? marker = null)
        {
            RuleId = ruleId ?? throw new ArgumentNullException(nameof(ruleId));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Severity = severity;
            Location = location;
            NormRef = normRef;
            Verification = verification;
            Marker = marker;
        }

        /// <summary>Идентификатор правила или проверки, например R006 или G-002.</summary>
        public string RuleId { get; }

        /// <summary>Важность.</summary>
        public FindingSeverity Severity { get; }

        /// <summary>Сообщение пользователю на русском.</summary>
        public string Message { get; }

        /// <summary>Место: опора, пикет, файл.</summary>
        public string? Location { get; }

        /// <summary>Ссылка на норму, если есть.</summary>
        public NormRef? NormRef { get; }

        /// <summary>Статус проверки данных, породивших находку.</summary>
        public Verification Verification { get; }

        /// <summary>Идентификатор GOST-NNN, если находка связана с пробелом в данных.</summary>
        public string? Marker { get; }

        /// <summary>Создаёт находку уровня Error.</summary>
        public static Finding Error(string ruleId, string message, string? location = null,
            NormRef? normRef = null, Verification verification = Verification.CuratorExtracted, string? marker = null) =>
            new Finding(ruleId, FindingSeverity.Error, message, location, normRef, verification, marker);

        /// <summary>Создаёт находку уровня Warning.</summary>
        public static Finding Warning(string ruleId, string message, string? location = null,
            NormRef? normRef = null, Verification verification = Verification.CuratorExtracted, string? marker = null) =>
            new Finding(ruleId, FindingSeverity.Warning, message, location, normRef, verification, marker);

        /// <summary>Создаёт находку уровня Info.</summary>
        public static Finding Info(string ruleId, string message, string? location = null,
            NormRef? normRef = null, Verification verification = Verification.CuratorExtracted, string? marker = null) =>
            new Finding(ruleId, FindingSeverity.Info, message, location, normRef, verification, marker);

        /// <summary>Текст находки для отчётов: «[не проверено по ГОСТ]» для непроверенных правил.</summary>
        public string Format()
        {
            var sb = new StringBuilder();
            if (Verification == Verification.Unverified || Verification == Verification.Placeholder)
                sb.Append("[не проверено по ГОСТ] ");
            sb.Append(RuleId).Append(": ").Append(Message);
            if (Location != null) sb.Append(" (").Append(Location).Append(')');
            if (NormRef != null) sb.Append(" [").Append(NormRef.Describe()).Append(']');
            if (Marker != null) sb.Append(" {").Append(Marker).Append('}');
            return sb.ToString();
        }
    }

    /// <summary>Детерминированный отчёт о статусах проверки данных, использованных при генерации.</summary>
    public sealed class VerificationSummary
    {
        private readonly SortedDictionary<string, Verification> _entries =
            new SortedDictionary<string, Verification>(StringComparer.Ordinal);

        /// <summary>Добавляет источник данных и его статус. Худший статус побеждает.</summary>
        public void Add(string source, Verification status)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!_entries.TryGetValue(source, out var old) || status < old)
                _entries[source] = status;
        }

        /// <summary>Истина, если использованы заглушки или непроверенные данные.</summary>
        public bool IsDraft => _entries.Values.Any(v => v <= Verification.Unverified);

        /// <summary>Истина, если хоть один источник не сверен студентом.</summary>
        public bool HasUnverified => _entries.Values.Any(v => v != Verification.StudentVerified);

        /// <summary>Все пары «источник = статус» в устойчивом порядке.</summary>
        public string Describe() => string.Join("; ", _entries.Select(e => e.Key + "=" + e.Value));

        /// <summary>Количество источников.</summary>
        public int Count => _entries.Count;

        /// <summary>Возвращает пары в устойчивом порядке для отчётов.</summary>
        public IReadOnlyList<KeyValuePair<string, Verification>> Entries() => _entries.ToList();
    }
}
