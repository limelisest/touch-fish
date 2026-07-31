using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TouchFish.Contracts;

namespace TouchFish.Modules.BossKey;

public partial class BossKeyViewModel : ObservableObject
{
    private const string HotkeyOwner = "boss-key.default";
    private readonly IWindowService _windowService;
    private readonly IHotkeyService _hotkeyService;
    private readonly IWindowPickerService _windowPickerService;
    private readonly IBossKeySettingsStore _settingsStore;
    private readonly WindowRuleMatcher _matcher;
    private readonly List<RestoreEntry> _placements = [];
    private HotkeyGesture _hotkey = new(0x4D, HotkeyModifiers.Control | HotkeyModifiers.Alt, "M");
    private bool _hotkeyAttached;
    private bool _windowsMinimized;

    public BossKeyViewModel(
        IWindowService windowService,
        IHotkeyService hotkeyService,
        IWindowPickerService windowPickerService,
        IBossKeySettingsStore settingsStore,
        WindowRuleMatcher matcher)
    {
        _windowService = windowService;
        _hotkeyService = hotkeyService;
        _windowPickerService = windowPickerService;
        _settingsStore = settingsStore;
        _matcher = matcher;
    }

    public ObservableCollection<WindowRuleItemViewModel> Windows { get; } = [];

    [ObservableProperty] private WindowRuleItemViewModel? _selectedWindow;
    [ObservableProperty] private string _hotkeyText = "Ctrl + Alt + M";
    [ObservableProperty] private string _statusText = "正在初始化……";
    [ObservableProperty] private string _pickerHint = "点击按钮后 TouchFish 会隐藏；单击目标窗口，按 Esc 取消";

    public async Task InitializeAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        _hotkey = settings.Hotkey;
        HotkeyText = _hotkey.DisplayName;

        Windows.Clear();
        foreach (var rule in settings.Windows)
        {
            Windows.Add(WindowRuleItemViewModel.FromModel(rule));
        }

