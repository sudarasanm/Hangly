using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Hangly.Windows.Physics;

namespace Hangly.Windows;

/// <summary>
/// The transparent, borderless, always-on-top window the charm hangs in — the
/// Windows analogue of Hangly's <c>OverlayWindowController</c> (an <c>NSPanel</c> on
/// macOS). Drives the ported <see cref="RopeSimulation"/> from
/// <see cref="CompositionTarget.Rendering"/>, which fires once per composed frame —
/// the closest WPF equivalent to <c>CADisplayLink</c>.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly RopeSimulation _simulation = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private TimeSpan? _lastRenderingTime;
    private TimeSpan _lastMouseTime;
    private System.Windows.Point _lastMousePoint;
    private bool _isDragging;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Top-centre of the primary screen, mirroring the macOS overlay's anchor
        // near the top of the main display.
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = 0;

        _simulation.Resize(new SizeD(Width, Height));

        // Placeholder proportions standing in for a real charm's bead layout — the
        // actual catalogue lives in the SVG asset pipeline, which this port does not
        // include. See README for scope.
        _simulation.SetBeads(new System.Collections.Generic.List<CharmBead>
        {
            new(size: new SizeD(0.22, 0.22), offset: 0.55, mass: 0.15),
            new(size: new SizeD(0.18, 0.18), offset: 0.30, mass: 0.10),
        });

        _simulation.Start();
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var renderingArgs = (RenderingEventArgs)e;

        // WPF can raise Rendering more than once per composed frame with the same
        // timestamp; stepping twice on one frame would double-advance the solver.
        if (_lastRenderingTime == renderingArgs.RenderingTime) return;

        var deltaTime = _lastRenderingTime is { } previous
            ? (renderingArgs.RenderingTime - previous).TotalSeconds
            : 0;
        _lastRenderingTime = renderingArgs.RenderingTime;

        if (deltaTime > 0)
        {
            _simulation.Step(deltaTime);
        }

        Canvas.Render(_simulation);
    }

    /// <summary>Called from the tray menu's "Reset Rope".</summary>
    public void ResetRope() => _simulation.Reset();

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var location = ToVec2(e.GetPosition(Canvas));
        if (!_simulation.BeginDrag(location)) return;

        _isDragging = true;
        _lastMousePoint = e.GetPosition(Canvas);
        _lastMouseTime = _clock.Elapsed;
        Canvas.CaptureMouse();
    }

    private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging) return;

        var point = e.GetPosition(Canvas);
        var now = _clock.Elapsed;
        var dt = (now - _lastMouseTime).TotalSeconds;

        var location = ToVec2(point);
        var velocity = dt > 0
            ? (location - ToVec2(_lastMousePoint)) / dt
            : Vec2.Zero;

        _simulation.UpdateDrag(location, velocity);

        _lastMousePoint = point;
        _lastMouseTime = now;
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;

        _isDragging = false;
        _simulation.EndDrag();
        Canvas.ReleaseMouseCapture();
    }

    private static Vec2 ToVec2(System.Windows.Point p) => new(p.X, p.Y);
}
