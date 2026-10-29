using Overclutch.Core.GameMode;
using Overclutch.Core.Settings;

namespace Overclutch.Core.Tests;

public sealed class GameModeTests
{
    [Fact]
    public void Performance_does_everything_balanced_does_and_more()
    {
        var balanced = GameModeOptions.Balanced;
        var performance = GameModeOptions.Performance;

        Assert.True(balanced is { RaisePriority: true, BoostPowerPlan: true, LowerBackgroundApps: true });
        Assert.True(performance is { RaisePriority: true, BoostPowerPlan: true, LowerBackgroundApps: true });
        Assert.False(balanced.PurgeStandbyMemory || balanced.PauseServices);
        Assert.True(performance.PurgeStandbyMemory && performance.PauseServices);
    }

    [Fact]
    public void Only_the_custom_level_uses_the_custom_options()
    {
        var custom = new GameModeOptions { RaisePriority = false, PauseServices = true };

        Assert.Equal(GameModeOptions.Balanced, GameModeOptions.For(GameModeLevel.Balanced, custom));
        Assert.Equal(GameModeOptions.Performance, GameModeOptions.For(GameModeLevel.Performance, custom));
        Assert.Same(custom, GameModeOptions.For(GameModeLevel.Custom, custom));
    }

    [Fact]
    public void Presets_are_fresh_copies()
    {
        GameModeOptions.Balanced.PauseServices = true;
        Assert.False(GameModeOptions.Balanced.PauseServices);
    }

    [Fact]
    public void Custom_options_survive_a_save()
    {
        var settings = new AppSettings
        {
            GameModeLevel = GameModeLevel.Custom,
            GameModeCustom = new GameModeOptions { BoostPowerPlan = false, PurgeStandbyMemory = true },
        };

        var loaded = AppSettings.Parse(settings.ToJson());

        Assert.Equal(GameModeLevel.Custom, loaded.GameModeLevel);
        Assert.Equal(settings.GameModeCustom, loaded.GameModeOptions);
    }

    [Theory]
    [InlineData("{}", GameModeLevel.Balanced)]
    [InlineData("""{"GameModeBoostPowerPlan":true,"GameModeLowerBackgroundApps":true}""", GameModeLevel.Balanced)]
    [InlineData("""{"GameModeBoostPowerPlan":false,"GameModeLowerBackgroundApps":true}""", GameModeLevel.Custom)]
    public void Settings_from_v0_3_keep_their_choices(string json, GameModeLevel level)
    {
        var loaded = AppSettings.Parse(json);

        Assert.Equal(level, loaded.GameModeLevel);
        Assert.Equal(json.Contains("false") ? GameModeOptions.Balanced with { BoostPowerPlan = false } : GameModeOptions.Balanced,
            loaded.GameModeOptions);
        Assert.DoesNotContain("GameModeBoostPowerPlan", loaded.ToJson());
    }
}
