using System;
using OddMaster.Core.Config;
using OddMaster.Core.Generation;
using OddMaster.Core.Geometry;
using OddMaster.Core.Typography;

namespace OddMaster.Render.Dxf
{
    /// <summary>
    /// Запись одного знака в DXF: hello-чертёж стенда. Использует тот же порт, что пойдёт в nanoCAD,
    /// поэтому доказательство «ядро → примитивы чертежа» действительно сквозное.
    /// </summary>
    public static class SignDxfWriter
    {
        /// <summary>Строит документ с одним знаком в масштабе 1:1 (мм знака = мм чертежа).</summary>
        public static DxfBackend Build(SignGeometry geometry, DrawingConventions drawing, FontMetrics font)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            if (drawing == null) throw new ArgumentNullException(nameof(drawing));
            if (font == null) throw new ArgumentNullException(nameof(font));
            var backend = new DxfBackend(drawing.DxfFlattenSagittaMm) { InsUnits = 4 };
            backend.AddSign(geometry, new Point2(0, 0), 1.0, "SIGN", font);
            return backend;
        }
    }
}
