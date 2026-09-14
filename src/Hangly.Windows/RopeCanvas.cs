using System;
using System.Windows;
using System.Windows.Media;
using Hangly.Windows.Physics;

namespace Hangly.Windows;

/// <summary>
/// Draws the rope, its beads and the charm. The MVP-equivalent of Hangly's
/// SwiftUI <c>RopeCanvasView</c> — plain strokes and filled ellipses, no filters, so
/// it stays cheap to redraw at the display's own rate (see the original's
/// "no filters anywhere in the render path" performance note).
/// </summary>
public sealed class RopeCanvas : FrameworkElement
{
    private RopeSimulation? _simulation;

    private static readonly Pen CordPen = MakeCordPen();
    private static readonly Brush BeadBrush = MakeFrozenBrush(210, 180, 140);
    private static readonly Brush CharmBrush = MakeFrozenBrush(200, 60, 60);

    private static Pen MakeCordPen()
    {
        var pen = new Pen(MakeFrozenBrush(90, 74, 58), 2);
        pen.Freeze();
        return pen;
    }

    private static Brush MakeFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    /// <summary>Called once per tick after the simulation has stepped.</summary>
    public void Render(RopeSimulation simulation)
    {
        _simulation = simulation;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        // A transparent-but-painted background keeps the whole element hit-testable,
        // not just the pixels the rope happens to draw over.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));

        var sim = _simulation;
        if (sim is null || sim.Points.Count < 2) return;

        DrawCord(dc, sim);
        DrawBeads(dc, sim);
        DrawCharm(dc, sim);
    }

    private static void DrawCord(DrawingContext dc, RopeSimulation sim)
    {
        var polyline = sim.Curve.PolylineUpTo(sim.CordLength);
        if (polyline.Count < 2) return;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(ToPoint(polyline[0]), false, false);
            for (var i = 1; i < polyline.Count; i++)
            {
                ctx.LineTo(ToPoint(polyline[i]), true, false);
            }
        }
        geometry.Freeze();
        dc.DrawGeometry(null, CordPen, geometry);
    }

    private static void DrawBeads(DrawingContext dc, RopeSimulation sim)
    {
        foreach (var bead in sim.Beads)
        {
            var radius = Math.Max(bead.Size.Width, bead.Size.Height) / 2;
            dc.DrawEllipse(BeadBrush, null, ToPoint(bead.Position), radius, radius);
        }
    }

    private static void DrawCharm(DrawingContext dc, RopeSimulation sim)
    {
        dc.DrawEllipse(CharmBrush, null, ToPoint(sim.CharmCenter), sim.CharmRadius, sim.CharmRadius);
    }

    private static Point ToPoint(Vec2 v) => new(v.X, v.Y);
}
