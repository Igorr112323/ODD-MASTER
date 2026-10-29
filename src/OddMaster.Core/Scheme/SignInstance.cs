using System;
using System.Globalization;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Scheme
{
    /// <summary>Направление знака относительно хода пикетажа.</summary>
    public enum SignDirection
    {
        /// <summary>По ходу пикетажа.</summary>
        Along,
        /// <summary>Против хода пикетажа.</summary>
        Against
    }

    /// <summary>Знак на схеме (SignInstance): номер, типоразмер, параметр, направление.</summary>
    public sealed class SignInstance
    {
        /// <summary>Создаёт знак на схеме.</summary>
        public SignInstance(string id, SignNumber number, string sizeClass,
            string? parameterKind = null, decimal? parameterValue = null,
            SignDirection direction = SignDirection.Along)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Number = number ?? throw new ArgumentNullException(nameof(number));
            SizeClass = sizeClass ?? throw new ArgumentNullException(nameof(sizeClass));
            ParameterKind = parameterKind;
            ParameterValue = parameterValue;
            Direction = direction;
        }

        /// <summary>Идентификатор экземпляра.</summary>
        public string Id { get; }
        /// <summary>Номер знака.</summary>
        public SignNumber Number { get; }
        /// <summary>Типоразмер I–IV.</summary>
        public string SizeClass { get; }
        /// <summary>Имя параметра (speed, distance).</summary>
        public string? ParameterKind { get; }
        /// <summary>Значение параметра.</summary>
        public decimal? ParameterValue { get; }
        /// <summary>Направление.</summary>
        public SignDirection Direction { get; }
        /// <summary>Табличка группы 8.</summary>
        public bool IsPlate => Number.Group == 8;

        /// <summary>Значение параметра в канонической строке (InvariantCulture).</summary>
        public string ParameterDisplay => ParameterValue.HasValue
            ? ParameterValue.Value.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty;

        /// <summary>Ключ для группировки ведомости: номер + типоразмер + параметр (ADR-0011).</summary>
        public string StatementKey => Number + "|" + SizeClass + "|" + ParameterDisplay;

        /// <summary>Краткое представление для журналов.</summary>
        public string Display => Number + (ParameterDisplay.Length > 0 ? " (" + ParameterDisplay + ")" : "");
    }
}
