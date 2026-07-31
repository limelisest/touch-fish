using System.Windows;
using System.Windows.Threading;
using TouchFish.Contracts;
using TouchFish.UI.FloatingWidgets;

namespace TouchFish.Modules.BossKey;

public sealed class FloatingWidgetManager : IDisposable
{
    private readonly IWindowService _windowService;
    private readonly WindowRuleMatcher _matcher;
    private readonly Dictionary<Guid, FloatingWidgetWindow> _widgets = [];
    private readonly Dictionary<Guid, nint> _targetHandles = [];
    private readonly Dictionary<Guid, DateTimeOffset> _focusGraceUntil = [];
    private readonly HashSet<Guid> _widgetArmed = [];
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _focusTimer;
    private IReadOnlyList<WindowRuleItemViewModel> _rules = [];
    private Func<Task>? _saveSettings;

    public FloatingWidgetManager(IWindowService windowService, WindowRuleMatcher matcher)
    {
        _windowService = windowService;
        _matcher = matcher;
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _refreshTimer.Tick += (_, _) => RefreshWidgetContent();
        _refreshTimer.Start();
        _focusTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _focusTimer.Tick += (_, _) => MonitorBoundWindows();
        _focusTimer.Start();
    }

    public void Sync(IEnumerable<WindowRuleItemViewModel> rules, Func<Task> saveSettings)
    {
        _rules = rules.ToArray();
        _saveSettings = saveSettings;
        var enabledRules = _rules.Where(rule => rule.FloatingWidgetEnabled).ToArray();
        var enabledIds = enabledRules.Select(rule => rule.Id).ToHashSet();

        foreach (var staleId in _widgets.Keys.Where(id => !enabledIds.Contains(id)).ToArray())
        {
            _widgets[staleId].Close();
            _widgets.Remove(staleId);
            _targetHandles.Remove(staleId);
            _focusGraceUntil.Remove(staleId);
            _widgetArmed.Remove(staleId);
        }

        for (var index = 0; index < enabledRules.Length; index++)
        {
            var rule = enabledRules[index];
            if (!_widgets.TryGetValue(rule.Id, out var widget))
            {
                widget = CreateWidget(rule, index);
                _widgets[rule.Id] = widget;
                var target = FindTarget(rule, allowSearch: false);
                if (target != nint.Zero && _windowService.GetForegroundWindowHandle() == target)
                {
                    _focusGraceUntil[rule.Id] = DateTimeOffset.UtcNow.AddMilliseconds(250);
                }
                else
                {
                    _widgetArmed.Add(rule.Id);
                    widget.Show();
                }
            }

            widget.EdgeSnapEnabled = rule.FloatingWidgetEdgeSnapEnabled;
            widget.UpdateContent(DisplayName(rule), null);
        }

        RefreshWidgetContent();
    }

    private FloatingWidgetWindow CreateWidget(WindowRuleItemViewModel rule, int index)
    {
        var widget = new FloatingWidgetWindow
        {
            EdgeSnapEnabled = rule.FloatingWidgetEdgeSnapEnabled
        };
        widget.PointerEntered += () =>
        {
            if (_widgetArmed.Remove(rule.Id))
            {
                ActivateRule(rule, widget);
            }
        };
        widget.PointerExited += () => _widgetArmed.Add(rule.Id);
        widget.PositionChanged += (left, top) =>
        {
            rule.FloatingWidgetLeft = left;
            rule.FloatingWidgetTop = top;
            SaveSettingsInBackground();
        };

        var workArea = SystemParameters.WorkArea;
        var defaultLeft = workArea.Right - 132;
        var defaultTop = workArea.Top + 16 + index * 48;
        if (defaultTop + 40 > workArea.Bottom)
        {
            defaultTop = workArea.Top + 16;
            defaultLeft -= 128 * (index / Math.Max(1, (int)(workArea.Height / 48)));
        }

        var target = FindTarget(rule);
        var placement = target == nint.Zero ? null : _windowService.CapturePlacement(target);
        var scale = target == nint.Zero ? 1d : _windowService.GetWindowDpi(target) / 96d;
        widget.SetInitialPosition(
            placement is null ? rule.FloatingWidgetLeft ?? defaultLeft : placement.Left / scale,
            placement is null ? rule.FloatingWidgetTop ?? defaultTop : placement.Top / scale);
        if (target != nint.Zero && _windowService.GetForegroundWindowHandle() != target && !_windowService.IsMinimized(target))
        {
            _windowService.Minimize(target);
        }

        return widget;
    }

