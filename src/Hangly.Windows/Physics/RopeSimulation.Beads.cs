using System;
using System.Collections.Generic;
using System.Linq;

namespace Hangly.Windows.Physics;

/// <summary>
/// Beads threaded on the cord above the charm. Ported from
/// <c>RopeSimulation+Beads.swift</c>.
///
/// Kept apart from the solver because the dependency runs one way: the rope is
/// solved first and the beads then ride the cord it produced. Nothing here writes a
/// rope node's position, so no amount of bead behaviour can disturb the rope.
/// </summary>
public sealed partial class RopeSimulation
{
    private const double BeadTetherStiffness = 0.05;
    private const int BeadSeparationPasses = 2;

    /// <summary>Advances the beads threaded on the cord. Runs after the rope has been solved.</summary>
    public void AdvanceBeads(double timeStep)
    {
        if (Beads.Count == 0 || Curve.IsEmpty) return;

        var gravityStep = new Vec2(0, Configuration.Gravity * timeStep * timeStep);
        var damping = Configuration.Damping;
        var displacementLimit = Configuration.MaximumSpeed * timeStep;
        var drawnLength = CordLength;

        for (var index = 0; index < Beads.Count; index++)
        {
            var bead = Beads[index];
            var carried = (bead.Displacement * damping).Limited(displacementLimit);
            var predicted = bead.Position + carried + gravityStep;

            var rest = (drawnLength - bead.RestOffset).Clamped(0, drawnLength);
            // Search only the cord around where the bead already was: a global
            // search could snap it across a fold in a fast swing.
            var window = bead.SlideLimit + bead.SpacingRadius + Configuration.SegmentLength;
            var arc = Curve.ArcNearestTo(predicted, bead.Arc, window);
            arc += (rest - arc) * BeadTetherStiffness;
            bead.Arc = arc.Clamped(rest - bead.SlideLimit, rest + bead.SlideLimit);
            Beads[index] = bead;
        }

        SeparateBeads(drawnLength);

        for (var index = 0; index < Beads.Count; index++)
        {
            var bead = Beads[index];
            bead.PreviousPosition = bead.Position;
            bead.Position = Curve.PointAtArc(bead.Arc);
            bead.Angle = Curve.AngleAtArc(bead.Arc);
            Beads[index] = bead;
        }
    }

    /// <summary>
    /// Pushes touching beads apart along the cord, and keeps the lowest one clear of
    /// the charm and the highest clear of the anchor. Resolved from the charm
    /// upwards, because that end is a wall.
    /// </summary>
    public void SeparateBeads(double cordLength)
    {
        var last = Beads.Count - 1;
        if (last < 0) return;

        for (var pass = 0; pass < BeadSeparationPasses; pass++)
        {
            var lastBead = Beads[last];
            lastBead.Arc = Math.Min(lastBead.Arc, cordLength - lastBead.SpacingRadius);
            Beads[last] = lastBead;
            if (last == 0) return;

            for (var index = last - 1; index >= 0; index--)
            {
                var minimumGap = Beads[index].SpacingRadius + Beads[index + 1].SpacingRadius;
                var gap = Beads[index + 1].Arc - Beads[index].Arc;
                if (gap >= minimumGap) continue;
                var bead = Beads[index];
                bead.Arc -= minimumGap - gap;
                Beads[index] = bead;
            }

            var first = Beads[0];
            first.Arc = Math.Max(first.Arc, first.SpacingRadius);
            Beads[0] = first;
        }
    }

    /// <summary>
    /// Re-measures the cord: the curve through the chain, where the charm covers it,
    /// and which way the charm therefore hangs.
    /// </summary>
    public void RefreshCord()
    {
        if (Points.Count < 2) return;
        Curve.Rebuild(Points.Select(p => p.Position).ToList(), CharmCenter);
        CordLength = Curve.ArcEnteringCircle(CharmCenter, KnotDistance);
        CordEnd = Curve.PointAtArc(CordLength);

        var delta = CharmCenter - CordEnd;
        if (delta.MagnitudeSquared > Vec2.Ulp)
        {
            CharmOrientation = Math.Atan2(delta.Y, delta.X);
        }
    }

    /// <summary>How much of the curve the charm's artwork covers, so the cord stops there.</summary>
    public double KnotDistance => CharmRadius * CharmMetrics.KnotInset;

    /// <summary>Re-measures the beads against the charm's current radius.</summary>
    /// <param name="preservingMotion">Keep each bead where it is and let the tether carry it to its new place.</param>
    public void RebuildBeads(bool preservingMotion)
    {
        var radius = CharmRadius;
        RefreshCord();
        if (BeadDescriptions.Count == 0 || radius <= 0 || Points.Count < 2)
        {
            Beads = new List<RopeBead>();
            ApplyMasses();
            return;
        }

        var drawnLength = Curve.IsEmpty ? Configuration.TotalLength - KnotDistance : CordLength;

        var newBeads = new List<RopeBead>(BeadDescriptions.Count);
        for (var index = 0; index < BeadDescriptions.Count; index++)
        {
            var description = BeadDescriptions[index];
            var restOffset = description.Offset * radius;
            var arc = (drawnLength - restOffset).Clamped(0, Math.Max(drawnLength, 0));
            RopeBead? existing = preservingMotion && index < Beads.Count ? Beads[index] : null;
            var position = existing?.Position ?? Curve.PointAtArc(arc);

            newBeads.Add(new RopeBead
            {
                Position = position,
                PreviousPosition = existing?.PreviousPosition ?? position,
                Arc = existing?.Arc ?? arc,
                RestOffset = restOffset,
                SpacingRadius = description.SpacingRatio * radius,
                Size = description.Size * radius,
                Mass = description.Mass,
                Angle = existing?.Angle ?? Curve.AngleAtArc(arc),
            });
        }
        Beads = newBeads;
        ApplyMasses();
    }

    /// <summary>
    /// Rebuilds every node's inverse mass: the anchor pinned, the charm on the end,
    /// and each bead's weight shared between the two nodes it hangs between.
    /// </summary>
    public void ApplyMasses()
    {
        var last = Points.Count - 1;
        if (last <= 0) return;

        for (var index = 0; index <= last; index++)
        {
            double mass = index switch
            {
                0 => 0,
                _ when index == last => 1 / Math.Max(CharmMetrics.Mass, 0.0001),
                _ => 1,
            };
            SetInverseMass(mass, index);
        }

        if (Beads.Count == 0 || Configuration.SegmentLength <= Vec2.Ulp) return;

        var load = new double[Points.Count];
        foreach (var bead in Beads)
        {
            var position = (bead.Arc / Configuration.SegmentLength).Clamped(0, last);
            var lower = (int)position;
            var upper = Math.Min(lower + 1, last);
            var fraction = position - lower;
            load[lower] += bead.Mass * (1 - fraction);
            load[upper] += bead.Mass * fraction;
        }

        for (var index = 1; index <= last; index++)
        {
            if (load[index] <= 0) continue;
            var baseMass = index == last ? CharmMetrics.Mass : 1;
            SetInverseMass(1 / Math.Max(baseMass + load[index], 0.0001), index);
        }
    }
}
