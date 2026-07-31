using Microsoft.Extensions.DependencyInjection;
using TouchFish.Contracts;

namespace TouchFish.Modules.BossKey;

public sealed class BossKeyModule : ITouchFishModule
{
    public TouchFishModuleMetadata Metadata { get; } = new(
        "boss-key",
        "老板键",
        "快速最小化和恢复指定窗口",
        100);
}

public static class BossKeyModuleServiceCollectionExtensions
{
    public static IServiceCollection AddBossKeyModule(this IServiceCollection services)
    {
        services.AddSingleton<ITouchFishModule, BossKeyModule>();
        services.AddSingleton<WindowRuleMatcher>();
        services.AddSingleton<IBossKeySettingsStore, BossKeySettingsStore>();
        services.AddSingleton<FloatingWidgetManager>();
        services.AddSingleton<BossKeyViewModel>();
        return services;
    }
}
