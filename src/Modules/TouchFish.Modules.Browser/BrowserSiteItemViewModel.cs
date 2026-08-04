using CommunityToolkit.Mvvm.ComponentModel;
using TouchFish.Contracts;

namespace TouchFish.Modules.Browser;

public partial class BrowserSiteItemViewModel : ObservableObject
{
    public BrowserSiteItemViewModel(BrowserSite site)
    {
        Id = site.Id;
        _name = site.Name;
        _url = site.Url;
        _isEnabled = site.IsEnabled;
        _windowOpacity = site.WindowOpacity;
        _windowTopmost = site.WindowTopmost;
        _autoHideSeconds = site.AutoHideSeconds;
        _floatingWidgetEnabled = site.FloatingWidgetEnabled;
        _floatingWidgetTriggerMode = site.FloatingWidgetTriggerMode;
        WindowLeft = site.WindowLeft;
        WindowTop = site.WindowTop;
        WindowWidth = site.WindowWidth;
        WindowHeight = site.WindowHeight;
        FloatingWidgetLeft = site.FloatingWidgetLeft;
        FloatingWidgetTop = site.FloatingWidgetTop;
    }

    public Guid Id { get; }
    [ObservableProperty] private string _name;
    [ObservableProperty] private string _url;
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private double _windowOpacity;
    [ObservableProperty] private bool _windowTopmost;
    [ObservableProperty] private int _autoHideSeconds;
    [ObservableProperty] private bool _floatingWidgetEnabled;
    [ObservableProperty] private FloatingWidgetTriggerMode _floatingWidgetTriggerMode;

    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
    public double? FloatingWidgetLeft { get; set; }
    public double? FloatingWidgetTop { get; set; }

    public bool IsClickTrigger
    {
        get => FloatingWidgetTriggerMode == FloatingWidgetTriggerMode.Click;
        set { if (value) FloatingWidgetTriggerMode = FloatingWidgetTriggerMode.Click; }
    }

    public bool IsPointerHoverTrigger
    {
        get => FloatingWidgetTriggerMode == FloatingWidgetTriggerMode.PointerHover;
        set { if (value) FloatingWidgetTriggerMode = FloatingWidgetTriggerMode.PointerHover; }
    }

    partial void OnNameChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
    }

    partial void OnWindowOpacityChanged(double value)
    {
        var clamped = Math.Clamp(value, 0.25, 1);
        if (Math.Abs(value - clamped) > 0.001) WindowOpacity = clamped;
    }

    partial void OnAutoHideSecondsChanged(int value)
    {
        if (value < 0) AutoHideSeconds = 0;
    }

    partial void OnFloatingWidgetTriggerModeChanged(FloatingWidgetTriggerMode value)
    {
        OnPropertyChanged(nameof(IsClickTrigger));
        OnPropertyChanged(nameof(IsPointerHoverTrigger));
    }

    public BrowserSite ToModel() => new()
    {
        Id = Id,
        Name = string.IsNullOrWhiteSpace(Name) ? "网页" : Name.Trim(),
        Url = Url.Trim(),
        IsEnabled = IsEnabled,
        WindowOpacity = Math.Clamp(WindowOpacity, 0.25, 1),
        WindowTopmost = WindowTopmost,
        AutoHideSeconds = Math.Max(0, AutoHideSeconds),
        FloatingWidgetEnabled = FloatingWidgetEnabled,
        FloatingWidgetTriggerMode = FloatingWidgetTriggerMode,
        WindowLeft = WindowLeft,
        WindowTop = WindowTop,
        WindowWidth = WindowWidth,
        WindowHeight = WindowHeight,
        FloatingWidgetLeft = FloatingWidgetLeft,
        FloatingWidgetTop = FloatingWidgetTop
    };
}
