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

        Assert.Equal(5, settings.SchemaVersion);
        Assert.True(settings.Windows[0].AutoMinimizeEnabled);
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

    [Fact]
    public void OlderZeroValueMigratesToDisabledSwitch()
    {
        var settings = new BossKeySettings
        {
            SchemaVersion = 4,
            Windows = [new WindowRule { AutoMinimizeSeconds = 0 }]
        };

        BossKeySettingsMigration.Migrate(settings);

        Assert.False(settings.Windows[0].AutoMinimizeEnabled);
        Assert.Equal(0, settings.Windows[0].AutoMinimizeSeconds);
    }
}
