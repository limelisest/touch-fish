using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Threading;
using TouchFish.Contracts;

namespace TouchFish.Modules.BossKey;

public partial class BossKeyViewModel : ObservableObject, IDisposable
{
    private const string HotkeyOwner = "boss-key.default";
    private readonly IWindowService _windowService;
    private readonly IHotkeyService _hotkeyService;
    private readonly IWindowPickerService _windowPickerService;
    private readonly IBossKeySettingsStore _settingsStore;
    private readonly WindowRuleMatcher _matcher;
    private readonly FloatingWidgetManager _floatingWidgetManager;
    private readonly IToolWindowRegistry _toolWindowRegistry;
    private readonly Dictionary<nint, AutoMinimizeState> _autoMinimizeStates = [];
    private readonly DispatcherTimer _autoMinimizeTimer;
    private DateTimeOffset _lastAutoMinimizeRefresh = DateTimeOffset.MinValue;
    private nint _lastHoveredWindowHandle;
    private WindowDescriptor? _lastHoveredWindow;
    private HotkeyGesture _hotkey = new(0x4D, HotkeyModifiers.Control | HotkeyModifiers.Alt, "M");
    private bool _hotkeyAttached;

    public BossKeyViewModel(
        IWindowService windowService,
        IHotkeyService hotkeyService,
        IWindowPickerService windowPickerService,
        IBossKeySettingsStore settingsStore,
        WindowRuleMatcher matcher,
        FloatingWidgetManager floatingWidgetManager,
        IToolWindowRegistry toolWindowRegistry)
    {
        _windowService = windowService;
        _hotkeyService = hotkeyService;
        _windowPickerService = windowPickerService;
        _settingsStore = settingsStore;
        _matcher = matcher;
        _floatingWidgetManager = floatingWidgetManager;
        _toolWindowRegistry = toolWindowRegistry;

        _autoMinimizeTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _autoMinimizeTimer.Tick += OnAutoMinimizeTick;
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
            var item = WindowRuleItemViewModel.FromModel(rule);
            AttachWindowRule(item);
            Windows.Add(item);
        }

        SyncFloatingWidgets();

