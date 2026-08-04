using TouchFish.Modules.BossKey;
using Xunit;

namespace TouchFish.Modules.BossKey.Tests;

public sealed class AutoMinimizePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void ActiveCursorFocusOrInputMethodPreventsInactivityTracking(
        bool cursorInsideTarget,
        bool targetHasFocus,
        bool inputMethodActive)
    {
        Assert.False(AutoMinimizePolicy.CanTrackInactivity(
            cursorInsideTarget,
            targetHasFocus,
            inputMethodActive));
    }

    [Fact]
    public void TracksInactivityOnlyAfterCursorLeavesAndTargetLosesFocus()
    {
        Assert.True(AutoMinimizePolicy.CanTrackInactivity(
            cursorInsideTarget: false,
            targetHasFocus: false,
            inputMethodActive: false));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void WidgetActivationTracksCursorLeavingWithoutRequiringFocusLoss(
        bool cursorInsideTarget,
        bool inputMethodActive,
        bool expected)
    {
        Assert.Equal(expected, AutoMinimizePolicy.CanTrackWidgetInactivity(
            cursorInsideTarget,
            inputMethodActive));
    }

    [Fact]
    public void MinimizesWhenConfiguredSecondsHaveElapsed()
    {
        var lostFocusAt = Now.AddSeconds(-10);

        Assert.True(AutoMinimizePolicy.ShouldMinimize(lostFocusAt, 10, Now));
    }

    [Fact]
    public void ZeroSecondsMinimizesImmediatelyAfterEligibleInactivityStarts()
    {
        Assert.True(AutoMinimizePolicy.ShouldMinimize(Now, 0, Now));
    }

    [Fact]
    public void WidgetEntryGraceProtectsOnlyTheFirstSecond()
    {
        var graceUntil = Now.AddSeconds(1);

        Assert.True(AutoMinimizePolicy.IsEntryGraceActive(graceUntil, Now));
        Assert.True(AutoMinimizePolicy.IsEntryGraceActive(graceUntil, Now.AddMilliseconds(999)));
        Assert.False(AutoMinimizePolicy.IsEntryGraceActive(graceUntil, Now.AddSeconds(1)));
        Assert.False(AutoMinimizePolicy.IsEntryGraceActive(null, Now));
    }

    [Fact]
    public void NonWidgetActivationStaysSuppressedUntilCursorEntersTarget()
    {
        Assert.True(AutoMinimizePolicy.IsNonWidgetActivationSuppressed(true, false));
        Assert.False(AutoMinimizePolicy.IsNonWidgetActivationSuppressed(true, true));
        Assert.False(AutoMinimizePolicy.IsNonWidgetActivationSuppressed(false, false));
    }

    [Fact]
    public void DoesNotMinimizeBeforeDelayOrWithoutLostFocusTimestamp()
    {
        var lostFocusAt = Now.AddSeconds(-9);

        Assert.False(AutoMinimizePolicy.ShouldMinimize(lostFocusAt, 10, Now));
        Assert.False(AutoMinimizePolicy.ShouldMinimize(null, 0, Now));
    }
}
