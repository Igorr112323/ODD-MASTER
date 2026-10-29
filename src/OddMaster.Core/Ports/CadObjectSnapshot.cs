using System;
using System.Collections.Generic;

namespace OddMaster.Core.Ports
{
    /// <summary>Точка, введённая пользователем. Единицы — миллиметры чертежа.</summary>
    public readonly struct CadPoint
    {
        /// <summary>Создаёт точку.</summary>
        public CadPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Координата X, мм.</summary>
        public double X { get; }

        /// <summary>Координата Y, мм.</summary>
        public double Y { get; }

        /// <inheritdoc />
        public override string ToString() =>
            "(" + X.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "; " +
            Y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ")";
    }

    /// <summary>
    /// Свойство объекта: пара «имя — значение» в текстовом виде. Текстовый вид выбран специально:
    /// так данные одинаково переживают сохранение в DWG, JSON и DXF, а сравнение снимков не зависит
    /// от плавающей точки (правило 9).
    /// </summary>
    public sealed class CadProperty
    {
        /// <summary>Создаёт свойство.</summary>
        public CadProperty(string name, string value)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>Имя свойства.</summary>
        public string Name { get; }

        /// <summary>Значение свойства.</summary>
        public string Value { get; }
    }

    /// <summary>Снимок объекта: тип, слой и свойства. Только чтение — менять можно лишь транзакцией.</summary>
    public sealed class CadObjectSnapshot
    {
        /// <summary>Создаёт снимок.</summary>
        public CadObjectSnapshot(CadHandle handle, string type, string layer,
            IReadOnlyList<CadProperty> properties)
        {
            Handle = handle;
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Layer = layer ?? throw new ArgumentNullException(nameof(layer));
            Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        }

        /// <summary>Дескриптор.</summary>
        public CadHandle Handle { get; }

        /// <summary>Тип доменного объекта: Sign, Post, Axis.</summary>
        public string Type { get; }

        /// <summary>Слой чертежа.</summary>
        public string Layer { get; }

        /// <summary>Свойства в порядке, заданном хостом (детерминированно).</summary>
        public IReadOnlyList<CadProperty> Properties { get; }

        /// <summary>Значение свойства или null, если его нет.</summary>
        public string? Get(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            foreach (var property in Properties)
            {
                if (string.Equals(property.Name, name, StringComparison.Ordinal)) return property.Value;
            }
            return null;
        }
    }
}
