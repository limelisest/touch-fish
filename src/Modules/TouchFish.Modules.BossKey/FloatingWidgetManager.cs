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
    private readonly DispatcherTimer _refreshTimer;
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
    }

    public event Action<nint>? TargetActivatedFromWidget;

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
        }

        for (var index = 0; index < enabledRules.Length; index++)
        {
            var rule = enabledRules[index];
            if (!_widgets.TryGetValue(rule.Id, out var widget))
            {
                widget = CreateWidget(rule, index);
                _widgets[rule.Id] = widget;
                widget.Show();
            }

            widget.TriggerMode = rule.FloatingWidgetTriggerMode;
            widget.EdgeSnapEnabled = true;
            widget.UpdateContent(DisplayName(rule), null);
        }

        RefreshWidgetContent();
    }

    private FloatingWidgetWindow CreateWidget(WindowRuleItemViewModel rule, int index)
    {
        var widget = new FloatingWidgetWindow
        {
            TriggerMode = rule.FloatingWidgetTriggerMode,
            EdgeSnapEnabled = true
        };
        widget.ActivationRequested += () => ActivateRule(rule);
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

        widget.SetInitialPosition(
            rule.FloatingWidgetLeft ?? defaultLeft,
            rule.FloatingWidgetTop ?? defaultTop);
        return widget;
    }

    private void ActivateRule(WindowRuleItemViewModel rule)
    {
        if (!_targetHandles.TryGetValue(rule.Id, out var handle) || !_windowService.IsWindow(handle))
        {
            var target = _matcher.FindMatches(rule.ToModel(), _windowService.EnumerateTopLevelWindows()).FirstOrDefault();
            if (target is null)
            {
                return;
            }

            handle = target.Handle;
            _targetHandles[rule.Id] = handle;
        }

        TargetActivatedFromWidget?.Invoke(handle);
        _windowService.TryFocus(handle);
        System.Windows.Application.Current.Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() => _windowService.TryFocus(handle)));
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
            widget.TriggerMode = rule.FloatingWidgetTriggerMode;
            widget.EdgeSnapEnabled = true;
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
        foreach (var widget in _widgets.Values)
        {
            widget.Close();
        }

        _widgets.Clear();
        _targetHandles.Clear();
    }
}
