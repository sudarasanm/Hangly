using System.Windows;

namespace Hangly.Windows;

/// <summary>
/// Entry point. Hangly has no main window in the conventional sense — it owns one
/// transparent overlay window and a tray icon, and shuts down only when the tray
/// menu's Exit is chosen, mirroring the macOS app having no Dock icon.
/// </summary>
public partial class App : Application
{
    private OverlayWindow? _overlay;
    private TrayIconManager? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _overlay = new OverlayWindow();
        _overlay.Show();

        _tray = new TrayIconManager(
            onResetRope: () => _overlay.ResetRope(),
            onExit: Shutdown);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
