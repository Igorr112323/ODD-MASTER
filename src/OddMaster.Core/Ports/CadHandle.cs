using System;
using System.Globalization;

namespace OddMaster.Core.Ports
{
    /// <summary>
    /// Непрозрачный дескриптор объекта чертежа (LINUX_FIRST_PROGRAM §2, правило 1).
    /// Ядро не знает, что внутри: число, указатель или строка САПР. Сравнение — по значению,
    /// формат в тексте — инвариантный, чтобы снимки не зависели от культуры.
    /// </summary>
    public readonly struct CadHandle : IEquatable<CadHandle>
    {
        /// <summary>Дескриптор объекта.</summary>
        public CadHandle(int value)
        {
            Value = value;
        }

        /// <summary>Значение, выданное хостом. Для ядра — просто метка.</summary>
        public int Value { get; }

        /// <summary>Пустой (неинициализированный) дескриптор.</summary>
        public bool IsEmpty => Value == 0;

        /// <inheritdoc />
        public bool Equals(CadHandle other) => Value == other.Value;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is CadHandle other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => Value;

        /// <inheritdoc />
        public override string ToString() => "h" + Value.ToString("D4", CultureInfo.InvariantCulture);

        /// <summary>Сравнение дескрипторов.</summary>
        public static bool operator ==(CadHandle left, CadHandle right) => left.Equals(right);

        /// <summary>Сравнение дескрипторов.</summary>
        public static bool operator !=(CadHandle left, CadHandle right) => !left.Equals(right);
    }
}
