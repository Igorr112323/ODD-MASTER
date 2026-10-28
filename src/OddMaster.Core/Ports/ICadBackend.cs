using System.Collections.Generic;
using OddMaster.Core.Geometry;

namespace OddMaster.Core.Ports
{
    /// <summary>
    /// Приёмник чертежа: абстрактные примитивы САПР. Ядро и рендеры не знают, кто за интерфейсом —
    /// DXF-стенд на Linux, nanoCAD или тестовая заглушка. Никаких типов MultiCAD здесь нет.
    /// Порт = замена реализации, ядро не меняется (ADR-0025).
    /// </summary>
    public interface ICadBackend
    {
        /// <summary>Имя реализации для отчётов: «DXF R12», «MultiCAD», «test».</summary>
        string BackendName { get; }

        /// <summary>Сколько сущностей уже добавлено (для отчётов и проверок).</summary>
        int EntityCount { get; }

        /// <summary>Объявляет слой. Повторный вызов с тем же цветом допустим и ничего не делает.</summary>
        void AddLayer(string name, int colorIndex);

        /// <summary>Отрезок. colorIndex = 0 — цвет «по слою».</summary>
        void AddLine(Point2 start, Point2 end, string layer, int colorIndex = 0);

        /// <summary>
        /// Дуга окружности: углы в градусах, Y вверх, положительная развёртка — против часовой.
        /// Приёмник сам приводит знак развёртки к своей системе.
        /// </summary>
        void AddArc(Point2 center, double radius, double startAngleDeg, double sweepDeg, string layer,
            int colorIndex = 0);

        /// <summary>Полилиния по готовым точкам.</summary>
        void AddPolyline(IReadOnlyList<Point2> points, bool closed, string layer, int colorIndex = 0);

        /// <summary>Залитый многоугольник (в САПР — контур с заливкой или штриховка).</summary>
        void AddFilledPolygon(IReadOnlyList<Point2> points, string layer, int colorIndex = 0);

        /// <summary>
        /// Текст: начало базовой линии и ВЫСОТА ПРОПИСНЫХ в мм чертежа.
        /// Именно так высоту понимает DXF (код 40): 5 мм означает прописную 5 мм, а не кегль 5.
        /// Кегль модели (TextLayout.HeightMm) переводится в высоту прописных через CapHeightMm.
        /// </summary>
        void AddText(string text, Point2 baselineStart, double capHeightMm, string layer,
            double rotationDeg = 0.0, int colorIndex = 0);

        /// <summary>Сохраняет документ в файл.</summary>
        void SaveDocument(string path);
    }
}
