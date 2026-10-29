namespace Overclutch.Core.GameMode;

/// <summary>Decides from an executable path whether it is a game.</summary>
public static class GameDetector
{
    private static readonly string[] LibraryMarkers =
    [
        @"\steamapps\common\",
        @"\Epic Games\",
        @"\Riot Games\",
        @"\EA Games\",
        @"\Ubisoft Game Launcher\games\",
        @"\GOG Galaxy\Games\",
        @"\XboxGames\",
        @"\Rockstar Games\",
    ];

    // Launchers, crash reporters and anti-cheat helpers live next to the games but are not games.
    private static readonly string[] ExcludedNameParts =
    [
        "launcher", "crash", "report", "anticheat", "easyanticheat", "beservice", "battleye", "vanguard",
        "riotclient", "redist", "vcredist", "directx", "dxsetup", "setup", "unins", "install", "helper",
        "updater", "webhelper", "overlay", "cefprocess", "epicwebhelper", "battle.net",
    ];

    /// <param name="installs">Game folders the launchers know about, wherever they are (<see cref="GameLibraries"/>).</param>
    public static bool IsGame(string exePath, IEnumerable<string> extraGames, IEnumerable<GameInstall>? installs = null)
    {
        var name = Path.GetFileNameWithoutExtension(exePath);
        if (extraGames.Any(g => string.Equals(TrimExe(g), name, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (ExcludedNameParts.Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase)))
            return false;

        return LibraryMarkers.Any(marker => exePath.Contains(marker, StringComparison.OrdinalIgnoreCase))
            || InstallOf(exePath, installs ?? []) is not null;
    }

    /// <summary>The innermost known game folder holding this executable.</summary>
    public static GameInstall? InstallOf(string exePath, IEnumerable<GameInstall> installs) =>
        installs.Where(i => i.Contains(exePath)).MaxBy(i => i.Directory.Length);

    public static string TrimExe(string name) =>
        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
}
