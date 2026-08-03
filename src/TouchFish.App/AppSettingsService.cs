using System.Diagnostics;
using System.IO;
using System.Text.Json;
using TouchFish.Contracts;

namespace TouchFish.App;

public sealed class AppSettingsStore
{
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TouchFish",
        "appsettings.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<TouchFishAppSettings> LoadAsync()
    {
        if (!File.Exists(_path)) return new TouchFishAppSettings();
        try
        {
            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<TouchFishAppSettings>(stream, Options)
                   ?? new TouchFishAppSettings();
        }
        catch
        {
            return new TouchFishAppSettings();
        }
    }

    public async Task SaveAsync(TouchFishAppSettings settings)
    {
        await _saveLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = $"{_path}.tmp";
            await using (var stream = File.Create(temporary))
            {
                await JsonSerializer.SerializeAsync(stream, settings, Options);
            }

            File.Move(temporary, _path, true);
        }
        finally
        {
            _saveLock.Release();
        }
    }
}

public sealed class StartupTaskService
{
    private const string TaskName = "TouchFish Startup";

    public async Task ApplyAsync(bool enabled, bool silent)
    {
        if (!enabled)
        {
            await RunAsync(["/Delete", "/TN", TaskName, "/F"], ignoreFailure: true);
            return;
        }

        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("无法确定 TouchFish 程序路径。");
        var command = $"\"{executable}\"{(silent ? " --silent" : string.Empty)}";
        await RunAsync([
            "/Create",
            "/TN", TaskName,
            "/TR", command,
            "/SC", "ONLOGON",
            "/RL", "LIMITED",
            "/F"
        ]);
    }

    private static async Task RunAsync(IEnumerable<string> arguments, bool ignoreFailure = false)
    {
        var startInfo = new ProcessStartInfo("schtasks.exe")
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 Windows 任务计划程序。");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0 && !ignoreFailure)
        {
            throw new InvalidOperationException($"开机启动设置失败，任务计划程序返回代码 {process.ExitCode}。");
        }
    }
}
