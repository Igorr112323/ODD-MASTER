using System;
using System.Collections.Generic;
using OddMaster.Core.Catalog;
using OddMaster.Core.Geometry;

namespace OddMaster.Core.Generation
{
    /// <summary>Габариты носителя, извлечённые из строки таблицы прил. Д.</summary>
    internal sealed class CarrierBuilder
    {
        public CarrierShape Shape;
        public double A, B, D, H, Radius;

        /// <summary>Читает размерности из SizeSpec. B_options берёт первый вариант (Д.6).</summary>
        public static CarrierBuilder FromSpec(CarrierShape shape, SizeSpec spec, double? cornerRadius, out string? problem)
        {
            problem = null;
            var result = new CarrierBuilder { Shape = shape, Radius = cornerRadius ?? 0.0 };
            switch (shape)
            {
                case CarrierShape.TriangleUp:
                case CarrierShape.TriangleDown:
                    if (!spec.TryGet("A", out result.A)) problem = "нет размерности A";
                    break;
                case CarrierShape.Circle:
                    if (!spec.TryGet("D", out result.D)) problem = "нет размерности D";
                    break;
                case CarrierShape.Octagon:
                case CarrierShape.Square:
                case CarrierShape.Diamond:
                    if (!spec.TryGet("B", out result.B)) problem = "нет размерности B";
                    break;
                default:
                    if (!spec.TryGet("H", out result.H)) problem = "нет размерности H";
                    if (!TryReadB(spec, out result.B)) problem = "нет размерности B/B_options";
                    break;
            }
            return result;
        }

        private static bool TryReadB(SizeSpec spec, out double value)
        {
            if (spec.TryGet("B", out value)) return true;
            foreach (var options in spec.Options.Values)
                if (options.Count > 0) { value = options[0]; return true; }
            value = 0;
            return false;
        }

        /// <summary>
        /// Поворот размеров H↔B для знаков, которые по изображению вертикальные, а в таблице
        /// прил. Д описаны горизонтальными (CR-25). Гипотеза: норма — прил. Б.
        /// </summary>
        public void SwapAxes()
        {
            var t = B; B = H; H = t;
        }

        /// <summary>Номинальный габарит носителя (A/D/B/H включают наружную кайму, GOST-004).</summary>
        public BoundingBox NominalBounds() => NominalBounds(0.0);

        /// <summary>Габарит контура, смещённого внутрь на inset (для поля и каймы).</summary>
        public BoundingBox NominalBounds(double inset)
        {
            switch (Shape)
            {
                case CarrierShape.TriangleUp:
                case CarrierShape.TriangleDown:
                {
                    var side = A - 2 * Math.Sqrt(3.0) * inset;
                    return BoundingBox.Centered(side, side * Math.Sqrt(3.0) / 2);
                }
                case CarrierShape.Circle:
                    return BoundingBox.Centered(D - 2 * inset, D - 2 * inset);
                case CarrierShape.Octagon:
                case CarrierShape.Square:
                    return BoundingBox.Centered(B - 2 * inset, B - 2 * inset);
                case CarrierShape.Diamond:
                {
                    var side = B - 2 * inset;
                    return BoundingBox.Centered(side * Math.Sqrt(2.0), side * Math.Sqrt(2.0));
                }
                default:
                    return BoundingBox.Centered(B - 2 * inset, H - 2 * inset);
            }
        }

        /// <summary>Строит контур. inset — смещение краёв внутрь (для слоя Field/InnerBorder).</summary>
        public PrimitiveResult Build(double inset)
        {
            var bounds = NominalBounds(inset);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return PrimitiveResult.Fail("Кайма толще знака: внутренний габарит неположителен.");
            var r = Math.Max(Radius - inset, 0.0);
            switch (Shape)
            {
                case CarrierShape.Circle:
                    return PrimitiveResult.Ok(GeometryPrimitives.Circle(D - 2 * inset));
                case CarrierShape.Octagon:
                    return PrimitiveResult.Ok(GeometryPrimitives.RegularOctagon(B - 2 * inset));
                case CarrierShape.TriangleUp:
                case CarrierShape.TriangleDown:
                    return GeometryPrimitives.Triangle(A - 2 * Math.Sqrt(3.0) * inset, r, Shape == CarrierShape.TriangleUp);
                case CarrierShape.Square:
                    return GeometryPrimitives.RoundedRectangle(B - 2 * inset, B - 2 * inset, r);
                case CarrierShape.Diamond:
                    return GeometryPrimitives.Diamond(B - 2 * inset);
                default:
                    return GeometryPrimitives.RoundedRectangle(B - 2 * inset, H - 2 * inset, r);
            }
        }
    }
}
