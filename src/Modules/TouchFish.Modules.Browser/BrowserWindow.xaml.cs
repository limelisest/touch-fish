using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using TouchFish.UI.FloatingWidgets;

namespace TouchFish.Modules.Browser;

public partial class BrowserWindow : Window
{
    private bool _allowClose;
    private bool _initialized;
    private string _currentUrl = "";

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

    public async Task ApplyAsync(BrowserSiteItemViewModel site, CoreWebView2Environment environment)
    {
        SiteTitle.Text = string.IsNullOrWhiteSpace(site.Name) ? "网页" : site.Name;
        Title = SiteTitle.Text;
        Opacity = Math.Clamp(site.WindowOpacity, 0.25, 1);
        Topmost = site.WindowTopmost;
        Width = Math.Max(MinWidth, site.WindowWidth);
        Height = Math.Max(MinHeight, site.WindowHeight);
        if (site.WindowLeft is { } left && site.WindowTop is { } top)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
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
            _initialized = true;
        }

        var normalizedUrl = NormalizeUrl(site.Url);
        if (!string.Equals(normalizedUrl, _currentUrl, StringComparison.OrdinalIgnoreCase))
        {
            _currentUrl = normalizedUrl;
            Browser.CoreWebView2.Navigate(normalizedUrl);
        }
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

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || Application.Current?.Dispatcher.HasShutdownStarted == true) return;
        e.Cancel = true;
        Hide();
    }

    private void DragBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void Reload_OnClick(object sender, RoutedEventArgs e) => Browser.CoreWebView2?.Reload();
    private void Hide_OnClick(object sender, RoutedEventArgs e) => Hide();

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
