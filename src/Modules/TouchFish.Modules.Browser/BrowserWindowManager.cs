using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using TouchFish.Contracts;
using TouchFish.UI.FloatingWidgets;

namespace TouchFish.Modules.Browser;

public sealed class BrowserWindowManager : IDisposable
{
    private readonly IWindowService _windowService;
    private readonly DispatcherTimer _autoHideTimer;
    private readonly Dictionary<Guid, BrowserWindow> _windows = [];
    private readonly Dictionary<Guid, FloatingWidgetWindow> _widgets = [];
    private readonly Dictionary<Guid, DateTimeOffset?> _outsideSince = [];
    private readonly Dictionary<Guid, DateTimeOffset> _widgetGraceUntil = [];
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly Lazy<Task<CoreWebView2Environment>> _environment;
    private IReadOnlyList<BrowserSiteItemViewModel> _sites = [];
    private Action? _stateChanged;
    private bool _featureEnabled = true;
    private bool _shuttingDown;

    public BrowserWindowManager(IWindowService windowService)
    {
        _windowService = windowService;
        _environment = new Lazy<Task<CoreWebView2Environment>>(() =>
            CoreWebView2Environment.CreateAsync(
                userDataFolder: Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TouchFish",
                    "WebView2")));
        _autoHideTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _autoHideTimer.Tick += OnAutoHideTick;
    }

    public event Action<string>? StatusChanged;

    public void SetFeatureEnabled(bool enabled)
    {
        _featureEnabled = enabled;
        if (!enabled)
        {
            _autoHideTimer.Stop();
            foreach (var window in _windows.Values) window.Hide();
            CloseAllWidgets();
            ClearTracking();
            return;
        }
        if (_sites.Count > 0)
        {
            _ = SyncAsync(forceShow: true);
            _autoHideTimer.Start();
        }
    }

    public void Sync(IReadOnlyList<BrowserSiteItemViewModel> sites, Action stateChanged)
    {
        _sites = sites;
        _stateChanged = stateChanged;
        _ = SyncAsync(forceShow: false);
    }

    public async Task OpenAsync(BrowserSiteItemViewModel site)
    {
        if (!_featureEnabled || !site.IsEnabled) return;
        await _syncLock.WaitAsync();
        try
        {
            await EnsureWindowAsync(site, show: true);
            SyncWidget(site);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task SyncAsync(bool forceShow)
    {
        if (_shuttingDown) return;
        await _syncLock.WaitAsync();
        try
        {
            var sites = _sites.ToArray();
            var activeIds = sites.Where(site => site.IsEnabled).Select(site => site.Id).ToHashSet();
            foreach (var id in _windows.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            {
                _windows[id].ClosePermanently();
                _windows.Remove(id);
                _outsideSince.Remove(id);
            }
            foreach (var id in _widgets.Keys.Where(id => !activeIds.Contains(id)).ToArray()) CloseWidget(id);

            if (!_featureEnabled) return;
            foreach (var site in sites.Where(site => site.IsEnabled))
            {
                await EnsureWindowAsync(site, forceShow || !_windows.ContainsKey(site.Id));
                SyncWidget(site);
            }
            _autoHideTimer.Start();
        }
        catch (Exception exception)
        {
            StatusChanged?.Invoke($"网页窗口初始化失败：{exception.Message}");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task EnsureWindowAsync(BrowserSiteItemViewModel site, bool show)
    {
        if (!_windows.TryGetValue(site.Id, out var window))
        {
            window = new BrowserWindow(site.Id);
            window.LayoutStateChanged += OnWindowStateChanged;
            window.ConfigurationChanged += () => _stateChanged?.Invoke();
            _windows[site.Id] = window;
        }
        var environment = await _environment.Value;
        var applyTask = window.ApplyAsync(site, environment);
        if (show) window.ShowAndActivate();
        await applyTask;
        if (show) window.ShowAndActivate();
    }

    private void SyncWidget(BrowserSiteItemViewModel site)
    {
        if (!_widgets.TryGetValue(site.Id, out var widget))
        {
            widget = new FloatingWidgetWindow { EdgeSnapEnabled = true };
            widget.SetInitialPosition(
                site.FloatingWidgetLeft ?? 80 + _widgets.Count * 14,
                site.FloatingWidgetTop ?? 80 + _widgets.Count * 48);
            widget.ActivationRequested += () =>
            {
                _widgetGraceUntil[site.Id] = DateTimeOffset.Now.AddSeconds(1);
                _ = OpenAsync(site);
            };
            widget.PointerEntered += () => _widgetGraceUntil[site.Id] = DateTimeOffset.Now.AddSeconds(1);
            widget.PositionChanged += (left, top) =>
            {
                site.FloatingWidgetLeft = left;
                site.FloatingWidgetTop = top;
                _stateChanged?.Invoke();
            };
            _widgets[site.Id] = widget;
            widget.Show();
        }
        widget.TriggerMode = site.FloatingWidgetTriggerMode;
        widget.EdgeSnapEnabled = true;
        widget.UpdateContent(string.IsNullOrWhiteSpace(site.Name) ? "网页" : site.Name, null);
    }

    private void OnWindowStateChanged(BrowserWindow window)
    {
        var site = _sites.FirstOrDefault(item => item.Id == window.SiteId);
        if (site is null || !window.IsLoaded) return;
        if (double.IsFinite(window.Left)) site.WindowLeft = window.Left;
        if (double.IsFinite(window.Top)) site.WindowTop = window.Top;
        if (double.IsFinite(window.ActualWidth) && window.ActualWidth > 0) site.WindowWidth = window.ActualWidth;
        if (double.IsFinite(window.ActualHeight) && window.ActualHeight > 0) site.WindowHeight = window.ActualHeight;
        _stateChanged?.Invoke();
    }

    private void OnAutoHideTick(object? sender, EventArgs e)
    {
        if (!_featureEnabled || _shuttingDown) return;
        var now = DateTimeOffset.Now;
        var cursorWindow = _windowService.GetWindowUnderCursorHandle();
        foreach (var site in _sites.Where(site => site.IsEnabled))
        {
            if (!_windows.TryGetValue(site.Id, out var window) || !window.IsVisible) continue;
            var handle = window.Handle;
            var cursorInside = _windowService.IsWindowRelated(handle, cursorWindow);
            if (cursorInside ||
                _widgetGraceUntil.TryGetValue(site.Id, out var grace) && now < grace)
            {
                _outsideSince[site.Id] = null;
                continue;
            }
            if (!_outsideSince.TryGetValue(site.Id, out var outsideSince) || outsideSince is null)
            {
                _outsideSince[site.Id] = now;
                outsideSince = now;
            }
            if (now - outsideSince.Value >= TimeSpan.FromSeconds(Math.Max(0, site.AutoHideSeconds)))
            {
                window.Hide();
                _outsideSince[site.Id] = null;
                StatusChanged?.Invoke($"已自动隐藏“{site.Name}”。");
            }
        }
    }

    private void CloseWidget(Guid id)
    {
        if (!_widgets.Remove(id, out var widget)) return;
        widget.Close();
    }

    private void CloseAllWidgets()
    {
        foreach (var widget in _widgets.Values) widget.Close();
        _widgets.Clear();
    }

    private void ClearTracking()
    {
        _outsideSince.Clear();
        _widgetGraceUntil.Clear();
    }

    public void Shutdown()
    {
        if (_shuttingDown) return;
        _shuttingDown = true;
        _autoHideTimer.Stop();
        CloseAllWidgets();
        foreach (var window in _windows.Values) window.ClosePermanently();
        _windows.Clear();
        ClearTracking();
    }

    public void Dispose()
    {
        Shutdown();
        _autoHideTimer.Tick -= OnAutoHideTick;
        _syncLock.Dispose();
    }
}
