using System.Text.Json.Serialization;
using TouchFish.Contracts;

namespace TouchFish.Modules.Browser;

public sealed class BrowserSettings
{
    public int SchemaVersion { get; set; } = 1;
    public List<BrowserSite> Sites { get; set; } = [];
}

public sealed class BrowserSite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "新网页";
    public string Url { get; set; } = "https://www.bing.com";
    public bool IsEnabled { get; set; } = true;
    public double WindowOpacity { get; set; } = 1;
    public bool WindowTopmost { get; set; } = true;
    public int AutoHideSeconds { get; set; }
    public bool FloatingWidgetEnabled { get; set; } = true;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FloatingWidgetTriggerMode FloatingWidgetTriggerMode { get; set; } = FloatingWidgetTriggerMode.Click;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double WindowWidth { get; set; } = 760;
    public double WindowHeight { get; set; } = 560;
    public double? FloatingWidgetLeft { get; set; }
    public double? FloatingWidgetTop { get; set; }
}
