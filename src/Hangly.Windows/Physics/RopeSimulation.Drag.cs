namespace Hangly.Windows.Physics;

/// <summary>
/// Picking the charm up, moving it and letting it go. Ported from
/// <c>RopeSimulation+Drag.swift</c>. This is input handling rather than physics: it
/// decides what the solver is asked to do with the final node, and the solver
/// decides what the rope does about it.
/// </summary>
public sealed partial class RopeSimulation
{
    /// <returns>Whether the drag was accepted.</returns>
    public bool BeginDrag(Vec2 location)
    {
        if (Points.Count == 0) return false;
        if (!CanGrab(location)) return false;

        DragIndex = Points.Count - 1;
        DragTarget = location;
        DragVelocity = Vec2.Zero;
        Wake();
        return true;
    }

    /// <summary>Whether a grab at <paramref name="location"/> would hit the charm.</summary>
    public bool CanGrab(Vec2 location)
    {
        if (Points.Count == 0) return false;
        var charm = Points[^1];
        var radius = CharmRadius + RopeConfiguration.Layout.GrabPadding;
        return charm.Position.DistanceTo(location) <= radius;
    }

    /// <param name="location">Cursor location.</param>
    /// <param name="velocity">Cursor velocity in points per second.</param>
    public void UpdateDrag(Vec2 location, Vec2 velocity)
    {
        if (DragIndex == null) return;
        DragTarget = ReachableTarget(location);
        DragVelocity = velocity.Limited(Configuration.MaximumSpeed);
    }

    /// <summary>
    /// Pins the drag target to the circle the rope can actually reach, so the rope
    /// goes taut and swings around the anchor instead of stretching then snapping
    /// back on release.
    /// </summary>
    public Vec2 ReachableTarget(Vec2 location)
    {
        var reach = Configuration.TotalLength * Configuration.MaximumReachRatio;
        var offset = location - Anchor;
        var distance = offset.Magnitude;
        if (distance <= reach || distance <= Vec2.Ulp) return location;
        return Anchor + (offset / distance * reach);
    }

    /// <summary>Releases the charm. The rope carries on at the speed it was thrown.</summary>
    public void EndDrag()
    {
        DragIndex = null;
        DragVelocity = Vec2.Zero;
    }
}
