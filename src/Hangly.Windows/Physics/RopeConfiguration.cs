namespace Hangly.Windows.Physics;

/// <summary>
/// Every number the rope solver depends on, in one value type. Ported from
/// <c>RopeConfiguration.swift</c> — see that file for the reasoning behind each
/// constant. Units are points and seconds throughout. The canvas has its origin at
/// the top left with Y increasing downward, so gravity is a positive Y acceleration.
/// </summary>
public struct RopeConfiguration
{
    public int SegmentCount;
    public double SegmentLength;
    public double Gravity;
    public double Damping;
    public int ConstraintIterations;
    public int StretchPasses;
    public double ConvergenceTolerance;
    public double MaxStretchRatio;
    public double FixedTimeStep;
    public double MaxFrameDuration;
    public double MaximumSpeed;
    public double MaximumReachRatio;
    public double RestSpeed;
    public int FramesBeforeSleep;
    public double InitialAngle;

    public readonly int PointCount => SegmentCount + 1;
    public readonly double TotalLength => SegmentCount * SegmentLength;

    public static RopeConfiguration Default => new()
    {
        SegmentCount = 20,
        SegmentLength = 11,
        Gravity = 2000,
        Damping = 0.999,
        ConstraintIterations = 256,
        StretchPasses = 256,
        ConvergenceTolerance = 0.05,
        MaxStretchRatio = 1.02,
        FixedTimeStep = 1.0 / 240.0,
        MaxFrameDuration = 0.1,
        MaximumSpeed = 6000,
        MaximumReachRatio = 0.98,
        RestSpeed = 4.0,
        FramesBeforeSleep = 60,
        InitialAngle = 0.38,
    };

    /// <summary>Fits the rope to a canvas, keeping the shipped proportions at any scale.</summary>
    public static RopeConfiguration Fitted(SizeD size)
    {
        var configuration = Default;
        var usableLength = System.Math.Max(40, size.Height * Layout.LengthFraction);
        configuration.SegmentLength = usableLength / configuration.SegmentCount;
        return configuration;
    }

    /// <summary>Proportions shared by the solver and the renderer.</summary>
    public static class Layout
    {
        public const double LengthFraction = 0.69;
        public const double AnchorFraction = 0.045;
        public const double GrabPadding = 10.0;

        public static Vec2 Anchor(SizeD size) => new(size.Width / 2, size.Height * AnchorFraction);
    }
}
