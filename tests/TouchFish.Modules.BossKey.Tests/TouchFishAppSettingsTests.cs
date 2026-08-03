using System.Text.Json;
using TouchFish.Contracts;
using Xunit;

namespace TouchFish.Modules.BossKey.Tests;

public sealed class TouchFishAppSettingsTests
{
    [Fact]
    public void NewSettings_EnableExistingFeaturesByDefault()
    {
        var settings = new TouchFishAppSettings();

        Assert.True(settings.BossKeyFeatureEnabled);
        Assert.True(settings.ReaderFeatureEnabled);
    }

    [Fact]
    public void LegacySettingsWithoutFeatureSwitches_KeepExistingFeaturesEnabled()
    {
        var settings = JsonSerializer.Deserialize<TouchFishAppSettings>(
            """{"autoStartEnabled":true,"silentStartup":false}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(settings);
        Assert.True(settings.BossKeyFeatureEnabled);
        Assert.True(settings.ReaderFeatureEnabled);
    }
}
