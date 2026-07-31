using TouchFish.Modules.BossKey;
using Xunit;

namespace TouchFish.Modules.BossKey.Tests;

public sealed class BossKeySettingsMigrationTests
{
    [Fact]
    public void ConvertsLegacyMinutesToSeconds()
    {
        var settings = new BossKeySettings
        {
            SchemaVersion = 2,
            Windows =
            [
                new WindowRule { LegacyAutoMinimizeMinutes = 2 }
            ]
        };

        BossKeySettingsMigration.Migrate(settings);

        Assert.Equal(3, settings.SchemaVersion);
        Assert.Equal(120, settings.Windows[0].AutoMinimizeSeconds);
        Assert.Null(settings.Windows[0].LegacyAutoMinimizeMinutes);
    }

    [Fact]
    public void OlderRuleWithoutValueDefaultsToSixtySeconds()
    {
        var settings = new BossKeySettings
        {
            SchemaVersion = 1,
            Windows = [new WindowRule()]
        };

        BossKeySettingsMigration.Migrate(settings);

        Assert.Equal(60, settings.Windows[0].AutoMinimizeSeconds);
    }
}
