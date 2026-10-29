using System;
using System.Collections.Generic;
using OddMaster.Core.Catalog;
using OddMaster.Core.Geometry;
using OddMaster.Core.Generation;

namespace OddMaster.Core.Composition
{
    /// <summary>
    /// Строит примитивы состава знака по параметрам из config/elements/elements.json.
    /// Никаких координат из ГОСТ в коде: только доли, которые приходят из данных (CR-22).
    /// Все элементы детерминированы, порядок слоёв — порядок в каталоге знака.
    /// </summary>
    public static class CompositionBuilder
    {
        /// <summary>
        /// Строит слои состава. field — габарит поля знака (центр в 0,0), outline — наружный контур.
        /// </summary>
        public static List<SignLayer> Build(IReadOnlyList<CompositionElement> elements,
            BoundingBox field, Path outline)
        {
            if (elements == null) throw new ArgumentNullException(nameof(elements));
            if (outline == null) throw new ArgumentNullException(nameof(outline));
            var layers = new List<SignLayer>();
            var minSide = Math.Min(field.Width, field.Height);
            if (minSide <= 0) return layers;
            var mask = field;
            var maskPolygon = Flatten(outline);
            foreach (var element in elements)
            {
                switch (element.Kind)
                {
                    case CompositionKind.Contour:
                        layers.Add(new SignLayer(SignLayerKind.Composition, element.ColorName, outline, false,
                            minSide * element.StrokeRatio));
                        break;
                    case CompositionKind.Rectangle:
                    case CompositionKind.Panel:
                        layers.Add(Filled(element, Rectangle(
                            Center(element, field), field.Width * element.WidthRatio,
                            field.Height * element.HeightRatio)));
                        break;
                    case CompositionKind.Diamond:
                        layers.Add(Filled(element, Diamond(Center(element, field),
                            field.Width * element.WidthRatio, field.Height * element.HeightRatio)));
                        break;
                    case CompositionKind.Triangle:
                        layers.Add(Filled(element, Triangle(
                            Center(element, field), field.Width * element.WidthRatio,
                            field.Height * element.HeightRatio)));
                        break;
                    case CompositionKind.Cross:
                        foreach (var angle in new[] { 45.0, -45.0 })
                            layers.Add(Filled(element, Band(Center(element, field),
                                FitLength(Center(element, field), mask, minSide * element.BandRatio, angle,
                                    minSide * element.LengthRatio),
                                minSide * element.BandRatio, angle, maskPolygon)));
                        break;
                    case CompositionKind.Band:
                        layers.Add(Filled(element, Fitted(element, mask, maskPolygon, minSide)));
                        break;
                    case CompositionKind.Bundle:
                        for (var i = 0; i < element.Count; i++)
                        {
                            var offset = (i - (element.Count - 1) / 2.0) * minSide * element.SpacingRatio;
                            var center = new Point2(Center(element, field).X, Center(element, field).Y + offset);
                            layers.Add(Filled(element, Band(center,
                                FitLength(center, mask, minSide * element.BandRatio, element.AngleDeg,
                                    minSide * element.LengthRatio),
                                minSide * element.BandRatio, element.AngleDeg, maskPolygon)));
                        }
                        break;
                    case CompositionKind.Nested:
                        layers.AddRange(Nested(element, Center(element, field), minSide * element.SizeRatio));
                        break;
                    case CompositionKind.Arrow:
                        layers.Add(Filled(element, Arrow(Center(element, field),
                            minSide * element.SizeRatio, element.LengthRatio, element.Direction)));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(elements),
                            "неизвестный вид примитива состава: " + element.Kind);
                }
            }
            return layers;
        }

        /// <summary>Одна полоса элемента, вписанная в габарит поля.</summary>
        private static Path Fitted(CompositionElement element, BoundingBox field,
            IReadOnlyList<Point2> mask, double minSide)
        {
            var center = Center(element, field);
            var width = minSide * element.BandRatio;
            return Band(center, FitLength(center, field, width, element.AngleDeg,
                minSide * element.LengthRatio), width, element.AngleDeg, mask);
        }

