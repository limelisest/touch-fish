using TouchFish.Modules.BossKey;
using Xunit;

namespace TouchFish.Modules.BossKey.Tests;

public sealed class AutoMinimizePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MinimizesWhenConfiguredSecondsHaveElapsed()
    {
        var lostFocusAt = Now.AddSeconds(-10);

        Assert.True(AutoMinimizePolicy.ShouldMinimize(lostFocusAt, 10, Now));
    }

    [Fact]
    public void ZeroSecondsTriggersImmediatelyAfterFocusIsLost()
    {
        Assert.True(AutoMinimizePolicy.ShouldMinimize(Now, 0, Now));
    }

    [Fact]
    public void DoesNotMinimizeBeforeDelayOrWithoutLostFocusTimestamp()
    {
        var lostFocusAt = Now.AddSeconds(-9);

        Assert.False(AutoMinimizePolicy.ShouldMinimize(lostFocusAt, 10, Now));
        Assert.False(AutoMinimizePolicy.ShouldMinimize(null, 0, Now));
    }
}
