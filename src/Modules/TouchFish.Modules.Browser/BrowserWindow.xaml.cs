using System.ComponentModel;
using System.Runtime.InteropServices;
using Color = System.Drawing.Color;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using TouchFish.UI.FloatingWidgets;

namespace TouchFish.Modules.Browser;

public partial class BrowserWindow : Window
{
    private const int WindowHitTestMessage = 0x0084;
    private const int MouseXButtonDownMessage = 0x020B;
    private const int NonClientXButtonDownMessage = 0x00AB;
    private const int AppCommandMessage = 0x0319;
    private const int XButton1Id = 1;
    private const int XButton2Id = 2;
    private const int AppCommandBrowserBackward = 1;
    private const int AppCommandBrowserForward = 2;
    private const int AppCommandBrowserHome = 7;
    private static readonly double[] OpacityOptions = [1, 0.75, 0.5, 0.25];
    private bool _allowClose;
    private bool _initialized;
    private bool _applyingSettings;
    private bool _addressPressPending;
    private int _nativeNavigationTick;
    private Point _addressPressOrigin;
    private string _currentUrl = "";
    private BrowserSiteItemViewModel? _site;
    private HwndSource? _windowSource;

    public BrowserWindow(Guid siteId)
    {
        SiteId = siteId;
        InitializeComponent();
        Browser.DefaultBackgroundColor = Color.Transparent;
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
        Closing += OnClosing;
        LocationChanged += (_, _) => LayoutStateChanged?.Invoke(this);
        SizeChanged += (_, _) => LayoutStateChanged?.Invoke(this);
    }

    public Guid SiteId { get; }
    public nint Handle => new WindowInteropHelper(this).Handle;
    public event Action<BrowserWindow>? LayoutStateChanged;
    public event Action? ConfigurationChanged;

    public async Task ApplyAsync(BrowserSiteItemViewModel site, CoreWebView2Environment environment)
    {
        _site = site;
        _applyingSettings = true;
        try
        {
            Title = string.IsNullOrWhiteSpace(site.Name) ? "网页" : site.Name;
            var opacityIndex = FindNearestOpacityIndex(site.WindowOpacity);
            var opacity = OpacityOptions[opacityIndex];
            Opacity = opacity;
            if (Math.Abs(site.WindowOpacity - opacity) > 0.001) site.WindowOpacity = opacity;
            OpacitySelector.SelectedIndex = opacityIndex;
            Topmost = site.WindowTopmost;
            Width = Math.Max(MinWidth, site.WindowWidth);
            Height = Math.Max(MinHeight, site.WindowHeight);
            if (site.WindowLeft is { } left && site.WindowTop is { } top)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = left;
                Top = top;
            }
            if (!AddressBox.IsKeyboardFocusWithin) AddressBox.Text = site.Url;
        }
        finally
        {
            _applyingSettings = false;
        }

