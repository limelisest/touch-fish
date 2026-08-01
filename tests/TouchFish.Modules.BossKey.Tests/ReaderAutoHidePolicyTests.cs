using TouchFish.Modules.Reader;
using Xunit;

namespace TouchFish.Modules.BossKey.Tests;

public sealed class ReaderAutoHidePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EntryGraceProtectsTheFirstSecond()
    {
        var graceUntil = Now.AddSeconds(1);

        Assert.False(ReaderAutoHidePolicy.ShouldHide(graceUntil, Now, 0, Now.AddMilliseconds(999)));
        Assert.True(ReaderAutoHidePolicy.ShouldHide(graceUntil, Now, 0, Now.AddSeconds(1)));
    }

    [Fact]
    public void ZeroSecondsHidesImmediatelyAfterCursorLeaves()
    {
        Assert.True(ReaderAutoHidePolicy.ShouldHide(null, Now, 0, Now));
    }

    [Fact]
    public void ConfiguredDelayStartsWhenCursorLeaves()
    {
        Assert.False(ReaderAutoHidePolicy.ShouldHide(null, Now, 3, Now.AddMilliseconds(2999)));
        Assert.True(ReaderAutoHidePolicy.ShouldHide(null, Now, 3, Now.AddSeconds(3)));
    }
}
