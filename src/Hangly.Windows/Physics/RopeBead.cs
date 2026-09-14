namespace Hangly.Windows.Physics;

/// <summary>
/// A bead being simulated. Ported from <c>RopeBead.swift</c>.
///
/// A bead is a Verlet particle exactly like a rope node — it carries its position
/// and its previous position, and gravity and damping act on it the same way — with
/// one extra constraint: it lives on the cord. Each step the particle is integrated
/// freely, then projected back onto the curve, which is what makes it lag behind a
/// whipping rope and catch up afterwards instead of being glued to a fixed point.
/// </summary>
public struct RopeBead
{
    public Vec2 Position;
    public Vec2 PreviousPosition;

    /// <summary>Distance along the cord, from the anchor.</summary>
    public double Arc;

    /// <summary>Where the bead rests, measured back from the knot.</summary>
    public double RestOffset;

    /// <summary>Half the bead's extent along the cord, in points.</summary>
    public double SpacingRadius;

    public SizeD Size;
    public double Mass;

    /// <summary>Orientation of the cord where the bead sits, in radians.</summary>
    public double Angle;

    public readonly Vec2 Displacement => Position - PreviousPosition;

    /// <summary>How far the bead may travel from its rest place, in points.</summary>
    public readonly double SlideLimit => SpacingRadius * 0.6;
}
