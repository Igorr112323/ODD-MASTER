using System.Collections.Generic;
using OddMaster.Core.Catalog;
using OddMaster.Core.Geometry;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Generation
{
    /// <summary>Слой изображения знака: контур, кайма, поле, пиктограмма, надпись.</summary>
    public sealed class SignLayer
    {
        /// <summary>Создаёт слой.</summary>
        public SignLayer(SignLayerKind kind, string colorName, Path? geometry, bool filled, double strokeWidthMm)
        {
            Kind = kind; ColorName = colorName; Geometry = geometry;
            Filled = filled; StrokeWidthMm = strokeWidthMm;
        }

        /// <summary>Назначение слоя.</summary>
        public SignLayerKind Kind { get; }
        /// <summary>Семантическое имя цвета (RGB задаёт рендер).</summary>
        public string ColorName { get; }
        /// <summary>Контур слоя.</summary>
        public Path? Geometry { get; }
        /// <summary>Заливать контур (true) или только обводить (false).</summary>
        public bool Filled { get; }
        /// <summary>Толщина обводки, мм.</summary>
        public double StrokeWidthMm { get; }
    }

    /// <summary>Надпись на знаке: текст, положение, высота (пока Placeholder).</summary>
    public sealed class SignText
    {
        /// <summary>Создаёт надпись.</summary>
        public SignText(string value, Point2 anchor, double heightMm, string colorName, string fontFamily, Verification verification)
        {
            Value = value; Anchor = anchor; HeightMm = heightMm;
            ColorName = colorName; FontFamily = fontFamily; Verification = verification;
        }

        /// <summary>Текст (STOP, P, 40, «50 м»).</summary>
        public string Value { get; }
        /// <summary>Точка привязки (центр).</summary>
        public Point2 Anchor { get; }
        /// <summary>Высота шрифта, мм.</summary>
        public double HeightMm { get; }
        /// <summary>Семантическое имя цвета.</summary>
        public string ColorName { get; }
        /// <summary>Семейство шрифтов с запасом (Arial Bold по п. 3.4).</summary>
        public string FontFamily { get; }
        /// <summary>Статус проверки высоты/начертания.</summary>
        public Verification Verification { get; }
    }

    /// <summary>Результат параметрической генерации знака. Геометрия не зависит от nanoCAD.</summary>
    public sealed class SignGeometry
    {
        /// <summary>Создаёт геометрию знака.</summary>
        public SignGeometry(SignDefinition sign, string sizeClass, string profileId, string sizeTableId,
            BoundingBox bounds, Path outline, List<SignLayer> layers, List<SignText> texts,
            double outerBorderWidthMm, double innerBorderWidthMm, bool innerBorderIsPlaceholder,
            string? parameterValue, VerificationSummary verification, List<Finding> findings)
        {
            Sign = sign; SizeClass = sizeClass; ProfileId = profileId; SizeTableId = sizeTableId;
            Bounds = bounds; Outline = outline; Layers = layers; Texts = texts;
            OuterBorderWidthMm = outerBorderWidthMm; InnerBorderWidthMm = innerBorderWidthMm;
            InnerBorderIsPlaceholder = innerBorderIsPlaceholder; ParameterValue = parameterValue;
            Verification = verification; Findings = findings;
        }

        /// <summary>Описание знака из каталога.</summary>
        public SignDefinition Sign { get; }
        /// <summary>Типоразмер I–IV.</summary>
        public string SizeClass { get; }
        /// <summary>Профиль типоразмеров.</summary>
        public string ProfileId { get; }
        /// <summary>Таблица прил. Д, из которой взят размер.</summary>
        public string SizeTableId { get; }
        /// <summary>Номинальный габарит, мм (центр в 0,0).</summary>
        public BoundingBox Bounds { get; }
        /// <summary>Ширина габарита, мм.</summary>
        public double WidthMm => Bounds.Width;
        /// <summary>Высота габарита, мм.</summary>
        public double HeightMm => Bounds.Height;
        /// <summary>Наружный контур.</summary>
        public Path Outline { get; }
        /// <summary>Слои в порядке отрисовки.</summary>
        public IReadOnlyList<SignLayer> Layers { get; }
        /// <summary>Надписи.</summary>
        public IReadOnlyList<SignText> Texts { get; }
        /// <summary>Ширина наружной каймы из borders.json.</summary>
        public double OuterBorderWidthMm { get; }
        /// <summary>Ширина внутренней каймы (возможно, Placeholder).</summary>
        public double InnerBorderWidthMm { get; }
        /// <summary>Истина, если ширина внутренней каймы — заглушка.</summary>
        public bool InnerBorderIsPlaceholder { get; }
        /// <summary>Каноническое значение параметра (для ведомости).</summary>
        public string? ParameterValue { get; }
        /// <summary>Сводка статусов проверки.</summary>
        public VerificationSummary Verification { get; }
        /// <summary>Находки, возникшие при генерации.</summary>
        public IReadOnlyList<Finding> Findings { get; }
        /// <summary>Черновик: есть заглушки или непроверенные данные.</summary>
        public bool IsDraft => Verification.IsDraft;
    }

    /// <summary>Результат вызова генератора: геометрия (или null) + находки + сводка проверки.</summary>
    public sealed class GenerationResult
    {
        /// <summary>Создаёт результат.</summary>
        public GenerationResult(SignGeometry? geometry, List<Finding> findings, VerificationSummary verification)
        {
            Geometry = geometry; Findings = findings; Verification = verification;
        }

        /// <summary>Геометрия знака, если генерация возможна.</summary>
        public SignGeometry? Geometry { get; }
        /// <summary>Находки.</summary>
        public IReadOnlyList<Finding> Findings { get; }
        /// <summary>Сводка статусов проверки.</summary>
        public VerificationSummary Verification { get; }
        /// <summary>Истина, если геометрия построена.</summary>
        public bool IsSuccess => Geometry != null;
        /// <summary>Черновик.</summary>
        public bool IsDraft => Verification.IsDraft;
    }
}
