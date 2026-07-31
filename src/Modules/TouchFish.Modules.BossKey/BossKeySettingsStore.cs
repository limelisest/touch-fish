using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TouchFish.Modules.BossKey;

public interface IBossKeySettingsStore
{
    Task<BossKeySettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(BossKeySettings settings, CancellationToken cancellationToken = default);
}

public sealed class BossKeySettingsStore : IBossKeySettingsStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TouchFish",
        "modules",
        "boss-key",
        "settings.v1.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<BossKeySettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new BossKeySettings();
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var stream = File.OpenRead(_filePath);
            var settings = await JsonSerializer.DeserializeAsync<BossKeySettings>(stream, SerializerOptions, cancellationToken)
                           ?? new BossKeySettings();
            return BossKeySettingsMigration.Migrate(settings);
        }
        catch (JsonException)
        {
            var backup = $"{_filePath}.broken-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Move(_filePath, backup, true);
            return new BossKeySettings();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(BossKeySettings settings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = $"{_filePath}.tmp";
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, _filePath, true);
        }
        finally
        {
            _gate.Release();
        }
    }
}
