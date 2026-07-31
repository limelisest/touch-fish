using TouchFish.Modules.BossKey;
using Xunit;

namespace TouchFish.Modules.BossKey.Tests;

public sealed class AutoMinimizePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MinimizesWhenConfiguredDelayHasElapsed()
    {
        var lostFocusAt = Now.AddMinutes(-1);

        Assert.True(AutoMinimizePolicy.ShouldMinimize(lostFocusAt, 1, Now));
    }

    [Fact]
    public void DoesNotMinimizeBeforeDelayOrWhenDisabled()
    {
        var lostFocusAt = Now.AddSeconds(-59);

        Assert.False(AutoMinimizePolicy.ShouldMinimize(lostFocusAt, 1, Now));
        Assert.False(AutoMinimizePolicy.ShouldMinimize(Now.AddHours(-1), 0, Now));
    }
}
