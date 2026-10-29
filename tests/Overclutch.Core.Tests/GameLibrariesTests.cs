using Overclutch.Core.GameMode;

namespace Overclutch.Core.Tests;

public sealed class GameLibrariesTests
{
    private static readonly GameInstall Fortnite = new(@"D:\Jeux\Epic\Fortnite", "Fortnite");

    [Fact]
    public void Reads_an_epic_manifest_wherever_the_game_was_installed()
    {
        var install = GameLibraries.ParseEpicManifest("""
            { "bIsIncompleteInstall": false, "DisplayName": "Fortnite", "InstallLocation": "D:\\Jeux\\Epic\\Fortnite" }
            """);

        Assert.Equal(Fortnite, install);
    }

    [Theory]
    [InlineData("""{ "bIsIncompleteInstall": true, "InstallLocation": "D:\\Jeux\\Epic\\Fortnite" }""")]
    [InlineData("""{ "DisplayName": "Fortnite" }""")]
    [InlineData("not json")]
    public void Skips_unfinished_or_unreadable_epic_manifests(string json) =>
        Assert.Null(GameLibraries.ParseEpicManifest(json));

    [Fact]
    public void Reads_steam_libraries_and_games()
    {
        const string folders = """
            "libraryfolders"
            {
            	"0"	{ "path"		"C:\\Program Files (x86)\\Steam" }
            	"1"	{ "path"		"D:\\SteamLibrary" }
            }
            """;
        const string manifest = """
            "AppState"
            {
            	"appid"		"1284190"
            	"name"		"The Planet Crafter"
            	"installdir"		"The Planet Crafter"
            }
            """;

        Assert.Equal([@"C:\Program Files (x86)\Steam", @"D:\SteamLibrary"], GameLibraries.VdfValues(folders, "path"));
        Assert.Equal(new GameInstall(@"D:\SteamLibrary\steamapps\common\The Planet Crafter", "The Planet Crafter"),
            GameLibraries.ParseSteamManifest(manifest, @"D:\SteamLibrary"));
    }

    [Fact]
    public void Reads_the_riot_install_path()
    {
        const string yaml = """
            patching_policy: "manual"
            product_install_full_path: "D:/Riot Games/VALORANT/live"
            product_install_root: "D:/Riot Games"
            """;

        Assert.Equal(new GameInstall(@"D:\Riot Games\VALORANT\live", null), GameLibraries.ParseRiotSettings(yaml));
    }

    [Theory]
    [InlineData(@"D:\Jeux\Epic\Fortnite\FortniteGame\Binaries\Win64\FortniteClient-Win64-Shipping.exe", true)]
    [InlineData(@"D:\Jeux\Epic\Fortnite\FortniteGame\Binaries\Win64\FortniteLauncher.exe", false)]
    [InlineData(@"D:\Jeux\Epic\FortniteTools\tool.exe", false)]
    [InlineData(@"D:\Jeux\Autre\jeu.exe", false)]
    public void Games_in_a_launcher_folder_are_detected_on_any_drive(string path, bool expected) =>
        Assert.Equal(expected, GameDetector.IsGame(path, [], [Fortnite]));

    [Fact]
    public void The_launcher_name_is_used_for_the_game()
    {
        var engine = new GameInstall(@"D:\Jeux\Epic\Fortnite\Engine", null);

        Assert.Equal("Fortnite", GameDetector.InstallOf(@"D:\Jeux\Epic\Fortnite\FortniteGame\Binaries\Win64\Game.exe", [engine, Fortnite])?.Name);
        Assert.Same(engine, GameDetector.InstallOf(@"D:\Jeux\Epic\Fortnite\Engine\Binaries\x.exe", [Fortnite, engine]));
    }
}