        // Do not inspect every foreign window during startup. Some protected
        // windows reject shell property access; matching runs on demand instead.
        StatusText = Windows.Count == 0
            ? "请先添加需要最小化的窗口。"
            : $"已载入 {Windows.Count} 条窗口规则；点击刷新可检查状态。";
    }

    public void AttachHotkey(nint mainWindowHandle)
    {
        _hotkeyService.Attach(mainWindowHandle);
        _hotkeyAttached = true;
        RegisterCurrentHotkey();
    }

    public async Task<bool> SetHotkeyAsync(HotkeyGesture gesture)
    {
        if (!_hotkeyAttached)
        {
            StatusText = "快捷键服务尚未初始化。";
            return false;
        }

        if (!_hotkeyService.TryRegister(HotkeyOwner, gesture, ToggleWindows, out var error))
        {
            StatusText = error ?? "快捷键注册失败。";
            return false;
        }

        _hotkey = gesture;
        HotkeyText = gesture.DisplayName;
        await SaveSettingsAsync();
        StatusText = $"快捷键已设置为 {HotkeyText}。";
        return true;
    }

    public async Task<WindowDescriptor?> PickWindowAsync()
    {
        PickerHint = "选择模式：单击目标窗口，按 Esc 取消";
        return await _windowPickerService.PickWindowAsync();
    }

    public void CancelWindowPicking()
    {
        PickerHint = "点击按钮后 TouchFish 会隐藏；单击目标窗口，按 Esc 取消";
        StatusText = "已退出窗口选择模式。";
    }

    public void ReportWindowPickingError(Exception exception)
    {
        PickerHint = "点击按钮后 TouchFish 会隐藏；单击目标窗口，按 Esc 取消";
        StatusText = $"窗口选择失败：{exception.Message}";
    }

    public async Task AddWindowAsync(WindowDescriptor? window)
    {
        PickerHint = "点击按钮后 TouchFish 会隐藏；单击目标窗口，按 Esc 取消";
        if (window is null)
        {
            StatusText = "没有选择到有效窗口。";
            return;
        }

        if (window.ProcessId == Environment.ProcessId)
        {
            StatusText = "不能把 TouchFish 自己添加为目标窗口。";
            return;
        }

        var item = new WindowRuleItemViewModel
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(window.Title) ? window.ProcessName : window.Title,
            ProcessPath = window.ProcessPath,
            ProcessName = window.ProcessName,
            WindowClass = window.ClassName,
            // Web App 有 app-id 时不依赖会变化的标题；普通窗口默认使用捕获时标题。
            TitleContains = string.IsNullOrWhiteSpace(window.BrowserAppId) ? window.Title : string.Empty,
            AppUserModelId = window.AppUserModelId,
            BrowserAppId = window.BrowserAppId,
            CurrentState = "运行中"
        };

        Windows.Add(item);
        SelectedWindow = item;
        await SaveSettingsAsync();
        StatusText = string.IsNullOrWhiteSpace(window.BrowserAppId)
            ? "窗口已添加。若标题会变化，请把“标题包含”修改为稳定关键词。"
            : $"窗口已添加，并检测到浏览器 Web App 标识：{window.BrowserAppId}";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await SaveSettingsAsync();
        RefreshWindowStates();
        StatusText = "配置已保存。";
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedWindow is null)
        {
            StatusText = "请先选择要删除的窗口规则。";
            return;
        }

        var name = SelectedWindow.Name;
        Windows.Remove(SelectedWindow);
        SelectedWindow = null;
        await SaveSettingsAsync();
        StatusText = $"已删除“{name}”。";
    }

    [RelayCommand]
    private async Task MoveSelectedUpAsync()
    {
        if (SelectedWindow is null)
        {
            StatusText = "请先选择一条窗口规则。";
            return;
        }

        var index = Windows.IndexOf(SelectedWindow);
        if (index <= 0)
        {
            StatusText = "该窗口已经在最上面。";
            return;
        }

        Windows.Move(index, index - 1);
        await SaveSettingsAsync();
        StatusText = $"已上移“{SelectedWindow.Name}”；它会更晚恢复并显示在更上层。";
    }

    [RelayCommand]
    private async Task MoveSelectedDownAsync()
    {
        if (SelectedWindow is null)
        {
            StatusText = "请先选择一条窗口规则。";
            return;
        }

        var index = Windows.IndexOf(SelectedWindow);
        if (index < 0 || index >= Windows.Count - 1)
        {
            StatusText = "该窗口已经在最下面。";
            return;
        }

        Windows.Move(index, index + 1);
        await SaveSettingsAsync();
        StatusText = $"已下移“{SelectedWindow.Name}”；它会更早恢复并显示在更下层。";
    }

    [RelayCommand]
    private void LocateSelected()
    {
        if (SelectedWindow is null)
        {
            StatusText = "请先选择一条窗口规则。";
            return;
        }

        var match = _matcher.FindMatches(
                SelectedWindow.ToModel(),
                _windowService.EnumerateTopLevelWindows())
            .FirstOrDefault();

        if (match is null)
        {
            SelectedWindow.CurrentState = "未找到";
            StatusText = "当前没有找到符合该规则的窗口。";
            return;
        }

        SelectedWindow.CurrentState = "运行中";
        StatusText = _windowService.TryFocus(match.Handle)
            ? $"已定位到“{SelectedWindow.Name}”。"
            : "已找到窗口，但 Windows 阻止了前台焦点切换。";
    }

    [RelayCommand]
    private void RefreshWindowStates()
    {
        var currentWindows = _windowService.EnumerateTopLevelWindows();
        foreach (var item in Windows)
        {
            var count = _matcher.FindMatches(item.ToModel(), currentWindows).Count;
            item.CurrentState = count switch
            {
                0 => "未运行",
                1 => "运行中",
                _ => $"匹配 {count} 个"
            };
        }
    }

    private void RegisterCurrentHotkey()
    {
        if (_hotkeyService.TryRegister(HotkeyOwner, _hotkey, ToggleWindows, out var error))
        {
            StatusText = $"老板键已启用：{_hotkey.DisplayName}";
        }
        else
        {
            StatusText = error ?? "老板键注册失败。";
        }
    }

    private void ToggleWindows()
    {
        if (_windowsMinimized)
        {
            var restored = 0;
            nint foregroundWindow = nint.Zero;

            // Restore from the bottom of the list to the top. The first item is
            // therefore restored last and receives focus, matching its visual priority.
            foreach (var entry in _placements.OrderByDescending(entry => entry.Priority))
            {
                if (_windowService.Restore(entry.Placement))
                {
                    restored++;
                    foregroundWindow = entry.Placement.Handle;
                }
            }

            if (foregroundWindow != nint.Zero)
            {
                _windowService.TryFocus(foregroundWindow);
            }

            _placements.Clear();
            _windowsMinimized = false;
            StatusText = $"已按列表层级恢复 {restored} 个窗口。";
            RefreshWindowStates();
            return;
        }

        var currentWindows = _windowService.EnumerateTopLevelWindows();
        var targets = Windows
            .Select((item, priority) => new { Item = item, Priority = priority })
            .SelectMany(
                entry => _matcher.FindMatches(entry.Item.ToModel(), currentWindows),
                (entry, window) => new TargetWindow(window, entry.Priority))
            .Where(target => target.Window.ProcessId != Environment.ProcessId)
            .GroupBy(target => target.Window.Handle)
            .Select(group => group.OrderBy(target => target.Priority).First())
            .OrderBy(target => target.Priority)
            .ToArray();

        _placements.Clear();
        foreach (var target in targets)
        {
            if (_windowService.IsMinimized(target.Window.Handle))
            {
                continue;
            }

            var placement = _windowService.CapturePlacement(target.Window.Handle);
            if (placement is not null && _windowService.Minimize(target.Window.Handle))
            {
                _placements.Add(new RestoreEntry(placement, target.Priority));
            }
        }

        _windowsMinimized = _placements.Count > 0;
        StatusText = _windowsMinimized
            ? $"已最小化 {_placements.Count} 个窗口；再次按老板键可恢复。"
            : "没有找到可最小化的目标窗口。";
        RefreshWindowStates();
    }

    private Task SaveSettingsAsync() => _settingsStore.SaveAsync(new BossKeySettings
    {
        Hotkey = _hotkey,
        Windows = Windows.Select(item => item.ToModel()).ToList()
    });

    private sealed record TargetWindow(WindowDescriptor Window, int Priority);

    private sealed record RestoreEntry(WindowPlacementSnapshot Placement, int Priority);
}
