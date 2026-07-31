using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TouchFish.App;

public partial class SettingsViewModel(
    AppSettingsStore settingsStore,
    StartupTaskService startupTaskService) : ObservableObject
{
    private bool _savedAutoStartEnabled;
    private bool _savedSilentStartup;

    [ObservableProperty] private bool _autoStartEnabled;
    [ObservableProperty] private bool _silentStartup;
    [ObservableProperty] private string _statusText = "";

    public string Version { get; } = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "未知";
    public string Author => "LimeLisest";
    public string BuildTime { get; } = GetBuildTime();

    public async Task InitializeAsync()
    {
        var settings = await settingsStore.LoadAsync();
        AutoStartEnabled = settings.AutoStartEnabled;
        SilentStartup = settings.SilentStartup;
        _savedAutoStartEnabled = settings.AutoStartEnabled;
        _savedSilentStartup = settings.SilentStartup;
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            var settings = new TouchFishAppSettings
            {
                AutoStartEnabled = AutoStartEnabled,
                SilentStartup = SilentStartup
            };
            var startupConfigurationChanged = AutoStartEnabled != _savedAutoStartEnabled ||
                                              AutoStartEnabled && SilentStartup != _savedSilentStartup;
            if (startupConfigurationChanged)
            {
                await startupTaskService.ApplyAsync(AutoStartEnabled, SilentStartup);
            }

            await settingsStore.SaveAsync(settings);
            _savedAutoStartEnabled = AutoStartEnabled;
            _savedSilentStartup = SilentStartup;
            StatusText = AutoStartEnabled
                ? $"设置已保存，TouchFish 将在登录后{(SilentStartup ? "静默" : "正常")}启动。"
                : "设置已保存，开机自启动已关闭。";
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            StatusText = "已取消管理员授权，开机启动设置没有更改。";
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
    }

    private static string GetBuildTime()
    {
        var value = Assembly.GetEntryAssembly()?
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "BuildTimestampUtc")?
            .Value;
        return DateTimeOffset.TryParse(value, out var timestamp)
            ? timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            : "未知";
    }
}
