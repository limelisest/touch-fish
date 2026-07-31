namespace TouchFish.Modules.BossKey;

public static class AutoMinimizePolicy
{
    public static bool ShouldMinimize(
        DateTimeOffset? lostFocusAt,
        int seconds,
        DateTimeOffset now)
    {
        return seconds >= 0 &&
               lostFocusAt is not null &&
               now - lostFocusAt.Value >= TimeSpan.FromSeconds(seconds);
    }
}
