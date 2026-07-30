namespace TouchFish.Contracts;

public sealed record TouchFishModuleMetadata(
    string Id,
    string Name,
    string Description,
    int Order);

public interface ITouchFishModule
{
    TouchFishModuleMetadata Metadata { get; }
}
