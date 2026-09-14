using System.Collections.Generic;

namespace Hangly.Windows.Physics;

/// <summary>Where one bead sits, for the renderer. Ported from <c>RopeSnapshot.swift</c>.</summary>
public readonly struct BeadPlacement
{
    public Vec2 Position { get; }
    public double Angle { get; }
    public SizeD Size { get; }

    public BeadPlacement(Vec2 position, double angle, SizeD size)
    {
        Position = position;
        Angle = angle;
        Size = size;
    }
}

/// <summary>Read-only geometry snapshot handed to the renderer each frame.</summary>
public readonly struct RopeSnapshot
{
    public IReadOnlyList<Vec2> Points { get; }
    public double CharmRadius { get; }
    public double CharmAngle { get; }
    public double CharmKnotInset { get; }
    public IReadOnlyList<BeadPlacement> Beads { get; }
    public double MaximumStretch { get; }
    public bool IsDragging { get; }

    public RopeSnapshot(
        IReadOnlyList<Vec2> points,
        double charmRadius,
        double charmAngle,
        double charmKnotInset,
        IReadOnlyList<BeadPlacement> beads,
        double maximumStretch,
        bool isDragging)
    {
        Points = points;
        CharmRadius = charmRadius;
        CharmAngle = charmAngle;
        CharmKnotInset = charmKnotInset;
        Beads = beads;
        MaximumStretch = maximumStretch;
        IsDragging = isDragging;
    }

    public Vec2 CharmCenter => Points.Count > 0 ? Points[^1] : Vec2.Zero;
}
