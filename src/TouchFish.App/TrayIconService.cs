using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace TouchFish.App;

public sealed class TrayIconService : IDisposable
{
    private readonly Window _window;
    private readonly Forms.NotifyIcon _notifyIcon;

    public TrayIconService(Window window, Action requestExit)
    {
        _window = window;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开 TouchFish", null, (_, _) => ShowWindow());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => requestExit());

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "TouchFish",
            Icon = SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowWindow();
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
    }
}
