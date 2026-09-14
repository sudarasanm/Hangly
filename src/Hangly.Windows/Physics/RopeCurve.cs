using System;
using System.Collections.Generic;

namespace Hangly.Windows.Physics;

/// <summary>
/// The cord as a curve that can be walked by distance. Ported from
/// <c>RopeCurve.swift</c>.
///
/// The rope is drawn as a quadratic spline through the node midpoints rather than as
/// a polyline, so anything that has to sit *on* the cord — a bead — must be placed on
/// that same curve, not on the underlying chain. This builds the spline, flattens it
/// once, and answers three questions: where is the point this far along, how far
/// along is the point nearest here, and what does the curve look like up to a cut.
/// </summary>
public sealed class RopeCurve
{
    private readonly List<Vec2> _samples = new();
    private readonly List<double> _cumulative = new();

    public double Length => _cumulative.Count > 0 ? _cumulative[^1] : 0;
    public bool IsEmpty => _samples.Count < 2;

    /// <summary>
    /// Re-measures the curve in place.
    /// </summary>
    /// <param name="points">Node positions, anchor first.</param>
    /// <param name="end">Where the cord stops (the knot on the charm, not its centre).</param>
    /// <param name="samplesPerSegment">Flattening density.</param>
    public void Rebuild(IReadOnlyList<Vec2> points, Vec2 end, int samplesPerSegment = 4)
    {
        _samples.Clear();
        _cumulative.Clear();
        if (points.Count < 2) return;

        var first = points[0];
        _samples.Add(first);
        if (points.Count > 2)
        {
            var start = first;
            for (var index = 1; index < points.Count - 1; index++)
            {
                var control = points[index];
                var finish = (points[index] + points[index + 1]) * 0.5;
                for (var step = 1; step <= samplesPerSegment; step++)
                {
                    var fraction = (double)step / samplesPerSegment;
                    _samples.Add(Quadratic(start, control, finish, fraction));
                }
                start = finish;
            }
        }
        _samples.Add(end);

        _cumulative.Add(0);
        var total = 0.0;
        for (var index = 1; index < _samples.Count; index++)
        {
            total += _samples[index].DistanceTo(_samples[index - 1]);
            _cumulative.Add(total);
        }
    }

    private static Vec2 Quadratic(Vec2 start, Vec2 control, Vec2 end, double fraction)
    {
        var inverse = 1 - fraction;
        var toStart = start * (inverse * inverse);
        var toControl = control * (2 * inverse * fraction);
        var toEnd = end * (fraction * fraction);
        return toStart + toControl + toEnd;
    }

    /// <summary>The flattened curve up to <paramref name="arc"/>, as a polyline that can be stroked.</summary>
    public List<Vec2> PolylineUpTo(double arc)
    {
        var result = new List<Vec2>();
        if (IsEmpty) return result;

        var cut = arc.Clamped(0, Length);
        result.Capacity = _samples.Count;
        for (var index = 0; index < _samples.Count; index++)
        {
            if (_cumulative[index] >= cut) break;
            result.Add(_samples[index]);
        }
        result.Add(PointAtArc(cut));
        return result;
    }

    /// <summary>Where the curve last crosses into a circle of <paramref name="radius"/> around <paramref name="center"/>.</summary>
    public double ArcEnteringCircle(Vec2 center, double radius)
    {
        if (IsEmpty) return 0;
        if (radius <= 0) return Length;

        var index = _samples.Count - 1;
        while (index > 0)
        {
            var outer = _samples[index - 1].DistanceTo(center);
            if (outer < radius)
            {
                index -= 1;
                continue;
            }
            var inner = _samples[index].DistanceTo(center);
            var span = outer - inner;
            var fraction = span > Vec2.Ulp ? ((outer - radius) / span).Clamped(0, 1) : 0;
            return _cumulative[index - 1] + ((_cumulative[index] - _cumulative[index - 1]) * fraction);
        }
        return 0;
    }

    /// <summary>The point this far along the cord, clamped to its ends.</summary>
    public Vec2 PointAtArc(double arc)
    {
        if (IsEmpty) return Vec2.Zero;
        var target = arc.Clamped(0, Length);
        var index = SegmentIndex(target);
        var spanStart = _cumulative[index];
        var spanLength = _cumulative[index + 1] - spanStart;
        if (spanLength <= Vec2.Ulp) return _samples[index];
        var fraction = (target - spanStart) / spanLength;
        return _samples[index] + ((_samples[index + 1] - _samples[index]) * fraction);
    }

    /// <summary>Direction of travel along the cord at this distance, in radians.</summary>
    public double AngleAtArc(double arc)
    {
        if (IsEmpty) return Math.PI / 2;
        var index = SegmentIndex(arc.Clamped(0, Length));
        var delta = _samples[index + 1] - _samples[index];
        if (delta.MagnitudeSquared <= Vec2.Ulp) return Math.PI / 2;
        return Math.Atan2(delta.Y, delta.X);
    }

    /// <summary>
    /// Distance along the cord of the point closest to <paramref name="location"/>.
    /// Only the cord within <paramref name="window"/> of <paramref name="near"/> is
    /// searched, so a fast swing can't jump the bead across a fold.
    /// </summary>
    public double ArcNearestTo(Vec2 location, double near, double window)
    {
        if (IsEmpty) return 0;
        var lower = (near - window).Clamped(0, Length);
        var upper = (near + window).Clamped(0, Length);

        var best = near;
        var bestDistance = double.PositiveInfinity;
        for (var index = 0; index < _samples.Count - 1; index++)
        {
            if (_cumulative[index + 1] < lower || _cumulative[index] > upper) continue;
            var (arc, distance) = ClosestPointOnSegment(index, location);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = arc;
            }
        }
        return best.Clamped(lower, upper);
    }

    private (double arc, double distance) ClosestPointOnSegment(int index, Vec2 location)
    {
        var start = _samples[index];
        var end = _samples[index + 1];
        var span = end - start;
        var lengthSquared = span.MagnitudeSquared;
        if (lengthSquared <= Vec2.Ulp)
        {
            return (_cumulative[index], start.DistanceTo(location));
        }
        var offset = location - start;
        var fraction = (((offset.X * span.X) + (offset.Y * span.Y)) / lengthSquared).Clamped(0, 1);
        var projected = start + (span * fraction);
        var arc = _cumulative[index] + ((_cumulative[index + 1] - _cumulative[index]) * fraction);
        return (arc, projected.DistanceTo(location));
    }

    /// <summary>Index of the sample segment containing <paramref name="arc"/>, by binary search.</summary>
    private int SegmentIndex(double arc)
    {
        var low = 0;
        var high = _cumulative.Count - 1;
        while (low < high - 1)
        {
            var middle = (low + high) / 2;
            if (_cumulative[middle] <= arc) low = middle;
            else high = middle;
        }
        return Math.Min(low, _samples.Count - 2);
    }
}
