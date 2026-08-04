using TouchFish.Contracts;
using Xunit;

namespace TouchFish.Modules.BossKey.Tests;

public sealed class CursorWindowBoundsPolicyTests
{
    [Theory]
    [InlineData(100, 100, true)]
    [InlineData(299, 249, true)]
    [InlineData(99, 100, false)]
    [InlineData(300, 100, false)]
    [InlineData(100, 250, false)]
    public void UsesScreenCoordinatesInsteadOfTopmostWindowIdentity(
        int cursorX,
        int cursorY,
        bool expected)
    {
        Assert.Equal(expected, CursorWindowBoundsPolicy.Contains(
            cursorX,
            cursorY,
            left: 100,
            top: 100,
            right: 300,
            bottom: 250));
    }
}
