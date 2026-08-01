using TouchFish.Contracts;
using Xunit;

namespace TouchFish.Modules.BossKey.Tests;

public sealed class FloatingWidgetSnapPolicyTests
{
    [Fact]
    public void NearbyWidgetSnapsAndAlignsBesidePeer()
    {
        var current = new FloatingWidgetBounds(185, 105, 120, 40);
        var peer = new FloatingWidgetBounds(310, 100, 120, 40);

        var result = FloatingWidgetSnapPolicy.SnapToPeers(current, [peer]);

        Assert.Equal((190d, 100d), result);
    }

    [Fact]
    public void DistantWidgetDoesNotAffectPosition()
    {
        var current = new FloatingWidgetBounds(100, 100, 120, 40);
        var peer = new FloatingWidgetBounds(500, 500, 120, 40);

        var result = FloatingWidgetSnapPolicy.SnapToPeers(current, [peer]);

        Assert.Equal((100d, 100d), result);
    }

    [Fact]
    public void WidgetCanSnapIntoVerticalStack()
    {
        var current = new FloatingWidgetBounds(207, 247, 120, 40);
        var peer = new FloatingWidgetBounds(200, 200, 120, 40);

        var result = FloatingWidgetSnapPolicy.SnapToPeers(current, [peer]);

        Assert.Equal((200d, 240d), result);
    }
}
