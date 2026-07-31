using CommunityToolkit.Mvvm.ComponentModel;
using TouchFish.Contracts;

namespace TouchFish.Modules.BossKey;

public partial class WindowRuleItemViewModel : ObservableObject
{
    public Guid Id { get; init; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _processPath = string.Empty;
    [ObservableProperty] private string _processName = string.Empty;
    [ObservableProperty] private string _windowClass = string.Empty;
    [ObservableProperty] private string _titleContains = string.Empty;
    [ObservableProperty] private string? _appUserModelId;
    [ObservableProperty] private string? _browserAppId;
    [ObservableProperty] private bool _autoMinimizeEnabled = true;
    [ObservableProperty] private int _autoMinimizeSeconds = 60;
    [ObservableProperty] private bool _floatingWidgetEnabled;
    [ObservableProperty] private FloatingWidgetTriggerMode _floatingWidgetTriggerMode = FloatingWidgetTriggerMode.Click;
    [ObservableProperty] private double? _floatingWidgetLeft;
    [ObservableProperty] private double? _floatingWidgetTop;
    [ObservableProperty] private bool _floatingWidgetEdgeSnapEnabled = true;
    [ObservableProperty] private string _currentState = "未检查";

    public bool IsClickTrigger
    {
        get => FloatingWidgetTriggerMode == FloatingWidgetTriggerMode.Click;
        set
        {
            if (value)
            {
                FloatingWidgetTriggerMode = FloatingWidgetTriggerMode.Click;
            }
        }
    }

    public bool IsPointerHoverTrigger
    {
        get => FloatingWidgetTriggerMode == FloatingWidgetTriggerMode.PointerHover;
        set
        {
            if (value)
            {
                FloatingWidgetTriggerMode = FloatingWidgetTriggerMode.PointerHover;
            }
        }
    }

    partial void OnFloatingWidgetTriggerModeChanged(FloatingWidgetTriggerMode value)
    {
        OnPropertyChanged(nameof(IsClickTrigger));
        OnPropertyChanged(nameof(IsPointerHoverTrigger));
    }

    public static WindowRuleItemViewModel FromModel(WindowRule model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        ProcessPath = model.ProcessPath,
        ProcessName = model.ProcessName,
        WindowClass = model.WindowClass,
        TitleContains = model.TitleContains,
        AppUserModelId = model.AppUserModelId,
        BrowserAppId = model.BrowserAppId,
        AutoMinimizeEnabled = model.AutoMinimizeEnabled,
        AutoMinimizeSeconds = model.AutoMinimizeSeconds,
        FloatingWidgetEnabled = model.FloatingWidgetEnabled,
        FloatingWidgetTriggerMode = model.FloatingWidgetTriggerMode,
        FloatingWidgetLeft = model.FloatingWidgetLeft,
        FloatingWidgetTop = model.FloatingWidgetTop,
        FloatingWidgetEdgeSnapEnabled = model.FloatingWidgetEdgeSnapEnabled
    };

    public WindowRule ToModel() => new()
    {
        Id = Id,
        Name = Name.Trim(),
        ProcessPath = ProcessPath.Trim(),
        ProcessName = ProcessName.Trim(),
        WindowClass = WindowClass.Trim(),
        TitleContains = TitleContains.Trim(),
        AppUserModelId = NullIfEmpty(AppUserModelId),
        BrowserAppId = NullIfEmpty(BrowserAppId),
        AutoMinimizeEnabled = AutoMinimizeEnabled,
        AutoMinimizeSeconds = Math.Clamp(AutoMinimizeSeconds, 0, 86400),
        FloatingWidgetEnabled = FloatingWidgetEnabled,
        FloatingWidgetTriggerMode = FloatingWidgetTriggerMode,
        FloatingWidgetLeft = FloatingWidgetLeft,
        FloatingWidgetTop = FloatingWidgetTop,
        FloatingWidgetEdgeSnapEnabled = FloatingWidgetEdgeSnapEnabled
    };

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
