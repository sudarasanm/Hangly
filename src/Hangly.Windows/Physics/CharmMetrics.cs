namespace Hangly.Windows.Physics;

/// <summary>Ported from <c>Charm.swift</c>'s <c>CharmMetrics</c>.</summary>
public readonly struct CharmMetrics
{
    /// <summary>Mass relative to a plain rope node.</summary>
    public double Mass { get; }

    /// <summary>Bounding radius as a fraction of the rope's total length.</summary>
    public double RadiusRatio { get; }

    /// <summary>Where the cord terminates, as a fraction of the bounding radius.</summary>
    public double KnotInset { get; }

    public CharmMetrics(double mass, double radiusRatio, double knotInset = 0.9)
    {
        Mass = mass;
        RadiusRatio = radiusRatio;
        KnotInset = knotInset;
    }

    /// <summary>The shipped default, matching the plain bead.</summary>
    public static readonly CharmMetrics Default = new(mass: 2.6, radiusRatio: 0.126, knotInset: 0.90);
}

/// <summary>
/// A bead as the charm describes it, in proportions rather than points. Ported from
/// <c>RopeBead.swift</c>'s <c>CharmBead</c>.
/// </summary>
public readonly struct CharmBead
{
    public SizeD Size { get; }
    public double Offset { get; }
    public double Mass { get; }

    public CharmBead(SizeD size, double offset, double mass)
    {
        Size = size;
        Offset = offset;
        Mass = mass;
    }

    public double SpacingRatio => Size.Height / 2;
}
