namespace TouchFish.UI.FloatingWidgets;

/// <summary>
/// Defines the common auto-hide timing used when a floating widget opens a window.
/// </summary>
public static class FloatingWidgetActivationPolicy
{
    public static TimeSpan EntryGraceDuration { get; } = TimeSpan.FromSeconds(1);

    public static DateTimeOffset StartEntryGrace(DateTimeOffset now) => now + EntryGraceDuration;

    public static bool IsEntryGraceActive(DateTimeOffset graceUntil, DateTimeOffset now) => now < graceUntil;
}
