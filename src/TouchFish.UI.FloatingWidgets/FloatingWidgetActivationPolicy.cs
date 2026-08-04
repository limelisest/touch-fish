namespace TouchFish.UI.FloatingWidgets;

/// <summary>
/// Defines the common activation and auto-hide timing used by every floating widget.
/// </summary>
public static class FloatingWidgetActivationPolicy
{
    public static TimeSpan HoverActivationDelay { get; } = TimeSpan.FromSeconds(1);

    public static TimeSpan EntryGraceDuration { get; } = TimeSpan.FromSeconds(1);

    public static DateTimeOffset StartEntryGrace(DateTimeOffset now) => now + EntryGraceDuration;

    public static bool IsEntryGraceActive(DateTimeOffset graceUntil, DateTimeOffset now) => now < graceUntil;
}
