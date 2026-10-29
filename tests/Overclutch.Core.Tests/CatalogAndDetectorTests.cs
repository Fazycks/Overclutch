using Overclutch.Core.GameMode;
using Overclutch.Core.Tweaks;

namespace Overclutch.Core.Tests;

public sealed class CatalogAndDetectorTests
{
    [Fact]
    public void Catalog_ids_are_unique_and_advanced_tweaks_explain_their_risk()
    {
        Assert.Equal(TweakCatalog.All.Count, TweakCatalog.All.Select(t => t.Id).Distinct().Count());
        Assert.All(TweakCatalog.All.Where(t => t.Risk == TweakRisk.Advanced), t => Assert.False(string.IsNullOrWhiteSpace(t.Warning)));
        Assert.DoesNotContain(TweakCatalog.Profile, t => t.Risk == TweakRisk.Advanced);
    }

    [Theory]
    [InlineData(@"D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive\game\bin\win64\cs2.exe", true)]
    [InlineData(@"C:\Riot Games\VALORANT\live\ShooterGame\Binaries\Win64\VALORANT-Win64-Shipping.exe", true)]
    [InlineData(@"C:\Riot Games\Riot Client\RiotClientServices.exe", false)]
    [InlineData(@"D:\SteamLibrary\steamapps\common\Game\UnityCrashHandler64.exe", false)]
    [InlineData(@"C:\Program Files (x86)\Steam\steamapps\common\Game\EasyAntiCheat_EOS_Setup.exe", false)]
    [InlineData(@"C:\Program Files\Google\Chrome\Application\chrome.exe", false)]
    public void Detects_games_from_their_install_path(string path, bool expected) =>
        Assert.Equal(expected, GameDetector.IsGame(path, []));

    [Fact]
    public void Extra_games_match_with_or_without_extension() =>
        Assert.True(GameDetector.IsGame(@"C:\Games\osu!\osu!.exe", ["osu!.exe"]));

    [Theory]
    [InlineData("Oui", "yes")]
    [InlineData("Non", "no")]
    [InlineData("Yes", "yes")]
    [InlineData("Nein", "no")]
    [InlineData("TscSyncPolicy", "TscSyncPolicy")]
    [InlineData(null, null)]
    public void Localized_bcd_booleans_are_written_back_in_english(string? read, string? expected) =>
        Assert.Equal(expected, Overclutch.Core.Native.Bcd.ToWritable(read));

    private const string TaskXml = """
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.6" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <Settings>{0}</Settings>
          <Triggers><BootTrigger><Enabled>false</Enabled></BootTrigger></Triggers>
        </Task>
        """;

    [Theory]
    [InlineData("", true)]
    [InlineData("<Enabled>false</Enabled>", false)]
    [InlineData("<Enabled>true</Enabled>", true)]
    public void Reads_whether_a_scheduled_task_is_enabled_from_its_settings(string settings, bool expected) =>
        Assert.Equal(expected, Overclutch.Core.Native.ScheduledTasks.ParseEnabled(string.Format(TaskXml, settings)));
}
