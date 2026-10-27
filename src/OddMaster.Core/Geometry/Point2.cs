using System;
using System.Globalization;

namespace OddMaster.Core.Geometry
{
    /// <summary>Точка в миллиметрах (знак) или метрах (схема). Неизменяемая.</summary>
    public readonly struct Point2 : IEquatable<Point2>
    {
        /// <summary>Создаёт точку.</summary>
        public Point2(double x, double y) { X = x; Y = y; }

        /// <summary>Координата X.</summary>
        public double X { get; }
        /// <summary>Координата Y (для знака — вверх).</summary>
        public double Y { get; }

        /// <summary>Сложение с вектором.</summary>
        public static Point2 operator +(Point2 p, Vector2 v) => new Point2(p.X + v.X, p.Y + v.Y);
        /// <summary>Вычитание вектора.</summary>
        public static Point2 operator -(Point2 p, Vector2 v) => new Point2(p.X - v.X, p.Y - v.Y);
        /// <summary>Разность точек — вектор.</summary>
        public static Vector2 operator -(Point2 a, Point2 b) => new Vector2(a.X - b.X, a.Y - b.Y);

        /// <summary>Расстояние до точки.</summary>
        public double DistanceTo(Point2 other) => Math.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y));

        /// <summary>Равенство с допуском по умолчанию 1e-9.</summary>
        public bool Equals(Point2 other) => DistanceTo(other) < 1e-9;

        /// <summary>Равенство.</summary>
        public override bool Equals(object? obj) => obj is Point2 p && Equals(p);

        /// <summary>Хэш.</summary>
        public override int GetHashCode() => HashCode.Combine(X, Y);

        /// <summary>Инвариантное представление «x;y».</summary>
        public override string ToString() =>
            X.ToString("0.######", CultureInfo.InvariantCulture) + ";" + Y.ToString("0.######", CultureInfo.InvariantCulture);
    }

    /// <summary>Вектор на плоскости.</summary>
    public readonly struct Vector2
    {
        /// <summary>Создаёт вектор.</summary>
        public Vector2(double x, double y) { X = x; Y = y; }

        /// <summary>Компонента X.</summary>
        public double X { get; }
        /// <summary>Компонента Y.</summary>
        public double Y { get; }

        /// <summary>Длина.</summary>
        public double Length => Math.Sqrt(X * X + Y * Y);

        /// <summary>Единичный вектор. Нулевой вектор остаётся нулевым.</summary>
        public Vector2 Normalized()
        {
            var len = Length;
            return len < 1e-12 ? new Vector2(0, 0) : new Vector2(X / len, Y / len);
        }

        /// <summary>Правая нормаль: (dy, -dx) — «+» смещения справа по ходу (ADR-0015).</summary>
        public Vector2 RightNormal() => new Vector2(Y, -X);

        /// <summary>Умножение на число.</summary>
        public static Vector2 operator *(Vector2 v, double k) => new Vector2(v.X * k, v.Y * k);
        /// <summary>Сложение.</summary>
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.X + b.X, a.Y + b.Y);
        /// <summary>Вычитание.</summary>
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.X - b.X, a.Y - b.Y);
        /// <summary>Унарный минус.</summary>
        public static Vector2 operator -(Vector2 v) => new Vector2(-v.X, -v.Y);

        /// <summary>Скалярное произведение.</summary>
        public double Dot(Vector2 other) => X * other.X + Y * other.Y;

        /// <summary>Векторное произведение (z-компонента).</summary>
        public double Cross(Vector2 other) => X * other.Y - Y * other.X;

        /// <summary>Вектор между двумя точками.</summary>
        public static Vector2 Between(Point2 from, Point2 to) => to - from;
    }
}