        /// <summary>
        /// Ограничивает длину полосы так, чтобы её габарит не выходил за поле знака:
        /// полоса под 45° длиной в сторону круга вылезала за габарит (M9-V01).
        /// </summary>
        private static double FitLength(Point2 center, BoundingBox field, double width, double angleDeg,
            double desired)
        {
            var radians = angleDeg * Math.PI / 180.0;
            var cos = Math.Abs(Math.Cos(radians));
            var sin = Math.Abs(Math.Sin(radians));
            var limit = desired;
            if (cos > 1e-9)
                limit = Math.Min(limit, 2 * (Math.Min(field.MaxX - center.X, center.X - field.MinX) -
                    width / 2 * sin) / cos);
            if (sin > 1e-9)
                limit = Math.Min(limit, 2 * (Math.Min(field.MaxY - center.Y, center.Y - field.MinY) -
                    width / 2 * cos) / sin);
            return Math.Max(limit, 0);
        }

        private static SignLayer Filled(CompositionElement element, Path path) =>
            new SignLayer(SignLayerKind.Composition, element.ColorName, path, true, 0);

        private static Point2 Center(CompositionElement element, BoundingBox field) =>
            new Point2(field.MinX + field.Width * (0.5 + element.XRatio),
                field.MinY + field.Height * (0.5 + element.YRatio));

        private static Path Rectangle(Point2 center, double width, double height)
        {
            var x = width / 2;
            var y = height / 2;
            return new Path(new Point2(center.X - x, center.Y - y))
                .LineTo(new Point2(center.X + x, center.Y - y))
                .LineTo(new Point2(center.X + x, center.Y + y))
                .LineTo(new Point2(center.X - x, center.Y + y))
                .Close();
        }

        private static Path Triangle(Point2 center, double width, double height)
        {
            var x = width / 2;
            var y = height / 2;
            return new Path(new Point2(center.X - x, center.Y - y))
                .LineTo(new Point2(center.X + x, center.Y - y))
                .LineTo(new Point2(center.X, center.Y + y))
                .Close();
        }

        /// <summary>
        /// Полоса заданной длины и толщины под углом: замкнутый прямоугольник.
        /// Единичные векторы считаются один раз, углы — против часовой (Y вверх).
        /// </summary>
        private static Path Band(Point2 center, double length, double width, double angleDeg,
            IReadOnlyList<Point2> mask = null)
        {
            var radians = angleDeg * Math.PI / 180.0;
            var ux = Math.Cos(radians);
            var uy = Math.Sin(radians);
            var px = -uy;
            var py = ux;
            var halfLength = length / 2;
            var halfWidth = width / 2;
            Point2 Corner(double along, double across) => new Point2(
                center.X + ux * along + px * across,
                center.Y + uy * along + py * across);
            var a = Corner(-halfLength, -halfWidth);
            var b = Corner(halfLength, -halfWidth);
            var c = Corner(halfLength, halfWidth);
            var d = Corner(-halfLength, halfWidth);
            if (mask != null)
            {
                var clipped = PolygonClip.Clip(new[] { a, b, c, d }, mask);
                if (clipped.Count >= 3) return PolygonClip.ToPath(clipped);
            }
            return new Path(a).LineTo(b).LineTo(c).LineTo(d).Close();
        }

        /// <summary>
        /// Сводит наружный контур знака к ломаной для отсечения. Дуги разбиваются по 15° —
        /// точности «внутри знака» это достаточно, а результат остаётся детерминированным.
        /// </summary>
        private static IReadOnlyList<Point2> Flatten(Path path)
        {
            var points = new List<Point2> { path.Start };
            var current = path.Start;
            foreach (var segment in path.Segments)
            {
                if (segment is ArcSegment arc)
                {
                    var steps = Math.Max(4, (int)Math.Ceiling(Math.Abs(arc.SweepDeg) / 15.0));
                    var center = ArcCenter(current, arc);
                    var startAngle = Math.Atan2(current.Y - center.Y, current.X - center.X) * 180.0 / Math.PI;
                    for (var i = 1; i <= steps; i++)
                    {
                        var angle = (startAngle + arc.SweepDeg * i / steps) * Math.PI / 180.0;
                        points.Add(new Point2(center.X + arc.Radius * Math.Cos(angle),
                            center.Y + arc.Radius * Math.Sin(angle)));
                    }
                }
                else
                {
                    points.Add(segment.End);
                }
                current = segment.End;
            }
            if (points.Count > 1 && points[0].DistanceTo(points[points.Count - 1]) < 1e-9)
                points.RemoveAt(points.Count - 1);
            return points;
        }

