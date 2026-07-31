using Microsoft.Extensions.DependencyInjection;
using TouchFish.Contracts;

namespace TouchFish.Modules.Reader;

public sealed class ReaderModule : ITouchFishModule
{
    public TouchFishModuleMetadata Metadata { get; } = new(
        "reader",
        "看书",
        "导入 TXT 小说并通过悬浮窗阅读",
        200);
}

public static class ReaderModuleServiceCollectionExtensions
{
    public static IServiceCollection AddReaderModule(this IServiceCollection services)
    {
        services.AddSingleton<ITouchFishModule, ReaderModule>();
        services.AddSingleton<ReaderChapterParser>();
        services.AddSingleton<ReaderLibraryService>();
        services.AddSingleton<ReaderWindowManager>();
        services.AddSingleton<ReaderViewModel>();
        return services;
    }
}
