using System;
using System.Collections.Generic;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Catalog
{
    /// <summary>Форма носителя знака (глоссарий 7.3).</summary>
    public enum CarrierShape
    {
        /// <summary>Треугольник вершиной вверх.</summary>
        TriangleUp,
        /// <summary>Треугольник вершиной вниз.</summary>
        TriangleDown,
        /// <summary>Круг.</summary>
        Circle,
        /// <summary>Квадрат.</summary>
        Square,
        /// <summary>Ромб (квадрат под 45°).</summary>
        Diamond,
        /// <summary>Правильный восьмиугольник.</summary>
        Octagon,
        /// <summary>Вертикальный прямоугольник.</summary>
        RectangleVertical,
        /// <summary>Горизонтальный прямоугольник.</summary>
        RectangleHorizontal
    }

    /// <summary>Разбор формы носителя из каталога.</summary>
    public static class CarrierShapes
    {
        /// <summary>Разбирает строковое имя формы.</summary>
        public static CarrierShape Parse(string value)
        {
            if (Enum.TryParse<CarrierShape>(value, ignoreCase: false, out var shape)) return shape;
            throw new FormatException("Неизвестная форма носителя «" + value + "».");
        }
    }

    /// <summary>Слои изображения знака.</summary>
    public enum SignLayerKind
    {
        /// <summary>Контур носителя.</summary>
        Outline,
        /// <summary>Наружная кайма.</summary>
        OuterBorder,
        /// <summary>Внутренняя кайма.</summary>
        InnerBorder,
        /// <summary>Поле знака.</summary>
        Field,
        /// <summary>Пиктограмма.</summary>
        Pictogram,
        /// <summary>Надпись.</summary>
        Text
    }

    /// <summary>Описание пиктограммы-заглушки. Реальная геометрия — за студентом (GOST-019).</summary>
    public sealed class SignPictogram
    {
        /// <summary>Создаёт описание пиктограммы.</summary>
        public SignPictogram(string id, bool placeholder, string? marker, Verification verification,
            string? placeholderShape = null)
        {
            Id = id;
            Placeholder = placeholder;
            Marker = marker;
            Verification = verification;
            PlaceholderShape = placeholderShape;
        }

        /// <summary>Идентификатор пиктограммы.</summary>
        public string Id { get; }

        /// <summary>Истина, если это заглушка, а не геометрия из ГОСТ.</summary>
        public bool Placeholder { get; }

        /// <summary>Маркер GOST-NNN.</summary>
        public string? Marker { get; }

        /// <summary>Статус проверки.</summary>
        public Verification Verification { get; }
        /// <summary>Форма заглушки из конфига: circle / diagonalBand (не нормативка).</summary>
        public string? PlaceholderShape { get; }
    }

    /// <summary>Описание надписи на знаке.</summary>
    public sealed class SignTextSpec
    {
        /// <summary>Создаёт описание надписи.</summary>
        public SignTextSpec(string kind, string value, Verification verification, string? marker)
        {
            Kind = kind;
            Value = value;
            Verification = verification;
            Marker = marker;
        }

        /// <summary>Fixed — константа (STOP, P); Parameter — значение параметра (speed, distance).</summary>
        public string Kind { get; }

        /// <summary>Текст или имя параметра.</summary>
        public string Value { get; }

        /// <summary>Статус проверки.</summary>
        public Verification Verification { get; }

        /// <summary>Маркер GOST-NNN.</summary>
        public string? Marker { get; }
    }

    /// <summary>Описание знака в каталоге (SignDefinition). Геометрия не хранится — она генерируется.</summary>
    public sealed class SignDefinition
    {
        /// <summary>Создаёт запись каталога.</summary>
        public SignDefinition(SignNumber number, string name, int group, CarrierShape shape, string sizeTableId,
            bool isPlate, Dictionary<string, string> colors, SignPictogram? pictogram, SignTextSpec? text,
            string? parameterKind, string? note, NormRef normRef, Verification verification, List<string> markers)
        {
            Number = number ?? throw new ArgumentNullException(nameof(number));
            Name = name;
            Group = group;
            Shape = shape;
            SizeTableId = sizeTableId;
            IsPlate = isPlate;
            Colors = colors ?? throw new ArgumentNullException(nameof(colors));
            Pictogram = pictogram;
            Text = text;
            ParameterKind = parameterKind;
            Note = note;
            NormRef = normRef ?? throw new ArgumentNullException(nameof(normRef));
            Verification = verification;
            Markers = markers ?? throw new ArgumentNullException(nameof(markers));
        }

        /// <summary>Номер знака.</summary>
        public SignNumber Number { get; }

        /// <summary>Наименование по ГОСТ.</summary>
        public string Name { get; }

        /// <summary>Группа 1–8.</summary>
        public int Group { get; }

        /// <summary>Форма носителя.</summary>
        public CarrierShape Shape { get; }

        /// <summary>Ожидаемая таблица размеров (для самопроверки данных).</summary>
        public string SizeTableId { get; }

        /// <summary>Истина для табличек группы 8.</summary>
        public bool IsPlate { get; }

        /// <summary>Семантические имена цветов: field, outerBorder, pictogram, text.</summary>
        public IReadOnlyDictionary<string, string> Colors { get; }

        /// <summary>Пиктограмма-заглушка, если есть.</summary>
        public SignPictogram? Pictogram { get; }

        /// <summary>Надпись, если есть.</summary>
        public SignTextSpec? Text { get; }

        /// <summary>Имя параметра из табл. Д.1 (speed, distance, …).</summary>
        public string? ParameterKind { get; }

        /// <summary>Примечание куратора.</summary>
        public string? Note { get; }

        /// <summary>Ссылка на норму.</summary>
        public NormRef NormRef { get; }

        /// <summary>Статус проверки записи.</summary>
        public Verification Verification { get; }

        /// <summary>Связанные маркеры GOST-NNN.</summary>
        public IReadOnlyList<string> Markers { get; }
    }
}
