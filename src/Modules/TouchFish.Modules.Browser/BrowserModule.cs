using Microsoft.Extensions.DependencyInjection;

namespace TouchFish.Modules.Browser;

public static class BrowserModule
{
    public static IServiceCollection AddBrowserModule(this IServiceCollection services)
    {
        services.AddSingleton<BrowserSettingsStore>();
        services.AddSingleton<BrowserWindowManager>();
        services.AddSingleton<BrowserViewModel>();
        return services;
    }
}
