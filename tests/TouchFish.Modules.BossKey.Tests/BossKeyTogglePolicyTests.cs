using TouchFish.Modules.BossKey;
using Xunit;

namespace TouchFish.Modules.BossKey.Tests;

public sealed class BossKeyTogglePolicyTests
{
    [Fact]
    public void MixedStateAlwaysMinimizesAll()
    {
        var action = BossKeyTogglePolicy.Decide([true, false, true]);

        Assert.Equal(BossKeyToggleAction.MinimizeAll, action);
    }

    [Fact]
    public void AllMinimizedShowsAll()
    {
        var action = BossKeyTogglePolicy.Decide([true, true]);

        Assert.Equal(BossKeyToggleAction.ShowAll, action);
    }
}
