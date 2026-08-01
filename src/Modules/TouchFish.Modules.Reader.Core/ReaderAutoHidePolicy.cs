namespace TouchFish.Modules.Reader;

public static class ReaderAutoHidePolicy
{
    public static bool ShouldHide(
        DateTimeOffset? entryGraceUntil,
        DateTimeOffset? cursorLeftAt,
        int seconds,
        DateTimeOffset now)
    {
        if (entryGraceUntil is not null && now < entryGraceUntil.Value)
        {
            return false;
        }

        return cursorLeftAt is not null &&
               now - cursorLeftAt.Value >= TimeSpan.FromSeconds(Math.Max(0, seconds));
    }
}
