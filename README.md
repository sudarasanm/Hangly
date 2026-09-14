# Hangly for Windows (port scaffold)

An early, unverified Windows port of [Hangly](https://github.com/SharanCreatedThis/Hangly), the macOS
menu-bar charm-on-a-rope app. **This code has not been built or run** — it was
written on macOS, where the required WPF/WinForms toolchain doesn't exist. It needs
a Windows machine with the .NET 8 SDK (or Visual Studio 2022) to compile and test
for the first time.

## What this is

A 1:1 port of Hangly's Verlet rope solver (`Hangly/Physics/*.swift` in the original
repo) to C#, wired into a minimal WPF shell: a transparent, borderless, always-on-top
window near the top of the screen, a system-tray icon standing in for the menu-bar
item, and mouse-drag interaction that drives the same physics as the original.

## What's a faithful port vs. what's new

**Ported line-for-line** (see comments in each file for the Swift source it came
from):
- `Physics/Vec2.cs` — the 2D vector math (`CGPoint+Vector.swift`)
- `Physics/RopeConfiguration.cs` — every tuning constant (`RopeConfiguration.swift`)
- `Physics/RopePoint.cs`, `RopeBead.cs`, `CharmMetrics.cs` — the particle types
- `Physics/RopeCurve.cs` — the arc-length spline the beads ride on
- `Physics/RopeSimulation*.cs` — the solver, drag handling, bead pass and snapshot,
  split into partial-class files mirroring the original's Swift extension files

One deliberate numeric fix during the port: Swift's `Double.ulpOfOne` (≈2.22e-16,
machine epsilon) was ported as `Vec2.Ulp`, **not** C#'s `double.Epsilon`
(≈4.9e-324, the smallest representable double) — the two aren't the same thing, and
using the latter would have made every near-zero guard in the solver far too
permissive.

**New, not ported** (there was nothing to port from — this logic is Apple-only or
out of scope):
- `OverlayWindow.xaml(.cs)` — the transparent overlay window and its render loop.
  Ticks off `CompositionTarget.Rendering`, the closest WPF equivalent to the
  original's `CADisplayLink`-based `SimulationClock`.
- `RopeCanvas.cs` — draws the cord, beads and charm as plain strokes/ellipses (no
  filters, matching the original's performance-driven "no filters in the render
  path" rule).
- `TrayIconManager.cs` — a `NotifyIcon` context menu (Reset Rope / Exit) standing in
  for the macOS menu-bar item.

## Known gaps (by design, to keep this a reviewable first pass)

- **One hardcoded charm.** The real SVG charm catalogue, the Studio background-removal
  pipeline, and the charm library UI are not ported — that's a separate, much larger
  effort (Core Image/Vision → something like OpenCV/ML.NET). The overlay currently
  hangs a plain red circle with two placeholder beads.
- **No click-through.** The overlay window intercepts mouse input over its whole
  bounding box, not just over the drawn rope/charm the way the macOS overlay's
  precise hit-testing does. Achieving that on Windows needs a layered window with
  per-pixel hit-testing (`WS_EX_LAYERED` + a hit-test override), not attempted here.
- **No login-item / autostart registration**, no Settings window, no reduced-motion
  handling, no multi-display awareness.
- **Untested.** No Windows machine was available to build this on. Treat it as a
  from-scratch review, not a working build, until someone compiles it once.

## Building (on Windows, with .NET 8 SDK)

```powershell
dotnet build Hangly.Windows.sln
dotnet run --project src\Hangly.Windows\Hangly.Windows.csproj
```

Or open `Hangly.Windows.sln` in Visual Studio 2022 (Desktop development with .NET
workload) and press F5.

## License

Ported from Hangly, MIT-licensed. See the original repository's `LICENSE`. The
charm artwork and Hangly name/wordmark restrictions in the original README apply
here too — this port draws its own placeholder shapes, not the original SVG
artwork.
