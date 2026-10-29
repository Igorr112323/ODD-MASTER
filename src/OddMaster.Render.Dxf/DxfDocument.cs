using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OddMaster.Core.Geometry;

namespace OddMaster.Render.Dxf
{
    /// <summary>Слой DXF: имя и индекс цвета ACI.</summary>
    public sealed class DxfLayerDefinition
    {
        /// <summary>Создаёт слой.</summary>
        public DxfLayerDefinition(string name, int colorIndex) { Name = name; ColorIndex = colorIndex; }

        /// <summary>Имя слоя.</summary>
        public string Name { get; }
        /// <summary>Индекс цвета ACI (1–255).</summary>
        public int ColorIndex { get; }
    }

    /// <summary>
    /// Минимальный детерминированный DXF R12 (AC1009): HEADER + TABLES(LAYER) + ENTITIES + EOF.
    /// Никаких библиотек, только групповые коды. Файл — чистый ASCII (кириллица через \U+XXXX),
    /// поэтому одинаковый вход даёт побайтно одинаковый выход.
    /// </summary>
    public sealed class DxfDocument
    {
        private readonly List<DxfLayerDefinition> _layers = new List<DxfLayerDefinition>();
        private readonly HashSet<string> _layerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _entities = new List<string>();
        private readonly List<Point2> _extents = new List<Point2>();

        /// <summary>Имя формата для отчётов.</summary>
        public string FormatName => "DXF R12 (AC1009)";

        /// <summary>Число сущностей в секции ENTITIES.</summary>
        public int EntityCount => _entities.Count;

        /// <summary>Объявленные слои в порядке объявления.</summary>
        public IReadOnlyList<DxfLayerDefinition> Layers => _layers;

        /// <summary>Углы поворота текста по умолчанию, градусы.</summary>
        public double DefaultTextRotationDeg { get; set; }

        /// <summary>Единицы чертежа по $INSUNITS: 4 — миллиметры, 6 — метры.</summary>
        public int InsUnits { get; set; } = 4;

        /// <summary>Объявляет слой. Повторное имя игнорируется: цвет первого объявления побеждает.</summary>
        public void AddLayer(string name, int colorIndex)
        {
            var safe = ValidateName(name);
            if (!_layerNames.Add(safe)) return;
            _layers.Add(new DxfLayerDefinition(safe, DxfColorMap.Normalize(colorIndex)));
        }

        /// <summary>Отрезок.</summary>
        public void AddLine(Point2 start, Point2 end, string layer, int colorIndex = 0)
        {
            EnsureLayer(layer, colorIndex);
            var sb = Begin("LINE", layer, colorIndex);
            Pair(sb, 10, start.X); Pair(sb, 20, start.Y); Pair(sb, 30, 0);
            Pair(sb, 11, end.X); Pair(sb, 21, end.Y); Pair(sb, 31, 0);
            _entities.Add(sb.ToString());
            _extents.Add(start); _extents.Add(end);
        }

        /// <summary>Дуга. Развёртка любого знака приводится к паре «начало/конец» против часовой.</summary>
        public void AddArc(Point2 center, double radius, double startAngleDeg, double sweepDeg, string layer,
            int colorIndex = 0)
        {
            if (!(radius > 0)) throw new ArgumentOutOfRangeException(nameof(radius), "Радиус дуги должен быть > 0.");
            if (Math.Abs(sweepDeg) < 1e-9) return;
            EnsureLayer(layer, colorIndex);
            var start = sweepDeg >= 0 ? startAngleDeg : startAngleDeg + sweepDeg;
            var end = sweepDeg >= 0 ? startAngleDeg + sweepDeg : startAngleDeg;
            var sb = Begin("ARC", layer, colorIndex);
            Pair(sb, 10, center.X); Pair(sb, 20, center.Y); Pair(sb, 30, 0);
            Pair(sb, 40, radius);
            Pair(sb, 50, NormalizeAngle(start));
            Pair(sb, 51, NormalizeAngle(end));
            _entities.Add(sb.ToString());
            var arc = new ArcSegment(center, radius, startAngleDeg, sweepDeg);
            foreach (var extreme in arc.ExtremePoints()) _extents.Add(extreme);
        }