        // Do not inspect every foreign window during startup. Some protected
        // windows reject shell property access; matching runs on demand instead.
        StatusText = Windows.Count == 0
            ? "请先添加需要最小化的窗口。"
            : $"已载入 {Windows.Count} 条窗口规则；点击刷新可检查状态。";
        _autoMinimizeTimer.Start();
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
            AutoMinimizeEnabled = true,
            AutoMinimizeSeconds = 60,
            CurrentState = "运行中"
        };

        AttachWindowRule(item);
        Windows.Add(item);
        SelectedWindow = item;
        await SaveSettingsAsync();
        SyncFloatingWidgets();
        StatusText = string.IsNullOrWhiteSpace(window.BrowserAppId)
            ? "窗口已添加。若标题会变化，请把“标题包含”修改为稳定关键词。"
            : $"窗口已添加，并检测到浏览器 Web App 标识：{window.BrowserAppId}";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await SaveSettingsAsync();
        SyncFloatingWidgets();
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
        DetachWindowRule(SelectedWindow);
        Windows.Remove(SelectedWindow);
        SelectedWindow = null;
        await SaveSettingsAsync();
        SyncFloatingWidgets();
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
    private async Task SyncAutoMinimizeToAllAsync()
    {
        if (SelectedWindow is null)
        {
            StatusText = "请先选择一条窗口规则。";
            return;
        }

        var enabled = SelectedWindow.AutoMinimizeEnabled;
        var seconds = Math.Clamp(SelectedWindow.AutoMinimizeSeconds, 0, 86400);
        SelectedWindow.AutoMinimizeSeconds = seconds;
        foreach (var window in Windows)
        {
            window.AutoMinimizeEnabled = enabled;
            window.AutoMinimizeSeconds = seconds;
        }

        _lastAutoMinimizeRefresh = DateTimeOffset.MinValue;
        await SaveSettingsAsync();
        StatusText = enabled
            ? $"已把光标移开自动最小化同步为启用，倒计时 {seconds} 秒。"
            : "已关闭所有窗口的光标移开自动最小化。";
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

    private void OnAutoMinimizeTick(object? sender, EventArgs e)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastAutoMinimizeRefresh >= TimeSpan.FromSeconds(5))
        {
            RefreshAutoMinimizeTargets(now);
        }

        var windowUnderCursor = _windowService.GetWindowUnderCursorHandle();
        if (windowUnderCursor != _lastHoveredWindowHandle)
        {
            _lastHoveredWindowHandle = windowUnderCursor;
            _lastHoveredWindow = windowUnderCursor == nint.Zero ? null : _windowService.InspectWindow(windowUnderCursor);
        }

        var hoveredWindow = _lastHoveredWindow;

        foreach (var (handle, state) in _autoMinimizeStates.ToArray())
        {
            if (!_windowService.IsWindow(handle))
            {
                _autoMinimizeStates.Remove(handle);
                continue;
            }

            if (_windowService.IsMinimized(handle))
            {
                state.LostFocusAt = null;
                continue;
            }

            if (windowUnderCursor != nint.Zero &&
                (_windowService.IsWindowRelated(handle, windowUnderCursor) ||
                 BelongsToRuleProcess(state.Rule, hoveredWindow)))
            {
                state.LostFocusAt = null;
                continue;
            }

            state.LostFocusAt ??= now;
            if (!AutoMinimizePolicy.ShouldMinimize(state.LostFocusAt, state.Seconds, now))
            {
                continue;
            }

            if (_windowService.Minimize(handle))
            {
                state.Rule.CurrentState = "已自动最小化";
                StatusText = $"光标离开“{state.Rule.Name}”及其衍生窗口 {state.Seconds} 秒，已自动最小化。";
            }

            state.LostFocusAt = null;
        }
    }

    private static bool BelongsToRuleProcess(WindowRuleItemViewModel rule, WindowDescriptor? window)
    {
        if (window is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.ProcessPath) && !string.IsNullOrWhiteSpace(window.ProcessPath))
        {
            return string.Equals(rule.ProcessPath, window.ProcessPath, StringComparison.OrdinalIgnoreCase);
        }

        return !string.IsNullOrWhiteSpace(rule.ProcessName) &&
               string.Equals(rule.ProcessName, window.ProcessName, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshAutoMinimizeTargets(DateTimeOffset now)
    {
        _lastAutoMinimizeRefresh = now;
        var currentWindows = _windowService.EnumerateTopLevelWindows();
        var activeHandles = new HashSet<nint>();

        foreach (var rule in Windows.Where(rule => rule.AutoMinimizeEnabled))
        {
            var seconds = Math.Clamp(rule.AutoMinimizeSeconds, 0, 86400);
            foreach (var window in _matcher.FindMatches(rule.ToModel(), currentWindows))
            {
                if (!activeHandles.Add(window.Handle))
                {
                    continue;
                }

                if (_autoMinimizeStates.TryGetValue(window.Handle, out var state))
                {
                    state.Rule = rule;
                    state.Seconds = seconds;
                }
                else
                {
                    _autoMinimizeStates[window.Handle] = new AutoMinimizeState(rule, seconds);
                }
            }
        }

        foreach (var staleHandle in _autoMinimizeStates.Keys.Where(handle => !activeHandles.Contains(handle)).ToArray())
        {
            _autoMinimizeStates.Remove(staleHandle);
        }
    }

    private void AttachWindowRule(WindowRuleItemViewModel rule)
    {
        rule.PropertyChanged += OnWindowRulePropertyChanged;
    }

    private void DetachWindowRule(WindowRuleItemViewModel rule)
    {
        rule.PropertyChanged -= OnWindowRulePropertyChanged;
    }

    private void OnWindowRulePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is
            nameof(WindowRuleItemViewModel.AutoMinimizeEnabled) or
            nameof(WindowRuleItemViewModel.AutoMinimizeSeconds))
        {
            _lastAutoMinimizeRefresh = DateTimeOffset.MinValue;
            return;
        }

        if (e.PropertyName is not (
            nameof(WindowRuleItemViewModel.FloatingWidgetEnabled) or
            nameof(WindowRuleItemViewModel.FloatingWidgetTriggerMode) or
            nameof(WindowRuleItemViewModel.FloatingWidgetEdgeSnapEnabled)))
        {
            return;
        }

        SyncFloatingWidgets();
        _ = SaveFloatingSettingsAsync();
    }

    private async Task SaveFloatingSettingsAsync()
    {
        try
        {
            await SaveSettingsAsync();
        }
        catch (Exception exception)
        {
            StatusText = $"悬浮窗配置保存失败：{exception.Message}";
        }
    }

    private void SyncFloatingWidgets()
    {
        _floatingWidgetManager.Sync(Windows, SaveSettingsAsync);
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

        var managedWindows = _toolWindowRegistry.Windows.Where(window => window.IsAvailable).ToArray();
        var minimizedStates = targets
            .Select(target => _windowService.IsMinimized(target.Window.Handle))
            .Concat(managedWindows.Select(window => window.IsMinimizedOrHidden))
            .ToArray();
        var action = BossKeyTogglePolicy.Decide(minimizedStates);
        if (action == BossKeyToggleAction.None)
        {
            StatusText = "没有找到目标窗口。";
            return;
        }

        if (action == BossKeyToggleAction.ShowAll)
        {
            var restored = 0;
            nint foregroundWindow = nint.Zero;

            foreach (var managedWindow in managedWindows)
            {
                if (managedWindow.Restore())
                {
                    restored++;
                }
            }

            // Restore bottom-to-top so the first list item is shown last.
            foreach (var target in targets.OrderByDescending(target => target.Priority))
            {
                if (_windowService.Restore(target.Window.Handle))
                {
                    restored++;
                    foregroundWindow = target.Window.Handle;
                }
            }

            if (foregroundWindow != nint.Zero)
            {
                _windowService.TryFocus(foregroundWindow);
            }

            StatusText = $"已统一显示 {restored} 个窗口。";
        }
        else
        {
            var minimized = 0;
            foreach (var target in targets)
            {
                if (_windowService.IsMinimized(target.Window.Handle) || _windowService.Minimize(target.Window.Handle))
                {
                    minimized++;
                }
            }

            foreach (var managedWindow in managedWindows)
            {
                if (managedWindow.Minimize())
                {
                    minimized++;
                }
            }

            StatusText = $"已统一最小化 {minimized} 个窗口。";
        }

        RefreshWindowStates();
    }

    private Task SaveSettingsAsync() => _settingsStore.SaveAsync(new BossKeySettings
    {
        Hotkey = _hotkey,
        Windows = Windows.Select(item => item.ToModel()).ToList()
    });

    private sealed record TargetWindow(WindowDescriptor Window, int Priority);

    public void Dispose()
    {
        foreach (var rule in Windows)
        {
            DetachWindowRule(rule);
        }

        _autoMinimizeTimer.Stop();
        _autoMinimizeTimer.Tick -= OnAutoMinimizeTick;
    }

    private sealed class AutoMinimizeState(WindowRuleItemViewModel rule, int seconds)
    {
        public WindowRuleItemViewModel Rule { get; set; } = rule;
        public int Seconds { get; set; } = seconds;
        public DateTimeOffset? LostFocusAt { get; set; }
    }
}