        if (!_initialized)
        {
            await Browser.EnsureCoreWebView2Async(environment);
            Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            Browser.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                Browser.CoreWebView2.Navigate(args.Uri);
            };
            Browser.CoreWebView2.SourceChanged += (_, _) =>
            {
                UpdateAddressFromBrowser();
                UpdateNavigationButtons();
            };
            Browser.CoreWebView2.HistoryChanged += (_, _) => UpdateNavigationButtons();
            _initialized = true;
        }

        Navigate(site.Url);
        UpdateNavigationButtons();
    }

    public void ShowAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    public void ClosePermanently()
    {
        _allowClose = true;
        Browser.Dispose();
        Close();
    }

    private void Navigate(string value)
    {
        if (!_initialized) return;
        var normalizedUrl = NormalizeUrl(value);
        AddressBox.Text = normalizedUrl;
        if (string.Equals(normalizedUrl, _currentUrl, StringComparison.OrdinalIgnoreCase)) return;
        _currentUrl = normalizedUrl;
        if (_site is not null) _site.Url = normalizedUrl;
        Browser.CoreWebView2.Navigate(normalizedUrl);
        ConfigurationChanged?.Invoke();
    }

    private void UpdateAddressFromBrowser()
    {
        if (!_initialized || Browser.Source is null) return;
        var source = Browser.Source.AbsoluteUri;
        _currentUrl = source;
        if (!AddressBox.IsKeyboardFocusWithin) AddressBox.Text = source;
        if (_site is not null) _site.Url = source;
        ConfigurationChanged?.Invoke();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        FloatingWindowStyles.HideFromAltTab(this);
        _windowSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _windowSource?.AddHook(WindowMessageHook);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _windowSource?.RemoveHook(WindowMessageHook);
        _windowSource = null;
    }

    private nint WindowMessageHook(
        nint windowHandle,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (TryHandleNativeNavigation(message, wParam, lParam))
        {
            handled = true;
            return 1;
        }

        if (message != WindowHitTestMessage ||
            !NativeMethods.GetCursorPos(out var point) ||
            !NativeMethods.GetWindowRect(windowHandle, out var rect))
        {
            return nint.Zero;
        }

        const int border = 8;
        var left = point.X < rect.Left + border;
        var right = point.X >= rect.Right - border;
        var top = point.Y < rect.Top + border;
        var bottom = point.Y >= rect.Bottom - border;
        var result = (left, right, top, bottom) switch
        {
            (true, _, true, _) => 13,  // HTTOPLEFT
            (_, true, true, _) => 14,  // HTTOPRIGHT
            (true, _, _, true) => 16,  // HTBOTTOMLEFT
            (_, true, _, true) => 17,  // HTBOTTOMRIGHT
            (true, _, _, _) => 10,     // HTLEFT
            (_, true, _, _) => 11,     // HTRIGHT
            (_, _, true, _) => 12,     // HTTOP
            (_, _, _, true) => 15,     // HTBOTTOM
            _ => 0
        };
        if (result == 0)
        {
            return nint.Zero;
        }

        handled = true;
        return result;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || Application.Current?.Dispatcher.HasShutdownStarted == true) return;
        e.Cancel = true;
        Hide();
    }

    private void AddressBox_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!AddressBox.IsReadOnly) return;
        _addressPressPending = true;
        _addressPressOrigin = e.GetPosition(this);
        AddressBox.CaptureMouse();
        e.Handled = true;
    }

    private void AddressBox_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_addressPressPending || e.LeftButton != MouseButtonState.Pressed || !AddressBox.IsReadOnly) return;
        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _addressPressOrigin.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _addressPressOrigin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _addressPressPending = false;
        AddressBox.ReleaseMouseCapture();
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The mouse may have been released between the threshold check and DragMove.
        }
        e.Handled = true;
    }

    private void AddressBox_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_addressPressPending || !AddressBox.IsReadOnly) return;
        _addressPressPending = false;
        AddressBox.ReleaseMouseCapture();
        SetAddressEditing(true);
        AddressBox.Focus();
        AddressBox.SelectAll();
        e.Handled = true;
    }

    private void AddressBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            AddressBox.Text = _currentUrl;
            SetAddressEditing(false);
            Browser.Focus();
            e.Handled = true;
            return;
        }
        if (e.Key != Key.Enter) return;
        Navigate(AddressBox.Text);
        SetAddressEditing(false);
        Browser.Focus();
        e.Handled = true;
    }

    private void AddressBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_applyingSettings && !AddressBox.IsReadOnly) Navigate(AddressBox.Text);
        SetAddressEditing(false);
    }

    private void SetAddressEditing(bool editing)
    {
        AddressBox.IsReadOnly = !editing;
        AddressBox.Cursor = editing ? Cursors.IBeam : Cursors.Arrow;
    }

    private void Reload_OnClick(object sender, RoutedEventArgs e) => Browser.CoreWebView2?.Reload();
    private void Hide_OnClick(object sender, RoutedEventArgs e) => Hide();
    private void Back_OnClick(object sender, RoutedEventArgs e) => GoBack();
    private void Forward_OnClick(object sender, RoutedEventArgs e) => GoForward();
    private void Home_OnClick(object sender, RoutedEventArgs e) => GoHome();

    private void Window_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.XButton1)
        {
            ConsumeNativeNavigation(GoBack);
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.XButton2) return;
        ConsumeNativeNavigation(GoForward);
        e.Handled = true;
    }

    private bool TryHandleNativeNavigation(int message, nint wParam, nint lParam)
    {
        if (message is MouseXButtonDownMessage or NonClientXButtonDownMessage)
        {
            var button = (int)((wParam.ToInt64() >> 16) & 0xFFFF);
            if (button == XButton1Id)
            {
                ConsumeNativeNavigation(GoBack);
                return true;
            }

            if (button == XButton2Id)
            {
                ConsumeNativeNavigation(GoForward);
                return true;
            }

            return false;
        }

        if (message != AppCommandMessage) return false;
        var command = (int)((lParam.ToInt64() >> 16) & 0x0FFF);
        if (command == AppCommandBrowserBackward)
        {
            ConsumeNativeNavigation(GoBack);
            return true;
        }

        if (command == AppCommandBrowserForward)
        {
            ConsumeNativeNavigation(GoForward);
            return true;
        }

        if (command != AppCommandBrowserHome) return false;
        ConsumeNativeNavigation(GoHome);
        return true;
    }

    private void ConsumeNativeNavigation(Action navigation)
    {
        var tick = Environment.TickCount;
        if (unchecked(tick - _nativeNavigationTick) < 50) return;
        _nativeNavigationTick = tick;
        navigation();
    }

    private void GoBack()
    {
        if (Browser.CoreWebView2?.CanGoBack == true) Browser.CoreWebView2.GoBack();
    }

    private void GoForward()
    {
        if (Browser.CoreWebView2?.CanGoForward == true) Browser.CoreWebView2.GoForward();
    }

    private void GoHome()
    {
        var homeUrl = _site?.HomeUrl;
        if (string.IsNullOrWhiteSpace(homeUrl)) homeUrl = _site?.Url;
        if (string.IsNullOrWhiteSpace(homeUrl)) return;
        Navigate(homeUrl);
    }

    private void UpdateNavigationButtons()
    {
        var core = Browser.CoreWebView2;
        BackButton.IsEnabled = core?.CanGoBack == true;
        ForwardButton.IsEnabled = core?.CanGoForward == true;
    }

    private void OpacitySelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_applyingSettings || _site is null || OpacitySelector.SelectedIndex < 0) return;
        var opacity = OpacityOptions[OpacitySelector.SelectedIndex];
        Opacity = opacity;
        _site.WindowOpacity = opacity;
        ConfigurationChanged?.Invoke();
    }

    private static int FindNearestOpacityIndex(double opacity)
    {
        var bestIndex = 0;
        var bestDistance = double.MaxValue;
        for (var index = 0; index < OpacityOptions.Length; index++)
        {
            var distance = Math.Abs(OpacityOptions[index] - opacity);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            bestIndex = index;
        }
        return bestIndex;
    }

    private static string NormalizeUrl(string value)
    {
        var trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri.AbsoluteUri;
        }
        return Uri.TryCreate($"https://{trimmed}", UriKind.Absolute, out uri)
            ? uri.AbsoluteUri
            : "https://www.bing.com";
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(nint windowHandle, out Rect rect);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Point
        {
            internal int X;
            internal int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect
        {
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;
        }
    }
}
