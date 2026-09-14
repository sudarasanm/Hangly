using System;
using System.Drawing;
using System.Windows.Forms;

namespace Hangly.Windows;

/// <summary>
/// The Windows analogue of Hangly's menu-bar item: a tray icon with a small context
/// menu, since there is no menu-bar concept on Windows. Uses WinForms' NotifyIcon
/// because WPF has no first-party tray icon API.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public TrayIconManager(Action onResetRope, Action onExit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Reset Rope", null, (_, _) => onResetRope());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => onExit());

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Hangly",
            Visible = true,
            ContextMenuStrip = menu,
        };
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
