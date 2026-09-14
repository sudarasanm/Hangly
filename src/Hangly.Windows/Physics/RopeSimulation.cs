using System;
using System.Collections.Generic;

namespace Hangly.Windows.Physics;

/// <summary>
/// A hanging rope simulated with Verlet integration and position-based constraints.
/// Ported from <c>RopeSimulation.swift</c>.
///
/// The step order is deliberate and is what makes the rope both stable and stiff:
/// 1. Pin the anchor — first, so a moved anchor drags the rope this step.
/// 2. Integrate — each free node moves by its damped displacement plus gravity.
/// 3. Drive the held node — a dragged node is written directly.
/// 4. Relax constraints — Gauss-Seidel passes pull each link back to rest length.
/// 5. Clamp stretch — a hard pass guarantees no link exceeds its limit.
///
/// Time advances in fixed slices, so the rope behaves identically regardless of the
/// caller's frame rate.
/// </summary>
public sealed partial class RopeSimulation
{
    public List<RopePoint> Points { get; private set; } = new();
    public RopeConfiguration Configuration { get; private set; }
    public Vec2 Anchor { get; private set; }
    public CharmMetrics CharmMetrics { get; private set; }

    // The following are maintained by RopeSimulation.Beads.cs, which is the only
    // file that should write them.
    public List<RopeBead> Beads = new();
    public Vec2 CordEnd;
    public double CordLength;
    public double CharmOrientation = Math.PI / 2;

    public bool IsRunning { get; private set; }
    public int LastStepCount { get; private set; }
    public bool IsSleeping { get; private set; }

    private int _stillFrames;
    private double _accumulator;

    // Written by RopeSimulation.Drag.cs, read by the solver.
    internal int? DragIndex;
    internal Vec2 DragTarget;
    internal Vec2 DragVelocity;

    /// <summary>What the current charm says its beads are, in proportions.</summary>
    public List<CharmBead> BeadDescriptions = new();

    /// <summary>The drawn cord, rebuilt each step and reused rather than reallocated.</summary>
    public readonly RopeCurve Curve = new();

    public RopeSimulation(RopeConfiguration? configuration = null, Vec2? anchor = null, CharmMetrics? charmMetrics = null)
    {
        Configuration = configuration ?? RopeConfiguration.Default;
        Anchor = anchor ?? Vec2.Zero;
        // `CharmMetrics` here would bind to the instance property of the same name,
        // not the type, so the default value needs a fully-qualified reference.
        CharmMetrics = charmMetrics ?? global::Hangly.Windows.Physics.CharmMetrics.Default;
        Reset();
    }

    /// <summary>Attaches a charm's physical properties to the final node, in place.</summary>
    public void SetCharmMetrics(CharmMetrics metrics)
    {
        if (Equals(metrics, CharmMetrics)) return;
        CharmMetrics = metrics;
        RebuildBeads(preservingMotion: true);
        Wake();
    }

    /// <summary>Attaches the beads the current charm threads onto its cord.</summary>
    public void SetBeads(List<CharmBead> descriptions)
    {
        BeadDescriptions = descriptions;
        RebuildBeads(preservingMotion: false);
        Wake();
    }

    public bool IsDragging => DragIndex != null;

    public void Start()
    {
        if (IsRunning) return;
        if (Points.Count == 0) Reset();
        _accumulator = 0;
        IsRunning = true;
        Wake();
    }

    public void Stop()
    {
        IsRunning = false;
        _accumulator = 0;
    }

    public void Step(double deltaTime)
    {
        if (!IsRunning || deltaTime <= 0 || IsSleeping)
        {
            LastStepCount = 0;
            return;
        }

        // Clamping the accumulator stops a stall or a wake from sleep turning into a
        // burst of catch-up steps, which would look like the rope teleporting.
        _accumulator = Math.Min(_accumulator + deltaTime, Configuration.MaxFrameDuration);

        var timeStep = Configuration.FixedTimeStep;
        var taken = 0;
        while (_accumulator >= timeStep)
        {
            Advance(timeStep);
            _accumulator -= timeStep;
            taken += 1;
        }
        LastStepCount = taken;
        UpdateSleepState();
    }

    /// <summary>Wakes the rope so the next <see cref="Step"/> does work again.</summary>
    public void Wake()
    {
        IsSleeping = false;
        _stillFrames = 0;
    }

    private void UpdateSleepState()
    {
        if (DragIndex != null)
        {
            _stillFrames = 0;
            return;
        }

        var speedLimit = Configuration.RestSpeed * Configuration.FixedTimeStep;
        var moving = false;
        foreach (var p in Points)
        {
            if (p.Displacement.Magnitude > speedLimit) { moving = true; break; }
        }
        if (!moving)
        {
            foreach (var b in Beads)
            {
                if (b.Displacement.Magnitude > speedLimit) { moving = true; break; }
            }
        }
        if (moving)
        {
            _stillFrames = 0;
            return;
        }

        _stillFrames += 1;
        if (_stillFrames >= Configuration.FramesBeforeSleep)
        {
            IsSleeping = true;
        }
    }

    public void Reset() => Reset(Configuration.InitialAngle);

    /// <summary>Rebuilds the rope hanging straight down with no motion.</summary>
    public void ResetToHanging() => Reset(0);

    private void Reset(double angle)
    {
        Points = RopePoint.Chain(Configuration, Anchor, CharmMetrics, angle);
        _accumulator = 0;
        DragIndex = null;
        DragVelocity = Vec2.Zero;
        LastStepCount = 0;
        RebuildBeads(preservingMotion: false);
        Wake();
    }

