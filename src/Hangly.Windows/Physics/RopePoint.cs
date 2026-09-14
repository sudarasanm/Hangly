using System.Collections.Generic;

namespace Hangly.Windows.Physics;

/// <summary>
/// One node of the rope. Ported from <c>RopePoint.swift</c>.
///
/// Verlet integration stores no explicit velocity. A node's velocity is implied by
/// the gap between where it is and where it was, which is why momentum survives a
/// drag release for free: releasing simply stops writing the position and the gap
/// that the drag left behind becomes the node's velocity.
/// </summary>
public struct RopePoint
{
    public Vec2 Position;
    public Vec2 PreviousPosition;
    public double InverseMass;

    public RopePoint(Vec2 position, double inverseMass = 1)
    {
        Position = position;
        PreviousPosition = position;
        InverseMass = inverseMass;
    }

    public readonly Vec2 Displacement => Position - PreviousPosition;
    public readonly bool IsPinned => InverseMass == 0;

    /// <summary>Sets the implied velocity, in points per second, for a given step length.</summary>
    public void SetVelocity(Vec2 velocity, double timeStep)
    {
        PreviousPosition = Position - (velocity * timeStep);
    }

    /// <summary>
    /// Builds a straight chain of nodes hanging from <paramref name="anchor"/> at
    /// <paramref name="angle"/> from vertical: the anchor pinned, the charm weighted,
    /// everything between free.
    /// </summary>
    public static List<RopePoint> Chain(RopeConfiguration configuration, Vec2 anchor, CharmMetrics charmMetrics, double angle)
    {
        var direction = new Vec2(0, 1).Rotated(angle);
        var lastIndex = configuration.PointCount - 1;
        var points = new List<RopePoint>(configuration.PointCount);

        for (var index = 0; index <= lastIndex; index++)
        {
            double inverseMass = index switch
            {
                0 => 0,
                _ when index == lastIndex => 1 / System.Math.Max(charmMetrics.Mass, 0.0001),
                _ => 1,
            };

            var offset = direction * (index * configuration.SegmentLength);
            points.Add(new RopePoint(anchor + offset, inverseMass));
        }

        return points;
    }
}
