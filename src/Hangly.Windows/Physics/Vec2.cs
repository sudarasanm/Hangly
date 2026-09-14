using System;

namespace Hangly.Windows.Physics;

/// <summary>
/// Minimal 2D vector, ported from Hangly's <c>CGPoint+Vector.swift</c>. Doubles
/// throughout (not <c>System.Numerics.Vector2</c>'s floats) to match the original
/// solver's precision exactly.
/// </summary>
public readonly struct Vec2 : IEquatable<Vec2>
{
    /// <summary>
    /// Matches Swift's <c>Double.ulpOfOne</c> (2^-52), used throughout the solver as
    /// the "effectively zero" guard. C#'s own <c>double.Epsilon</c> is the smallest
    /// representable positive double (~4.9e-324) — far too small to serve the same
    /// purpose — so the solver ports use this constant instead.
    /// </summary>
    public const double Ulp = 2.220446049250313e-16;

    public double X { get; }
    public double Y { get; }

    public static readonly Vec2 Zero = new(0, 0);

    public Vec2(double x, double y)
    {
        X = x;
        Y = y;
    }

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public static Vec2 operator /(Vec2 a, double s) => new(a.X / s, a.Y / s);

    /// <summary>Euclidean length.</summary>
    public double Magnitude => Math.Sqrt(X * X + Y * Y);

    /// <summary>Length without the square root, for comparisons.</summary>
    public double MagnitudeSquared => X * X + Y * Y;

    /// <summary>Unit vector, or zero for a zero-length vector.</summary>
    public Vec2 Normalized
    {
        get
        {
            var length = Magnitude;
            return length > Ulp ? this / length : Zero;
        }
    }

    public double DistanceTo(Vec2 other) => (other - this).Magnitude;

    /// <summary>Returns the vector rotated counter-clockwise by <paramref name="radians"/>.</summary>
    public Vec2 Rotated(double radians)
    {
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        return new Vec2(X * cosine - Y * sine, X * sine + Y * cosine);
    }

    /// <summary>Caps the vector's length at <paramref name="maximum"/>, preserving direction.</summary>
    public Vec2 Limited(double maximum)
    {
        var length = Magnitude;
        if (length <= maximum || length <= Ulp) return this;
        return this * (maximum / length);
    }

    public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
    public override bool Equals(object? obj) => obj is Vec2 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
}

public static class DoubleClampExtensions
{
    /// <summary>Ported from Swift's <c>clamped(to:)</c> over a closed range.</summary>
    public static double Clamped(this double value, double min, double max) =>
        value < min ? min : value > max ? max : value;
}

/// <summary>Ported from CoreGraphics' <c>CGSize</c>, used only as a width/height pair.</summary>
public readonly struct SizeD
{
    public double Width { get; }
    public double Height { get; }

    public SizeD(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public static SizeD operator *(SizeD s, double scalar) => new(s.Width * scalar, s.Height * scalar);
}