    private void ActivateRule(WindowRuleItemViewModel rule, FloatingWidgetWindow widget)
    {
        var handle = FindTarget(rule);
        if (handle == nint.Zero)
        {
            _widgetArmed.Add(rule.Id);
            return;
        }

        widget.Hide();
        _windowService.Restore(handle);
        var scale = _windowService.GetWindowDpi(handle) / 96d;
        _windowService.MoveWindowTopLeft(
            handle,
            (int)Math.Round(widget.Left * scale),
            (int)Math.Round(widget.Top * scale));
        _focusGraceUntil[rule.Id] = DateTimeOffset.UtcNow.AddMilliseconds(900);
        _windowService.TryFocus(handle);
        System.Windows.Application.Current.Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() => _windowService.TryFocus(handle)));
    }

    private void MonitorBoundWindows()
    {
        if (_widgets.Count == 0)
        {
            return;
        }

        var foreground = _windowService.GetForegroundWindowHandle();
        foreach (var rule in _rules.Where(rule => rule.FloatingWidgetEnabled))
        {
            if (!_widgets.TryGetValue(rule.Id, out var widget))
            {
                continue;
            }

            var handle = FindTarget(rule, allowSearch: false);
            if (handle == nint.Zero)
            {
                if (!widget.IsVisible)
                {
                    widget.Show();
                    _ = RearmWidgetAsync(rule.Id, widget);
                }

                continue;
            }

            if (foreground == handle)
            {
                if (widget.IsVisible)
                {
                    widget.Hide();
                    _widgetArmed.Remove(rule.Id);
                    _focusGraceUntil[rule.Id] = DateTimeOffset.UtcNow.AddMilliseconds(250);
                }

                continue;
            }

            if (widget.IsVisible ||
                _focusGraceUntil.TryGetValue(rule.Id, out var graceUntil) && DateTimeOffset.UtcNow < graceUntil)
            {
                continue;
            }

            CollapseTargetToWidget(rule, widget, handle);
        }
    }

    private void CollapseTargetToWidget(WindowRuleItemViewModel rule, FloatingWidgetWindow widget, nint handle)
    {
        var placement = _windowService.CapturePlacement(handle);
        if (placement is not null)
        {
            var scale = _windowService.GetWindowDpi(handle) / 96d;
            rule.FloatingWidgetLeft = placement.Left / scale;
            rule.FloatingWidgetTop = placement.Top / scale;
            widget.SetInitialPosition(rule.FloatingWidgetLeft.Value, rule.FloatingWidgetTop.Value);
        }

        _windowService.Minimize(handle);
        _focusGraceUntil.Remove(rule.Id);
        _widgetArmed.Remove(rule.Id);
        widget.Show();
        _ = RearmWidgetAsync(rule.Id, widget);
        SaveSettingsInBackground();
    }

    private async Task RearmWidgetAsync(Guid ruleId, FloatingWidgetWindow widget)
    {
        await Task.Delay(250);
        if (_widgets.TryGetValue(ruleId, out var current) && ReferenceEquals(current, widget) && !widget.IsMouseOver)
        {
            _widgetArmed.Add(ruleId);
        }
    }

    private nint FindTarget(WindowRuleItemViewModel rule, bool allowSearch = true)
    {
        if (_targetHandles.TryGetValue(rule.Id, out var handle) && _windowService.IsWindow(handle))
        {
            return handle;
        }

        _targetHandles.Remove(rule.Id);
        if (!allowSearch)
        {
            return nint.Zero;
        }

        var target = _matcher.FindMatches(rule.ToModel(), _windowService.EnumerateTopLevelWindows()).FirstOrDefault();
        if (target is null)
        {
            return nint.Zero;
        }

        _targetHandles[rule.Id] = target.Handle;
        return target.Handle;
    }

    private void RefreshWidgetContent()
    {
        if (_widgets.Count == 0)
        {
            return;
        }

        var currentWindows = _windowService.EnumerateTopLevelWindows();
        foreach (var rule in _rules.Where(rule => rule.FloatingWidgetEnabled))
        {
            if (!_widgets.TryGetValue(rule.Id, out var widget))
            {
                continue;
            }

            var target = _matcher.FindMatches(rule.ToModel(), currentWindows).FirstOrDefault();
            if (target is null)
            {
                _targetHandles.Remove(rule.Id);
            }
            else
            {
                _targetHandles[rule.Id] = target.Handle;
            }

            var icon = target is null ? null : _windowService.GetWindowIconPng(target.Handle);
            widget.EdgeSnapEnabled = rule.FloatingWidgetEdgeSnapEnabled;
            widget.UpdateContent(DisplayName(rule), icon);
        }
    }

    private async void SaveSettingsInBackground()
    {
        if (_saveSettings is null)
        {
            return;
        }

        try
        {
            await _saveSettings();
        }
        catch
        {
            // The next explicit save will retry persisted widget coordinates.
        }
    }

    private static string DisplayName(WindowRuleItemViewModel rule) =>
        string.IsNullOrWhiteSpace(rule.Name) ? rule.ProcessName : rule.Name;

    public void Dispose()
    {
        _refreshTimer.Stop();
        _focusTimer.Stop();
        foreach (var widget in _widgets.Values)
        {
            widget.Close();
        }

        _widgets.Clear();
        _targetHandles.Clear();
        _focusGraceUntil.Clear();
        _widgetArmed.Clear();
    }
}
