using System.IO;
using System.Text.Json;

namespace TouchFish.Modules.Browser;

public sealed class BrowserSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TouchFish",
        "browser-settings.json");

    public async Task<BrowserSettings> LoadAsync()
    {
        if (!File.Exists(_path)) return new BrowserSettings();
        try
        {
            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<BrowserSettings>(stream, Options) ?? new BrowserSettings();
        }
        catch
        {
            return new BrowserSettings();
        }
    }

    public async Task SaveAsync(BrowserSettings settings)
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