        /// <summary>Центр дуги по началу, концу и радиусу (два варианта — берём согласованный со знаком развёртки).</summary>
        private static Point2 ArcCenter(Point2 start, ArcSegment arc)
        {
            var dx = arc.End.X - start.X;
            var dy = arc.End.Y - start.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < 1e-9) return start;
            var half = distance / 2;
            var heightSquared = arc.Radius * arc.Radius - half * half;
            var height = heightSquared > 0 ? Math.Sqrt(heightSquared) : 0;
            var ux = -dy / distance;
            var uy = dx / distance;
            var mx = (start.X + arc.End.X) / 2;
            var my = (start.Y + arc.End.Y) / 2;
            return new Point2(mx + ux * height, my + uy * height);
        }

        /// <summary>Ромб (2.1/2.2): жёлтое поле внутри белого ромба.</summary>
        private static Path Diamond(Point2 center, double width, double height)
        {
            var x = width / 2;
            var y = height / 2;
            return new Path(new Point2(center.X, center.Y - y))
                .LineTo(new Point2(center.X + x, center.Y))
                .LineTo(new Point2(center.X, center.Y + y))
                .LineTo(new Point2(center.X - x, center.Y))
                .Close();
        }

        private static IEnumerable<Path> Cross(Point2 center, double length, double width) => new[]
        {
            Band(center, length, width, 45),
            Band(center, length, width, -45)
        };

        /// <summary>Вложенный знак: контур круга серым/чёрным и красный крест внутри (5.41, 5.42).</summary>
        private static IEnumerable<SignLayer> Nested(CompositionElement element, Point2 center, double size)
        {
            var circle = new Path(new Point2(center.X + size / 2, center.Y));
            for (var i = 0; i < 4; i++)
                circle.ArcTo(center, size / 2, i * 90.0, 90.0);
            circle.Close();
            yield return new SignLayer(SignLayerKind.Composition, element.ColorName, circle, false,
                size * element.BandRatio);
            var accent = string.IsNullOrEmpty(element.AccentColor) ? element.ColorName : element.AccentColor;
            foreach (var path in Cross(center, size * 0.7, size * element.BandRatio))
                yield return new SignLayer(SignLayerKind.Composition, accent, path, true, 0);
        }

        /// <summary>
        /// Стрелка-указатель таблички: древко и треугольная головка.
        /// Направление приходит из конфига; длина задаётся lengthRatio (8.2.3 — длинная вниз).
        /// </summary>
        private static Path Arrow(Point2 center, double size, double lengthRatio, string? direction)
        {
            var half = size * lengthRatio / 2;
            var head = size * 0.45;
            var shaft = size * 0.22;
            switch (direction ?? "up")
            {
                case "up":
                    return new Path(new Point2(center.X - shaft, center.Y - half))
                        .LineTo(new Point2(center.X + shaft, center.Y - half))
                        .LineTo(new Point2(center.X + shaft, center.Y + half - head))
                        .LineTo(new Point2(center.X + head, center.Y + half - head))
                        .LineTo(new Point2(center.X, center.Y + half))
                        .LineTo(new Point2(center.X - head, center.Y + half - head))
                        .LineTo(new Point2(center.X - shaft, center.Y + half - head))
                        .Close();
                case "down":
                    return new Path(new Point2(center.X - shaft, center.Y + half))
                        .LineTo(new Point2(center.X + shaft, center.Y + half))
                        .LineTo(new Point2(center.X + shaft, center.Y - half + head))
                        .LineTo(new Point2(center.X + head, center.Y - half + head))
                        .LineTo(new Point2(center.X, center.Y - half))
                        .LineTo(new Point2(center.X - head, center.Y - half + head))
                        .LineTo(new Point2(center.X - shaft, center.Y - half + head))
                        .Close();
                case "left":
                    return new Path(new Point2(center.X + half, center.Y - shaft))
                        .LineTo(new Point2(center.X + half, center.Y + shaft))
                        .LineTo(new Point2(center.X - half + head, center.Y + shaft))
                        .LineTo(new Point2(center.X - half + head, center.Y + head))
                        .LineTo(new Point2(center.X - half, center.Y))
                        .LineTo(new Point2(center.X - half + head, center.Y - head))
                        .LineTo(new Point2(center.X - half + head, center.Y - shaft))
                        .Close();
                case "right":
                    return GeometryPrimitives.MirrorPath(Arrow(center, size, lengthRatio, "left"));
                default:
                    throw new ArgumentException("Направление стрелки «" + direction + "» неизвестно.",
                        nameof(direction));
            }
        }
    }
}
