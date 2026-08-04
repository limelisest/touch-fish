using System.ComponentModel;
using Color = System.Drawing.Color;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using TouchFish.UI.FloatingWidgets;

namespace TouchFish.Modules.Browser;

public partial class BrowserWindow : Window
{
    private static readonly double[] OpacityOptions = [1, 0.75, 0.5, 0.25];
    private bool _allowClose;
    private bool _initialized;
    private bool _applyingSettings;
    private bool _addressPressPending;
    private Point _addressPressOrigin;
    private string _currentUrl = "";
    private string? _opacityScriptId;
    private BrowserSiteItemViewModel? _site;

    public BrowserWindow(Guid siteId)
    {
        SiteId = siteId;
        InitializeComponent();
        Browser.DefaultBackgroundColor = Color.Transparent;
        SourceInitialized += (_, _) => FloatingWindowStyles.HideFromAltTab(this);
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
        double opacity;
        try
        {
            Title = string.IsNullOrWhiteSpace(site.Name) ? "网页" : site.Name;
            var opacityIndex = FindNearestOpacityIndex(site.WindowOpacity);
            opacity = OpacityOptions[opacityIndex];
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
            Browser.CoreWebView2.SourceChanged += (_, _) => UpdateAddressFromBrowser();
            _initialized = true;
        }

        await ApplyWebOpacityAsync(opacity);
        Navigate(site.Url);
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

    private async Task ApplyWebOpacityAsync(double opacity)
    {
        if (!_initialized) return;
        var value = opacity.ToString("0.##", CultureInfo.InvariantCulture);
        var script = $$"""
            (() => {
                const applyTouchFishOpacity = () => {
                    const html = document.documentElement;
                    if (html) {
                        html.style.setProperty('opacity', '{{value}}', 'important');
                        html.style.setProperty('background-color', 'transparent', 'important');
                    }
                    if (document.body) {
                        document.body.style.setProperty('background-color', 'transparent', 'important');
                    }
                };
                if (document.readyState === 'loading') {
                    document.addEventListener('DOMContentLoaded', applyTouchFishOpacity, { once: true });
                } else {
                    applyTouchFishOpacity();
                }
            })();
            """;
        var newScriptId = await Browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
        if (_opacityScriptId is not null)
        {
            Browser.CoreWebView2.RemoveScriptToExecuteOnDocumentCreated(_opacityScriptId);
        }
        _opacityScriptId = newScriptId;
        await Browser.CoreWebView2.ExecuteScriptAsync(script);
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
        AddressBox.IsReadOnly = false;
        AddressBox.Focus();
        AddressBox.SelectAll();
        e.Handled = true;
    }

    private void AddressBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            AddressBox.Text = _currentUrl;
            Browser.Focus();
            e.Handled = true;
            return;
        }
        if (e.Key != Key.Enter) return;
        Navigate(AddressBox.Text);
        Browser.Focus();
        e.Handled = true;
    }

    private void AddressBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_applyingSettings && !AddressBox.IsReadOnly) Navigate(AddressBox.Text);
        AddressBox.IsReadOnly = true;
    }

    private void Reload_OnClick(object sender, RoutedEventArgs e) => Browser.CoreWebView2?.Reload();
    private void Hide_OnClick(object sender, RoutedEventArgs e) => Hide();

    private async void OpacitySelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_applyingSettings || _site is null || OpacitySelector.SelectedIndex < 0) return;
        var opacity = OpacityOptions[OpacitySelector.SelectedIndex];
        Opacity = opacity;
        _site.WindowOpacity = opacity;
        try
        {
            await ApplyWebOpacityAsync(opacity);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to apply web opacity: {exception}");
        }
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
}
