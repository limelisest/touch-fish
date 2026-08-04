using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TouchFish.Contracts;

namespace TouchFish.UI.FloatingWidgets;

public sealed class FloatingWidgetWindow : Window
{
    private const uint MonitorDefaultToNearest = 2;
    private static readonly object InstancesLock = new();
    private static readonly List<WeakReference<FloatingWidgetWindow>> Instances = [];
    private readonly Image _icon;
    private readonly TextBlock _title;
    private readonly DispatcherTimer _hoverTimer;
    private NativePoint _dragStartCursor;
    private NativePoint _lastCursor;
    private bool _dragging;
    private bool _pointerPressActive;

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

        lock (InstancesLock)
        {
            Instances.Add(new WeakReference<FloatingWidgetWindow>(this));
        }

        _hoverTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = FloatingWidgetActivationPolicy.HoverActivationDelay
        };
        _hoverTimer.Tick += OnHoverTimerTick;

        SourceInitialized += (_, _) => FloatingWindowStyles.HideFromAltTab(this);
        PreviewMouseLeftButtonDown += OnMouseLeftButtonDown;
        PreviewMouseMove += OnMouseMove;
        PreviewMouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;
        Closed += (_, _) =>
        {
            _hoverTimer.Stop();
            UnregisterInstance(this);
        };
    }

    public FloatingWidgetTriggerMode TriggerMode { get; set; }
    public bool EdgeSnapEnabled { get; set; } = true;

    public event Action? PointerEntered;
    public event Action? PointerExited;
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
        // Pressing during the hover delay means the user intends to move the widget.
        _hoverTimer.Stop();
        if (!NativeMethods.GetCursorPos(out _dragStartCursor))
        {
            return;
        }

        _lastCursor = _dragStartCursor;
        _dragging = false;
        _pointerPressActive = true;
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (Mouse.Captured != this || e.LeftButton != MouseButtonState.Pressed ||
            !NativeMethods.GetCursorPos(out var currentCursor))
        {
            return;
        }

        if (!_dragging)
        {
            var totalX = currentCursor.X - _dragStartCursor.X;
            var totalY = currentCursor.Y - _dragStartCursor.Y;
            if (Math.Abs(totalX) < 4 && Math.Abs(totalY) < 4)
            {
                return;
            }

            _dragging = true;
        }

        var delta = PixelsToDips(currentCursor.X - _lastCursor.X, currentCursor.Y - _lastCursor.Y);
        SetInitialPosition(Left + delta.X, Top + delta.Y);
        _lastCursor = currentCursor;
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
            if (EdgeSnapEnabled)
            {
                SnapToPeerWidgets();
                SnapToMonitorEdge();
            }

            PositionChanged?.Invoke(Left, Top);
        }
        else if (TriggerMode == FloatingWidgetTriggerMode.Click)
        {
            ActivationRequested?.Invoke();
        }

        _dragging = false;
        _pointerPressActive = false;
        e.Handled = true;
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        PointerEntered?.Invoke();
        if (_pointerPressActive || TriggerMode != FloatingWidgetTriggerMode.PointerHover)
        {
            return;
        }

        _hoverTimer.Stop();
        _hoverTimer.Start();
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        _hoverTimer.Stop();
        PointerExited?.Invoke();
    }

    private void OnHoverTimerTick(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();
        if (!_pointerPressActive && TriggerMode == FloatingWidgetTriggerMode.PointerHover && IsMouseOver)
        {
            ActivationRequested?.Invoke();
        }
    }

    private void SnapToPeerWidgets()
    {
        var peers = GetVisiblePeerBounds(this);
        if (peers.Count == 0)
        {
            return;
        }

        var snapped = FloatingWidgetSnapPolicy.SnapToPeers(
            new FloatingWidgetBounds(Left, Top, ActualWidth > 0 ? ActualWidth : Width, ActualHeight > 0 ? ActualHeight : Height),
            peers);
        SetInitialPosition(snapped.Left, snapped.Top);
    }

    private static IReadOnlyList<FloatingWidgetBounds> GetVisiblePeerBounds(FloatingWidgetWindow current)
    {
        var peers = new List<FloatingWidgetBounds>();
        lock (InstancesLock)
        {
            for (var index = Instances.Count - 1; index >= 0; index--)
            {
                if (!Instances[index].TryGetTarget(out var window))
                {
                    Instances.RemoveAt(index);
                    continue;
                }

                if (!ReferenceEquals(window, current) && window.IsVisible)
                {
                    peers.Add(new FloatingWidgetBounds(
                        window.Left,
                        window.Top,
                        window.ActualWidth > 0 ? window.ActualWidth : window.Width,
                        window.ActualHeight > 0 ? window.ActualHeight : window.Height));
                }
            }
        }

        return peers;
    }

    private static void UnregisterInstance(FloatingWidgetWindow window)
    {
        lock (InstancesLock)
        {
            for (var index = Instances.Count - 1; index >= 0; index--)
            {
                if (!Instances[index].TryGetTarget(out var candidate) || ReferenceEquals(candidate, window))
                {
                    Instances.RemoveAt(index);
                }
            }
        }
    }

    private void SnapToMonitorEdge()
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle == nint.Zero || !NativeMethods.GetWindowRect(windowHandle, out var windowRect))
        {
            return;
        }

        var monitor = NativeMethods.MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
        var monitorInfo = MonitorInfo.Create();
        if (monitor == nint.Zero || !NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        var workOriginDelta = PixelsToDips(
            monitorInfo.WorkArea.Left - windowRect.Left,
            monitorInfo.WorkArea.Top - windowRect.Top);
        var workSize = PixelsToDips(
            monitorInfo.WorkArea.Right - monitorInfo.WorkArea.Left,
            monitorInfo.WorkArea.Bottom - monitorInfo.WorkArea.Top);
        var workLeft = Left + workOriginDelta.X;
        var workTop = Top + workOriginDelta.Y;
        var workRight = workLeft + workSize.X;
        var workBottom = workTop + workSize.Y;
        const double snapDistance = 16;

        var snappedLeft = Left;
        var snappedTop = Top;
        if (Math.Abs(Left - workLeft) <= snapDistance)
        {
            snappedLeft = workLeft;
        }
        else if (Math.Abs(workRight - (Left + Width)) <= snapDistance)
        {
            snappedLeft = workRight - Width;
        }

        if (Math.Abs(Top - workTop) <= snapDistance)
        {
            snappedTop = workTop;
        }
        else if (Math.Abs(workBottom - (Top + Height)) <= snapDistance)
        {
            snappedTop = workBottom - Height;
        }

        SetInitialPosition(snappedLeft, snappedTop);
    }

    private Vector PixelsToDips(double x, double y)
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null)
        {
            return new Vector(x, y);
        }

        var converted = source.CompositionTarget.TransformFromDevice.Transform(new Point(x, y));
        return new Vector(converted.X, converted.Y);
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

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;

        public static MonitorInfo Create() => new()
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out NativePoint point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(nint windowHandle, out NativeRect rectangle);

        [DllImport("user32.dll")]
        internal static extern nint MonitorFromWindow(nint windowHandle, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo monitorInfo);
    }
}
