using System;
using System.Collections.Generic;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Composition
{
    /// <summary>
    /// Вид примитива состава. Это НЕ пиктограмма: форма задаётся параметрами,
    /// оцифровка по прил. Б не нужна (CR-22).
    /// </summary>
    public enum CompositionKind
    {
        /// <summary>Тонкий контур по наружной кайме (общий для всех знаков).</summary>
        Contour,
        /// <summary>Прямоугольник (кирпич 3.1).</summary>
        Rectangle,
        /// <summary>Косой крест из двух полос (3.27).</summary>
        Cross,
        /// <summary>Одна полоса под углом (зачёркивание).</summary>
        Band,
        /// <summary>Пучок тонких полос под углом (2.2, 3.25, 3.31, 5.22, 5.42).</summary>
        Bundle,
        /// <summary>Треугольник-вставка (5.19.x, 5.20).</summary>
        Triangle,
        /// <summary>Внутренняя панель (5.16, 6.2.1).</summary>
        Panel,
        /// <summary>Вложенный знак (5.41, 5.42).</summary>
        Nested,
        /// <summary>Стрелка-указатель на табличке (8.2.x).</summary>
        Arrow
    }

    /// <summary>Один параметрический элемент состава из config/elements/elements.json.</summary>
    public sealed class CompositionElement
    {
        /// <summary>Создаёт элемент.</summary>
        public CompositionElement(string id, CompositionKind kind, string colorName, bool filled,
            double xRatio, double yRatio, double widthRatio, double heightRatio, double bandRatio,
            double lengthRatio, double spacingRatio, int count, double angleDeg, double strokeRatio,
            double sizeRatio, string? direction, string? note, Verification verification, string? marker,
            string accentColor = "")
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Kind = kind;
            ColorName = colorName ?? string.Empty;
            Filled = filled;
            XRatio = xRatio; YRatio = yRatio;
            WidthRatio = widthRatio; HeightRatio = heightRatio;
            BandRatio = bandRatio; LengthRatio = lengthRatio; SpacingRatio = spacingRatio;
            Count = count; AngleDeg = angleDeg; StrokeRatio = strokeRatio; SizeRatio = sizeRatio;
            Direction = direction; Note = note; Verification = verification; Marker = marker;
            AccentColor = accentColor ?? string.Empty;
        }

        /// <summary>Идентификатор элемента (ключ в конфиге и в каталоге знака).</summary>
        public string Id { get; }
        /// <summary>Вид примитива.</summary>
        public CompositionKind Kind { get; }
        /// <summary>Семантический цвет (из каталога, не RGB).</summary>
        public string ColorName { get; }
        /// <summary>Заливка (true) или только обводка.</summary>
        public bool Filled { get; }
        /// <summary>Смещение центра по X, доля ширины поля.</summary>
        public double XRatio { get; }
        /// <summary>Смещение центра по Y, доля высоты поля.</summary>
        public double YRatio { get; }
        /// <summary>Ширина прямоугольника/треугольника, доля ширины поля.</summary>
        public double WidthRatio { get; }
        /// <summary>Высота прямоугольника/треугольника, доля высоты поля.</summary>
        public double HeightRatio { get; }
        /// <summary>Толщина полосы или линии, доля меньшей стороны поля.</summary>
        public double BandRatio { get; }
        /// <summary>Длина полосы/стрелки относительно меньшей стороны поля.</summary>
        public double LengthRatio { get; }
        /// <summary>Шаг между полосами пучка, доля меньшей стороны поля.</summary>
        public double SpacingRatio { get; }
        /// <summary>Число полос в пучке.</summary>
        public int Count { get; }
        /// <summary>Угол полосы в градусах (0 — вправо, далее против часовой).</summary>
        public double AngleDeg { get; }
        /// <summary>Толщина обводки контура, доля меньшей стороны.</summary>
        public double StrokeRatio { get; }
        /// <summary>Размер вложенного знака/стрелки, доля меньшей стороны.</summary>
        public double SizeRatio { get; }
        /// <summary>Направление стрелки: up / down / left / right.</summary>
        public string? Direction { get; }
        /// <summary>Пояснение к элементу (для отчёта и REVIEW-03).</summary>
        public string? Note { get; }
        /// <summary>Статус проверки размеров элемента (сейчас Unverified).</summary>
        public Verification Verification { get; }
        /// <summary>Маркер GOST-NNN.</summary>
        public string? Marker { get; }

        /// <summary>Второй цвет элемента (например, красный крест во вложенном знаке).</summary>
        public string AccentColor { get; }
    }

    /// <summary>Библиотека примитивов состава, загруженная из config/elements/elements.json.</summary>
    public sealed class CompositionLibrary
    {
        private readonly Dictionary<string, CompositionElement> _byId;

        /// <summary>Создаёт библиотеку.</summary>
        public CompositionLibrary(string id, NormRef normRef, Verification verification, string source,
            string note, List<string> markers, Dictionary<string, CompositionElement> elements)
        {
            Id = id;
            NormRef = normRef;
            Verification = verification;
            Source = source;
            Note = note;
            Markers = markers ?? throw new ArgumentNullException(nameof(markers));
            if (elements == null) throw new ArgumentNullException(nameof(elements));
            Elements = new List<CompositionElement>(elements.Values);
            _byId = new Dictionary<string, CompositionElement>(elements, StringComparer.Ordinal);
        }

        /// <summary>Идентификатор библиотеки.</summary>
        public string Id { get; }
        /// <summary>Ссылка на норму.</summary>
        public NormRef NormRef { get; }
        /// <summary>Статус проверки всех размеров библиотеки.</summary>
        public Verification Verification { get; }
        /// <summary>Откуда взяты доли (замер куратора по прил. А).</summary>
        public string Source { get; }
        /// <summary>Пояснение о границах применимости.</summary>
        public string Note { get; }
        /// <summary>Маркеры GOST-NNN библиотеки.</summary>
        public IReadOnlyList<string> Markers { get; }
        /// <summary>Все элементы в порядке файла.</summary>
        public IReadOnlyList<CompositionElement> Elements { get; }

        /// <summary>Ищет элемент по идентификатору. Отсутствие — null, а не исключение.</summary>
        public CompositionElement? Find(string id) =>
            id != null && _byId.TryGetValue(id, out var element) ? element : null;
    }
}
