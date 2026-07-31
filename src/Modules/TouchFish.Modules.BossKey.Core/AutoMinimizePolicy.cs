namespace TouchFish.Modules.BossKey;

public static class AutoMinimizePolicy
{
    public static bool ShouldMinimize(
        DateTimeOffset? inactiveSince,
        int seconds,
        DateTimeOffset now)
    {
        return seconds >= 0 &&
               inactiveSince is not null &&
               now - inactiveSince.Value >= TimeSpan.FromSeconds(seconds);
    }
}
