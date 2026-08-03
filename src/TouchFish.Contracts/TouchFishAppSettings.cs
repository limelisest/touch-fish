namespace TouchFish.Contracts;

public sealed class TouchFishAppSettings
{
    public bool AutoStartEnabled { get; set; }
    public bool SilentStartup { get; set; }
    public bool BossKeyFeatureEnabled { get; set; } = true;
    public bool ReaderFeatureEnabled { get; set; } = true;
}
