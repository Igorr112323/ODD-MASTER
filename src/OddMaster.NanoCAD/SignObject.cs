// ВНИМАНИЕ: файл НЕ компилировался — SDK nanoCAD 26 отсутствует.
// Все обращения к SDK помечены SDK-NNN; см. docs/SDK_ASSUMPTIONS.md.
using System;
using OddMaster.Core.Generation;
using OddMaster.Core.Ports;

namespace OddMaster.NanoCAD
{
    /// <summary>
    /// Кастомный объект знака на чертеже. Геометрия приходит из OddMaster.Core;
    /// адаптер только создаёт примитивы САПР.
    /// </summary>
    public class SignObject
    {
        private SignGeometry? _geometry;

        /// <summary>Устанавливает геометрию знака из ядра.</summary>
        public void SetGeometry(SignGeometry geometry)
        {
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        }

        /// <summary>
        /// Отрисовка. [ПРОВЕРИТЬ В SDK: SDK-004, SDK-005]
        /// Базовый класс McCustomBase и имя метода OnDraw — предположения по публикациям 2014–2017.
        /// </summary>
        public void OnDraw(object context)
        {
            // [ПРОВЕРИТЬ В SDK: SDK-004] наследование от McCustomBase, его сборка.
            // [ПРОВЕРИТЬ В SDK: SDK-005] сигнатура метода отрисовки.
            // [ПРОВЕРИТЬ В SDK: SDK-006] документ -> порт примитивов.
            if (_geometry == null) return;
            IDrawingPort port = new NanoCadDrawingPort(context);
            GeometryPortWriter.Write(port, _geometry, new OddMaster.Core.Geometry.Point2(0, 0), 1.0);
        }
    }
}
