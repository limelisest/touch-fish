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

    public static bool IsEntryGraceActive(DateTimeOffset? graceUntil, DateTimeOffset now) =>
        graceUntil is not null && now < graceUntil.Value;

    public static bool IsNonWidgetActivationSuppressed(bool suppressionActive, bool cursorInsideTarget) =>
        suppressionActive && !cursorInsideTarget;
}