        /// <summary>Полилиния.</summary>
        public void AddPolyline(IReadOnlyList<Point2> points, bool closed, string layer, int colorIndex = 0)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Count < 2) return;
            EnsureLayer(layer, colorIndex);
            var sb = Begin("POLYLINE", layer, colorIndex);
            Pair(sb, 66, 1);
            Pair(sb, 70, closed ? 1 : 0);
            Pair(sb, 10, 0); Pair(sb, 20, 0); Pair(sb, 30, 0);
            var tail = new StringBuilder();
            foreach (var point in points)
            {
                Pair(tail, 0, "VERTEX");
                Pair(tail, 8, layer);
                Pair(tail, 10, point.X); Pair(tail, 20, point.Y); Pair(tail, 30, 0);
                _extents.Add(point);
            }
            Pair(tail, 0, "SEQEND");
            Pair(tail, 8, layer);
            _entities.Add(sb.ToString() + tail);
        }

        /// <summary>Залитый многоугольник: веер треугольников через SOLID (в R12 нет HATCH).</summary>
        public void AddFilledPolygon(IReadOnlyList<Point2> points, string layer, int colorIndex = 0)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Count < 3) return;
            EnsureLayer(layer, colorIndex);
            var first = points[0];
            for (var i = 1; i < points.Count - 1; i++)
            {
                var sb = Begin("SOLID", layer, colorIndex);
                Pair(sb, 10, first.X); Pair(sb, 20, first.Y); Pair(sb, 30, 0);
                Pair(sb, 11, points[i].X); Pair(sb, 21, points[i].Y); Pair(sb, 31, 0);
                Pair(sb, 12, points[i + 1].X); Pair(sb, 22, points[i + 1].Y); Pair(sb, 32, 0);
                Pair(sb, 13, points[i + 1].X); Pair(sb, 23, points[i + 1].Y); Pair(sb, 33, 0);
                _entities.Add(sb.ToString());
            }
            foreach (var point in points) _extents.Add(point);
        }

        /// <summary>Текст: начало базовой линии, высота прописных (код 40) и поворот в градусах.</summary>
        public void AddText(string text, Point2 baselineStart, double heightMm, string layer,
            double rotationDeg = 0, int colorIndex = 0)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!(heightMm > 0)) throw new ArgumentOutOfRangeException(nameof(heightMm), "Высота текста должна быть > 0.");
            EnsureLayer(layer, colorIndex);
            var sb = Begin("TEXT", layer, colorIndex);
            Pair(sb, 10, baselineStart.X); Pair(sb, 20, baselineStart.Y); Pair(sb, 30, 0);
            Pair(sb, 40, heightMm);
            Pair(sb, 1, DxfTextCodec.Encode(text));
            var rotation = Math.Abs(rotationDeg) < 1e-9 ? DefaultTextRotationDeg : rotationDeg;
            if (Math.Abs(rotation) > 1e-9) Pair(sb, 50, rotation);
            _entities.Add(sb.ToString());
            _extents.Add(baselineStart);
        }

        /// <summary>Габарит по фактическим экстремумам сущностей (дуги — по четвертным точкам).</summary>
        public BoundingBox? Bounds()
        {
            if (_extents.Count == 0) return null;
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var point in _extents)
            {
                if (point.X < minX) minX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.X > maxX) maxX = point.X;
                if (point.Y > maxY) maxY = point.Y;
            }
            return new BoundingBox(minX, minY, maxX, maxY);
        }

        /// <summary>Собирает текст файла: HEADER, TABLES, ENTITIES, EOF. Порядок сущностей — порядок вызовов.</summary>
        public string Build()
        {
            var sb = new StringBuilder();
            WriteHeader(sb);
            WriteTables(sb);
            Pair(sb, 0, "SECTION");
            Pair(sb, 2, "ENTITIES");
            foreach (var entity in _entities) sb.Append(entity);
            Pair(sb, 0, "ENDSEC");
            Pair(sb, 0, "EOF");
            return sb.ToString();
        }

        /// <summary>Сохраняет файл в ASCII-кодировке без BOM (детерминированно).</summary>
        public void Save(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            var directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllText(path, Build(), new UTF8Encoding(false));
        }

        private void WriteHeader(StringBuilder sb)
        {
            double minX = 0, minY = 0, maxX = 0, maxY = 0;
            var hasExtents = _extents.Count > 0;
            if (hasExtents)
            {
                minX = double.MaxValue; minY = double.MaxValue;
                maxX = double.MinValue; maxY = double.MinValue;
                foreach (var point in _extents)
                {
                    if (point.X < minX) minX = point.X;
                    if (point.Y < minY) minY = point.Y;
                    if (point.X > maxX) maxX = point.X;
                    if (point.Y > maxY) maxY = point.Y;
                }
            }
            Pair(sb, 0, "SECTION");
            Pair(sb, 2, "HEADER");
            Pair(sb, 9, "$ACADVER");
            Pair(sb, 1, "AC1009");
            Pair(sb, 9, "$DWGCODEPAGE");
            Pair(sb, 3, "ANSI_1251");
            Pair(sb, 9, "$INSUNITS");
            Pair(sb, 70, InsUnits);
            Pair(sb, 9, "$MEASUREMENT");
            Pair(sb, 70, 1);
            Pair(sb, 9, "$EXTMIN");
            Pair(sb, 10, minX); Pair(sb, 20, minY); Pair(sb, 30, 0);
            Pair(sb, 9, "$EXTMAX");
            Pair(sb, 10, maxX); Pair(sb, 20, maxY); Pair(sb, 30, 0);
            Pair(sb, 0, "ENDSEC");
        }

        private void WriteTables(StringBuilder sb)
        {
            Pair(sb, 0, "SECTION");
            Pair(sb, 2, "TABLES");

            Pair(sb, 0, "TABLE");
            Pair(sb, 2, "LTYPE");
            Pair(sb, 70, 1);
            Pair(sb, 0, "LTYPE");
            Pair(sb, 2, "CONTINUOUS");
            Pair(sb, 70, 0);
            Pair(sb, 3, "Solid line");
            Pair(sb, 72, 65);
            Pair(sb, 73, 0);
            Pair(sb, 40, 0);
            Pair(sb, 0, "ENDTAB");

            Pair(sb, 0, "TABLE");
            Pair(sb, 2, "LAYER");
            Pair(sb, 70, _layers.Count);
            foreach (var layer in _layers)
            {
                Pair(sb, 0, "LAYER");
                Pair(sb, 2, layer.Name);
                Pair(sb, 70, 0);
                Pair(sb, 62, layer.ColorIndex);
                Pair(sb, 6, "CONTINUOUS");
            }
            Pair(sb, 0, "ENDTAB");

            Pair(sb, 0, "ENDSEC");
        }

        private void EnsureLayer(string layer, int colorIndex)
        {
            var safe = ValidateName(layer);
            if (!_layerNames.Contains(safe)) AddLayer(safe, colorIndex);
        }

        private static StringBuilder Begin(string type, string layer, int colorIndex)
        {
            var sb = new StringBuilder();
            Pair(sb, 0, type);
            Pair(sb, 8, ValidateName(layer));
            var color = DxfColorMap.Normalize(colorIndex);
            if (color != 0) Pair(sb, 62, color);
            return sb;
        }

        private static string ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Имя слоя пустое.", nameof(name));
            foreach (var c in name)
                if (!(char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-' || c == '$'))
                    throw new ArgumentException("Недопустимый символ «" + c + "» в имени слоя «" + name +
                        "». Разрешены буквы, цифры, '.', '_', '-', '$'.", nameof(name));
            if (name.Length > 255)
                throw new ArgumentException("Имя слоя длиннее 255 символов: " + name.Length + ".", nameof(name));
            return name;
        }

        private static double NormalizeAngle(double degrees)
        {
            var value = degrees % 360.0;
            if (value < 0) value += 360.0;
            return Math.Abs(value) < 1e-9 ? 0 : value;
        }

        private static void Pair(StringBuilder sb, int code, string value)
        {
            sb.Append(code.ToString(CultureInfo.InvariantCulture)).Append('\n').Append(value).Append('\n');
        }

        private static void Pair(StringBuilder sb, int code, double value) => Pair(sb, code, Num(value));

        private static string Num(double value)
        {
            var rounded = Math.Round(value, 6, MidpointRounding.AwayFromZero);
            if (Math.Abs(rounded) < 5e-7) rounded = 0;
            return rounded.ToString("0.######", CultureInfo.InvariantCulture);
        }
    }
}
