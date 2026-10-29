// ВНИМАНИЕ: файл НЕ компилировался — SDK nanoCAD 26 отсутствует.
// Каждая строка с обращением к SDK помечена SDK-NNN и описана в docs/SDK_ASSUMPTIONS.md.
using System;
using OddMaster.Core.Geometry;
using OddMaster.Core.Ports;

namespace OddMaster.NanoCAD
{
    /// <summary>Реализация порта отрисовки через примитивы MultiCAD (предположение, SDK-006).</summary>
    internal sealed class NanoCadDrawingPort : IDrawingPort
    {
        private readonly object _document;

        /// <summary>Создаёт порт для активного документа.</summary>
        internal NanoCadDrawingPort(object document) { _document = document; }

        /// <summary>Отрезок. [ПРОВЕРИТЬ В SDK: SDK-006]</summary>
        public void DrawLine(Point2 start, Point2 end, string layer)
        {
            // [ПРОВЕРИТЬ В SDK: SDK-006] точный тип примитива и способ добавления в документ.
            throw new NotImplementedException("Требуется проверка API MultiCAD.Geometry (SDK-006).");
        }

        /// <summary>Дуга. [ПРОВЕРИТЬ В SDK: SDK-006]</summary>
        public void DrawArc(Point2 center, double radius, double startAngleDeg, double sweepDeg, string layer)
        {
            // [ПРОВЕРИТЬ В SDK: SDK-006] единицы углов (градусы/радианы) и направление развёртки.
            throw new NotImplementedException("Требуется проверка API MultiCAD.Geometry (SDK-006).");
        }

        /// <summary>Замкнутый контур для заливки. [ПРОВЕРИТЬ В SDK: SDK-006]</summary>
        public void DrawFilledPath(Path path, string layer)
        {
            // [ПРОВЕРИТЬ В SDK: SDK-006] нужен ли отдельный тип штриховки/заливки.
            throw new NotImplementedException("Требуется проверка API MultiCAD.Geometry (SDK-006).");
        }

        /// <summary>Текст. [ПРОВЕРИТЬ В SDK: SDK-006]</summary>
        public void DrawText(string text, Point2 anchor, double heightMm, string layer)
        {
            // [ПРОВЕРИТЬ В SDK: SDK-006] тип текстового примитива и единицы высоты.
            throw new NotImplementedException("Требуется проверка API MultiCAD.Geometry (SDK-006).");
        }
    }
}
