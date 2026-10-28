using System;
using System.Collections.Generic;
using OddMaster.Core.Generation;
using OddMaster.Core.Geometry;
using OddMaster.Core.Ports;
using OddMaster.Core.Typography;

namespace OddMaster.Render.Dxf
{
    /// <summary>
    /// Реализация приёмника чертежа в формате DXF R12 — стенд для Linux без nanoCAD.
    /// Одновременно реализует <see cref="ICadBackend"/> и <see cref="IDrawingPort"/>, поэтому
    /// геометрия знака уходит в чертёж тем же путём, каким пойдёт в nanoCAD (ADR-0025).
    /// </summary>
    public sealed class DxfBackend : ICadBackend, IDrawingPort
    {
        private readonly DxfDocument _document = new DxfDocument();
        private readonly double _flattenSagittaMm;
        private double _capHeightRatio = 1.0;

        /// <summary>Создаёт приёмник. sagitta — допуск замены дуг хордами, мм (из config/conventions/drawing.json).</summary>
        public DxfBackend(double flattenSagittaMm)
        {
            if (!(flattenSagittaMm > 0))
                throw new ArgumentOutOfRangeException(nameof(flattenSagittaMm),
                    "Допуск аппроксимации дуг должен быть положительным, получено " + flattenSagittaMm + ".");
            _flattenSagittaMm = flattenSagittaMm;
        }

        /// <summary>Имя формата.</summary>
        public string BackendName => _document.FormatName;

        /// <summary>Число сущностей.</summary>
        public int EntityCount => _document.EntityCount;

        /// <summary>Слои документа.</summary>
        public IReadOnlyList<DxfLayerDefinition> Layers => _document.Layers;

        /// <summary>Готовый документ (для сохранения и тестов).</summary>
        public DxfDocument Document => _document;

        /// <summary>Единицы чертежа для HEADER: 4 — мм, 6 — метры.</summary>
        public int InsUnits
        {
            get => _document.InsUnits;
            set => _document.InsUnits = value;
        }

        /// <summary>Префикс имён слоёв. Позволяет держать в одном чертеже знаки с разными цветами каймы.</summary>
        public string ScopePrefix { get; set; } = string.Empty;

        /// <summary>Допуск аппроксимации дуг, мм.</summary>
        public double FlattenSagittaMm => _flattenSagittaMm;

        /// <summary>Объявляет слой с учётом префикса.</summary>
        public void AddLayer(string name, int colorIndex) => _document.AddLayer(Scoped(name), colorIndex);

        /// <summary>Отрезок.</summary>
        public void AddLine(Point2 start, Point2 end, string layer, int colorIndex = 0) =>
            _document.AddLine(start, end, Scoped(layer), colorIndex);

        /// <summary>Дуга.</summary>
        public void AddArc(Point2 center, double radius, double startAngleDeg, double sweepDeg, string layer,
            int colorIndex = 0) =>
            _document.AddArc(center, radius, startAngleDeg, sweepDeg, Scoped(layer), colorIndex);

        /// <summary>Полилиния.</summary>
        public void AddPolyline(IReadOnlyList<Point2> points, bool closed, string layer, int colorIndex = 0) =>
            _document.AddPolyline(points, closed, Scoped(layer), colorIndex);

        /// <summary>Залитый многоугольник.</summary>
        public void AddFilledPolygon(IReadOnlyList<Point2> points, string layer, int colorIndex = 0) =>
            _document.AddFilledPolygon(points, Scoped(layer), colorIndex);

        /// <summary>Текст. heightMm — высота прописных (семантика DXF TEXT, код 40).</summary>
        public void AddText(string text, Point2 baselineStart, double heightMm, string layer,
            double rotationDeg = 0, int colorIndex = 0) =>
            _document.AddText(text, baselineStart, heightMm, Scoped(layer), rotationDeg, colorIndex);

        /// <summary>Текст по раскладке TextEngine: кегль модели переводится в высоту прописных.</summary>
        public void AddText(string text, OddMaster.Core.Typography.TextLayout layout, string layer,
            double rotationDeg = 0, int colorIndex = 0)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            AddText(text, layout.BaselineStart, layout.CapHeightMm, layer, rotationDeg, colorIndex);
        }

        /// <summary>Сохраняет документ.</summary>
        public void SaveDocument(string path) => _document.Save(path);

        // ---- IDrawingPort: путь «ядро → приёмник» для геометрии знака ----