    /// <summary>Re-fits the rope to a new canvas without discarding its motion.</summary>
    public void Resize(SizeD canvasSize)
    {
        var fitted = RopeConfiguration.Fitted(canvasSize);
        var needsRebuild = Points.Count != fitted.PointCount;

        Configuration = fitted;
        Anchor = RopeConfiguration.Layout.Anchor(canvasSize);

        if (needsRebuild)
        {
            Reset();
        }
        else
        {
            RebuildBeads(preservingMotion: true);
            Wake();
        }
    }

    // MARK: - Solver

    private void Advance(double timeStep)
    {
        EnforceAnchor();
        Integrate(timeStep);
        DriveDraggedPoint(timeStep);

        var relaxations = 0;
        var residual = double.PositiveInfinity;
        while (relaxations < Configuration.ConstraintIterations && residual >= Configuration.ConvergenceTolerance)
        {
            residual = SolveDistanceConstraints();
            relaxations += 1;
        }

        EnforceMaximumStretch();
        RefreshCord();
        AdvanceBeads(timeStep);
    }

    private void EnforceAnchor()
    {
        if (Points.Count == 0) return;
        var p = Points[0];
        p.Position = Anchor;
        p.PreviousPosition = Anchor;
        Points[0] = p;
    }

    private void Integrate(double timeStep)
    {
        var gravityStep = new Vec2(0, Configuration.Gravity * timeStep * timeStep);
        var damping = Configuration.Damping;
        var displacementLimit = Configuration.MaximumSpeed * timeStep;

        for (var index = 0; index < Points.Count; index++)
        {
            if (index == DragIndex) continue;
            var point = Points[index];
            if (point.InverseMass <= 0) continue;

            var carried = (point.Displacement * damping).Limited(displacementLimit);
            point.PreviousPosition = point.Position;
            point.Position += carried + gravityStep;
            Points[index] = point;
        }
    }

    /// <summary>Moves the held node toward the cursor, at a finite rate.</summary>
    private void DriveDraggedPoint(double timeStep)
    {
        if (DragIndex is not int dragIndex) return;

        var point = Points[dragIndex];
        var current = point.Position;
        var travelLimit = Configuration.MaximumSpeed * timeStep;
        point.Position = current + (DragTarget - current).Limited(travelLimit);
        point.SetVelocity(DragVelocity, timeStep);
        Points[dragIndex] = point;
    }

    /// <returns>The largest correction applied, so the caller can stop early.</returns>
    private double SolveDistanceConstraints()
    {
        var restLength = Configuration.SegmentLength;
        var largestCorrection = 0.0;
        for (var index = 0; index < Points.Count - 1; index++)
        {
            var correction = SolveLink(index, index + 1, restLength);
            largestCorrection = Math.Max(largestCorrection, correction);
        }
        return largestCorrection;
    }

    /// <returns>The magnitude of the correction applied to this link.</returns>
    private double SolveLink(int indexA, int indexB, double restLength)
    {
        var inverseA = EffectiveInverseMass(indexA);
        var inverseB = EffectiveInverseMass(indexB);
        var totalInverseMass = inverseA + inverseB;
        if (totalInverseMass <= 0) return 0;

        var a = Points[indexA];
        var b = Points[indexB];
        var delta = b.Position - a.Position;
        var distance = delta.Magnitude;
        if (distance <= Vec2.Ulp) return 0;

        var correction = delta * ((distance - restLength) / distance / totalInverseMass);
        a.Position += correction * inverseA;
        b.Position -= correction * inverseB;
        Points[indexA] = a;
        Points[indexB] = b;
        return Math.Max((correction * inverseA).Magnitude, (correction * inverseB).Magnitude);
    }

    /// <summary>The hard guarantee behind "never stretches unrealistically".</summary>
    private void EnforceMaximumStretch()
    {
        var limit = Configuration.SegmentLength * Configuration.MaxStretchRatio;

        for (var pass = 0; pass < Configuration.StretchPasses; pass++)
        {
            var corrected = false;
            for (var index = 0; index < Points.Count - 1; index++)
            {
                if (ClampLink(index, limit)) corrected = true;
            }
            if (!corrected) return;
        }
    }

    /// <returns>Whether the link was over its limit.</returns>
    private bool ClampLink(int index, double limit)
    {
        var lower = index;
        var upper = index + 1;

        var inverseLower = EffectiveInverseMass(lower);
        var inverseUpper = EffectiveInverseMass(upper);
        var totalInverseMass = inverseLower + inverseUpper;
        if (totalInverseMass <= 0) return false;

        var a = Points[lower];
        var b = Points[upper];
        var delta = b.Position - a.Position;
        var distance = delta.Magnitude;
        if (distance <= limit || distance <= Vec2.Ulp) return false;

        var correction = delta * ((distance - limit) / distance / totalInverseMass);
        a.Position += correction * inverseLower;
        b.Position -= correction * inverseUpper;
        Points[lower] = a;
        Points[upper] = b;
        return true;
    }

    /// <summary>A held node is immovable for the solver, exactly like the anchor.</summary>
    private double EffectiveInverseMass(int index) => index == DragIndex ? 0 : Points[index].InverseMass;

    /// <summary>
    /// Sets one node's inverse mass. The only way anything outside the solver may
    /// touch a node, so bead loading can live beside the beads rather than here.
    /// </summary>
    public void SetInverseMass(double value, int index)
    {
        if (index < 0 || index >= Points.Count) return;
        var p = Points[index];
        p.InverseMass = value;
        Points[index] = p;
    }
}
