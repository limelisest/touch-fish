using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TouchFish.Modules.Browser;

public partial class BrowserViewModel : ObservableObject, IDisposable
{
    private readonly BrowserSettingsStore _store;
    private readonly BrowserWindowManager _windowManager;
    private readonly DispatcherTimer _saveTimer;
    private bool _featureEnabled = true;
    private bool _initialized;

    public BrowserViewModel(BrowserSettingsStore store, BrowserWindowManager windowManager)
    {
        _store = store;
        _windowManager = windowManager;
        _windowManager.StatusChanged += message => StatusText = message;
        _saveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _saveTimer.Tick += async (_, _) =>
        {
            _saveTimer.Stop();
            _windowManager.Sync(Sites, ScheduleSave);
            await SaveAsync();
        };
    }

    public ObservableCollection<BrowserSiteItemViewModel> Sites { get; } = [];
    [ObservableProperty] private BrowserSiteItemViewModel? _selectedSite;
    [ObservableProperty] private string _statusText = "添加网页后，可分别开启网页窗口。";

    public async Task InitializeAsync()
    {
        var settings = await _store.LoadAsync();
        foreach (var site in settings.Sites)
        {
            var item = new BrowserSiteItemViewModel(site);
            Attach(item);
            Sites.Add(item);
        }
        SelectedSite = Sites.FirstOrDefault();
        _initialized = true;
        _windowManager.SetFeatureEnabled(_featureEnabled);
        _windowManager.Sync(Sites, ScheduleSave);
        StatusText = Sites.Count == 0 ? "请先添加网页。" : $"已载入 {Sites.Count} 个网页。";
    }

    public void SetFeatureEnabled(bool enabled)
    {
        _featureEnabled = enabled;
        _windowManager.SetFeatureEnabled(enabled);
        StatusText = enabled ? "浏览器功能已启用。" : "浏览器功能已关闭。";
    }

    [RelayCommand]
    private void AddSite()
    {
        var index = Sites.Count + 1;
        var item = new BrowserSiteItemViewModel(new BrowserSite
        {
            Name = $"网页 {index}",
            Url = "https://www.bing.com",
            IsEnabled = false
        });
        Attach(item);
        Sites.Add(item);
        SelectedSite = item;
        ScheduleSave();
        StatusText = "已添加网页，请填写名称和网址后开启。";
    }

    [RelayCommand]
    private void DeleteSite()
    {
        if (SelectedSite is null) return;
        var index = Sites.IndexOf(SelectedSite);
        Detach(SelectedSite);
        Sites.Remove(SelectedSite);
        SelectedSite = Sites.Count == 0 ? null : Sites[Math.Clamp(index, 0, Sites.Count - 1)];
        ScheduleSave();
        StatusText = "网页已删除。";
    }

    [RelayCommand]
    private void MoveSiteUp()
    {
        if (SelectedSite is null) return;
        var index = Sites.IndexOf(SelectedSite);
        if (index <= 0) return;
        Sites.Move(index, index - 1);
        ScheduleSave();
    }

    [RelayCommand]
    private void MoveSiteDown()
    {
        if (SelectedSite is null) return;
        var index = Sites.IndexOf(SelectedSite);
        if (index < 0 || index >= Sites.Count - 1) return;
        Sites.Move(index, index + 1);
        ScheduleSave();
    }

    [RelayCommand]
    private async Task OpenSelectedAsync()
    {
        if (!_featureEnabled)
        {
            StatusText = "浏览器功能已关闭，请先通过侧边栏开关启用。";
            return;
        }
        if (SelectedSite is null) return;
        if (!SelectedSite.IsEnabled) SelectedSite.IsEnabled = true;
        await _windowManager.OpenAsync(SelectedSite);
        StatusText = $"已显示“{SelectedSite.Name}”。";
    }

    private void Attach(BrowserSiteItemViewModel site) => site.PropertyChanged += OnSitePropertyChanged;
    private void Detach(BrowserSiteItemViewModel site) => site.PropertyChanged -= OnSitePropertyChanged;

    private void OnSitePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BrowserSiteItemViewModel.IsClickTrigger) or
            nameof(BrowserSiteItemViewModel.IsPointerHoverTrigger)) return;
        if (e.PropertyName == nameof(BrowserSiteItemViewModel.IsEnabled))
        {
            _windowManager.Sync(Sites, ScheduleSave);
        }
        ScheduleSave();
    }

    private void ScheduleSave()
    {
        if (!_initialized) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private BrowserSettings CreateSettings() => new()
    {
        Sites = Sites.Select(site => site.ToModel()).ToList()
    };

    private Task SaveAsync() => _store.SaveAsync(CreateSettings());

    public void Shutdown()
    {
        _saveTimer.Stop();
        try
        {
            if (_initialized)
            {
                _store.SaveSynchronously(CreateSettings());
            }
        }
        catch
        {
            // A settings write failure must never prevent WebView2 windows from closing.
        }
        finally
        {
            _windowManager.Shutdown();
        }
    }

    public void Dispose()
    {
        _saveTimer.Stop();
        foreach (var site in Sites) Detach(site);
        _windowManager.StatusChanged -= message => StatusText = message;
    }
}
