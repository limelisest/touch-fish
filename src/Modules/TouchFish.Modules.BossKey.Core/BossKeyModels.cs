using System.Text.Json.Serialization;
using TouchFish.Contracts;

namespace TouchFish.Modules.BossKey;

public sealed class BossKeySettings
{
    public int SchemaVersion { get; set; } = 3;
    public HotkeyGesture Hotkey { get; set; } = new(0x4D, HotkeyModifiers.Control | HotkeyModifiers.Alt, "M");
    public List<WindowRule> Windows { get; set; } = [];
}

public sealed class WindowRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ProcessPath { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string WindowClass { get; set; } = string.Empty;
    public string TitleContains { get; set; } = string.Empty;
    public string? AppUserModelId { get; set; }
    public string? BrowserAppId { get; set; }
    public int AutoMinimizeSeconds { get; set; } = 60;

    [JsonPropertyName("autoMinimizeMinutes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LegacyAutoMinimizeMinutes { get; set; }
}