        void IDrawingPort.DrawLine(Point2 start, Point2 end, string layer) => AddLine(start, end, layer);

        void IDrawingPort.DrawArc(Point2 center, double radius, double startAngleDeg, double sweepDeg, string layer) =>
            AddArc(center, radius, startAngleDeg, sweepDeg, layer);

        /// <summary>
        /// Заливка контура: сначала точный контур (дуги дугами, отрезки отрезками), затем заливка
        /// веером SOLID. Так делает и чертёжник: граница остаётся точной, аппроксимируется только
        /// заливка. Иначе круг знака в файле терял бы дуги.
        /// </summary>
        void IDrawingPort.DrawFilledPath(Path path, string layer)
        {
            foreach (var segment in path.Segments)
            {
                if (segment is ArcSegment arc)
                    AddArc(arc.Center, arc.Radius, arc.StartAngleDeg, arc.SweepDeg, layer);
                else AddLine(segment.Start, segment.End, layer);
            }
            AddFilledPolygon(PathFlattener.FlattenClosed(path, _flattenSagittaMm), layer);
        }

        /// <summary>Порт отдаёт кегль модели (em), а DXF ждёт высоту прописных: переводим здесь.</summary>
        void IDrawingPort.DrawText(string text, Point2 anchor, double heightMm, string layer) =>
            AddText(text, anchor, heightMm * _capHeightRatio, layer);

        /// <summary>
        /// Рисует знак в документ через порт ядра (GeometryPortWriter) и добавляет баннер «ЧЕРНОВИК».
        /// scope — префикс слоёв (например, «P1.A»); origin — положение, scale — мм чертежа на мм знака.
        /// </summary>
        public void AddSign(SignGeometry geometry, Point2 origin, double scale, string scope, FontMetrics font)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            if (font == null) throw new ArgumentNullException(nameof(font));
            if (!(scale > 0)) throw new ArgumentOutOfRangeException(nameof(scale), "Масштаб должен быть > 0.");
            var previous = ScopePrefix;
            var previousRatio = _capHeightRatio;
            ScopePrefix = scope;
            CapHeightRatio = font.CapHeightRatio;
            try
            {
                foreach (var layer in geometry.Layers)
                {
                    if (layer.Geometry == null) continue;
                    AddLayer(layer.Kind.ToString(), DxfColorMap.Aci(layer.ColorName));
                }
                if (geometry.Texts.Count > 0)
                    AddLayer("Text", DxfColorMap.Aci(geometry.Texts[0].ColorName));
                if (geometry.IsDraft) AddLayer("Watermark", DxfColorMap.Aci("Red"));

                GeometryPortWriter.Write(this, geometry, origin, scale);

                if (geometry.IsDraft)
                    foreach (var line in DraftWatermark.Layout(geometry, font))
                        AddText(line.Value, Transform(line.BaselineStart, origin, scale),
                            line.CapHeightMm * scale, "Watermark");
            }
            finally
            {
                ScopePrefix = previous;
                _capHeightRatio = previousRatio;
            }
        }

        /// <summary>Перевод точки знака в координаты чертежа.</summary>
        public static Point2 Transform(Point2 point, Point2 origin, double scale) =>
            new Point2(origin.X + point.X * scale, origin.Y + point.Y * scale);

        /// <summary>Круг как замкнутая полилиния: в R12 дуги есть, но круг удобнее одной сущностью.</summary>
        public void AddCircle(Point2 center, double radius, string layer, int colorIndex = 0)
        {
            var circle = GeometryPrimitives.Circle(radius * 2);
            var points = new List<Point2>();
            foreach (var point in PathFlattener.FlattenClosed(circle, _flattenSagittaMm))
                points.Add(new Point2(center.X + point.X, center.Y + point.Y));
            AddPolyline(points, true, layer, colorIndex);
        }

        /// <summary>Отношение высоты прописных к кеглю: приходит из config/typography/fonts.json.</summary>
        public double CapHeightRatio
        {
            get => _capHeightRatio;
            set
            {
                if (!(value > 0) || value > 1)
                    throw new ArgumentOutOfRangeException(nameof(value),
                        "capHeightRatio должен быть в (0; 1], получено " + value + ".");
                _capHeightRatio = value;
            }
        }

        private string Scoped(string layer)
        {
            var prefix = ScopePrefix;
            if (string.IsNullOrEmpty(prefix)) return layer;
            return prefix + "." + layer;
        }
    }
}
