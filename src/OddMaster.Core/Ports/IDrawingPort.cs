using OddMaster.Core.Geometry;

namespace OddMaster.Core.Ports
{
    /// <summary>Порт отрисовки: ядро отдаёт примитивы, адаптер САПР их рисует.</summary>
    public interface IDrawingPort
    {
        /// <summary>Отрезок.</summary>
        void DrawLine(Point2 start, Point2 end, string layer);
        /// <summary>Дуга окружности (углы в градусах, Y вверх).</summary>
        void DrawArc(Point2 center, double radius, double startAngleDeg, double sweepDeg, string layer);
        /// <summary>Замкнутый контур (для заливки).</summary>
        void DrawFilledPath(Path path, string layer);
        /// <summary>Текст.</summary>
        void DrawText(string text, Point2 anchor, double heightMm, string layer);
    }
}
