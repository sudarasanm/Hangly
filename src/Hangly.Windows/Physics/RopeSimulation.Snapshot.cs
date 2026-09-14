using System;
using System.Linq;

namespace Hangly.Windows.Physics;

/// <summary>
/// Read-only geometry derived from the solver's state. Ported from
/// <c>RopeSimulation+Snapshot.swift</c>. Nothing here mutates anything; every member
/// is a pure function of the node positions.
/// </summary>
public sealed partial class RopeSimulation
{
    public double CharmRadius => Configuration.TotalLength * CharmMetrics.RadiusRatio;

    /// <summary>How the charm hangs: the direction from the knot to the charm's centre.</summary>
    public double CharmAngle => CharmOrientation;

    /// <summary>Where the charm hangs, which is the last node.</summary>
    public Vec2 CharmCenter => Points.Count > 0 ? Points[^1].Position : Vec2.Zero;

    /// <summary>Longest link as a multiple of its rest length.</summary>
    public double MeasuredMaximumStretch
    {
        get
        {
            if (Configuration.SegmentLength <= Vec2.Ulp || Points.Count < 2) return 1;
            var longest = 0.0;
            for (var index = 0; index < Points.Count - 1; index++)
            {
                var distance = Points[index].Position.DistanceTo(Points[index + 1].Position);
                longest = Math.Max(longest, distance / Configuration.SegmentLength);
            }
            return longest;
        }
    }

    public RopeSnapshot Snapshot() => new(
        points: Points.Select(p => p.Position).ToList(),
        charmRadius: CharmRadius,
        charmAngle: CharmAngle,
        charmKnotInset: CharmMetrics.KnotInset,
        beads: Beads.Select(b => new BeadPlacement(b.Position, b.Angle, b.Size)).ToList(),
        maximumStretch: MeasuredMaximumStretch,
        isDragging: IsDragging);
}
