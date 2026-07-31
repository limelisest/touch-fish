using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace TouchFish.App;

public sealed class TrayIconService : IDisposable
{
    private readonly Icon _icon;
    private readonly Window _window;
    private readonly Forms.NotifyIcon _notifyIcon;

    public TrayIconService(Window window, Action requestExit)
    {
        _window = window;
        _icon = LoadApplicationIcon();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开 TouchFish", null, (_, _) => ShowWindow());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => requestExit());

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "TouchFish",
            Icon = _icon,
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowWindow();
    }

    private static Icon LoadApplicationIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/app-icon.ico", UriKind.Absolute));
        if (resource?.Stream is null)
        {
            return (Icon)SystemIcons.Application.Clone();
        }

        using var source = new Icon(resource.Stream);
        return (Icon)source.Clone();
    }

    private void ShowWindow()
    {
        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon.Dispose();
    }
}
