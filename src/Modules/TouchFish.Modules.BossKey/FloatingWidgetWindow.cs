using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TouchFish.Modules.BossKey;

internal sealed class FloatingWidgetWindow : Window
{
    private readonly Image _icon;
    private readonly TextBlock _title;
    private Point _dragStart;
    private double _windowStartLeft;
    private double _windowStartTop;
    private bool _dragging;
    private DateTimeOffset _lastHoverActivation = DateTimeOffset.MinValue;

    public FloatingWidgetWindow()
    {
        Width = 120;
        Height = 40;
        MaxWidth = 120;
        MaxHeight = 40;
        MinWidth = 72;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        Topmost = true;

        _icon = new Image
        {
            Width = 24,
            Height = 24,
            Stretch = Stretch.Uniform,
            Source = LoadFallbackIcon()
        };
        _title = new TextBlock
        {
            MaxWidth = 72,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = 12
        };
        _title.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(7, 0, 7, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(_icon);
        _title.Margin = new Thickness(7, 0, 0, 0);
        panel.Children.Add(_title);

        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Child = panel
        };
        border.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        Content = border;

        PreviewMouseLeftButtonDown += OnMouseLeftButtonDown;
        PreviewMouseMove += OnMouseMove;
        PreviewMouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseEnter += OnMouseEnter;
    }

    public FloatingWidgetTriggerMode TriggerMode { get; set; }

    public event Action? ActivationRequested;
    public event Action<double, double>? PositionChanged;

    public void UpdateContent(string title, byte[]? iconPng)
    {
        _title.Text = title;
        if (iconPng is null || iconPng.Length == 0)
        {
            return;
        }

        try
        {
            using var stream = new MemoryStream(iconPng);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            _icon.Source = image;
        }
        catch
        {
            // Keep the fallback icon if the target returned malformed icon data.
        }
    }

    public void SetInitialPosition(double left, double top)
    {
        Left = Clamp(left, SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width);
        Top = Clamp(top, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = PointToScreen(e.GetPosition(this));
        _windowStartLeft = Left;
        _windowStartTop = Top;
        _dragging = false;
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (Mouse.Captured != this || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = PointToScreen(e.GetPosition(this));
        var deltaX = current.X - _dragStart.X;
        var deltaY = current.Y - _dragStart.Y;
        if (!_dragging && Math.Abs(deltaX) < 4 && Math.Abs(deltaY) < 4)
        {
            return;
        }

        _dragging = true;
        SetInitialPosition(_windowStartLeft + deltaX, _windowStartTop + deltaY);
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (Mouse.Captured == this)
        {
            ReleaseMouseCapture();
        }

        if (_dragging)
        {
            PositionChanged?.Invoke(Left, Top);
        }
        else if (TriggerMode == FloatingWidgetTriggerMode.Click)
        {
            ActivationRequested?.Invoke();
        }

        _dragging = false;
        e.Handled = true;
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (TriggerMode != FloatingWidgetTriggerMode.PointerHover)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - _lastHoverActivation < TimeSpan.FromMilliseconds(500))
        {
            return;
        }

        _lastHoverActivation = now;
        ActivationRequested?.Invoke();
    }

    private static ImageSource? LoadFallbackIcon()
    {
        try
        {
            return new BitmapImage(new Uri("pack://application:,,,/Assets/app-icon.ico", UriKind.Absolute));
        }
        catch
        {
            return null;
        }
    }

    private static double Clamp(double value, double minimum, double maximum) =>
        maximum <= minimum ? minimum : Math.Clamp(value, minimum, maximum);
}
