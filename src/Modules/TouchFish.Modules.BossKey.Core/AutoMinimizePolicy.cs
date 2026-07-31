namespace TouchFish.Modules.BossKey;

public static class AutoMinimizePolicy
{
    public static bool ShouldMinimize(
        DateTimeOffset? lostFocusAt,
        int minutes,
        DateTimeOffset now)
    {
        return minutes > 0 &&
               lostFocusAt is not null &&
               now - lostFocusAt.Value >= TimeSpan.FromMinutes(minutes);
    }
}
