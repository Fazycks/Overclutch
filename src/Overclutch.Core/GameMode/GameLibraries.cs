using System.Diagnostics;
using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Overclutch.Core.GameMode;

/// <summary>A game folder a launcher knows about. Name is null when the launcher does not give one.</summary>
public sealed record GameInstall(string Directory, string? Name)
{
    /// <summary>Full path ending with a separator, so "Fortnite" never contains "FortniteOther".</summary>
    public string Directory { get; } = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Directory)) + '\\';

    public bool Contains(string path) => path.StartsWith(Directory, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Reads where the launchers installed their games, so a library moved to "D:\Jeux" is still recognized.
/// </summary>
public static partial class GameLibraries
{
    private static readonly string ProgramData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    public static IReadOnlyList<GameInstall> Discover()
    {
        var installs = new List<GameInstall>();
        foreach (var source in (Func<IEnumerable<GameInstall>>[])[Epic, Steam, Riot, Gog, Ubisoft])
        {
            try
            {
                installs.AddRange(source());
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException or ArgumentException)
            {
                Trace.WriteLine($"Bibliothèques de jeux : {e.Message}");
            }
        }
        return installs;
    }

    private static IEnumerable<GameInstall> Epic()
    {
        var manifests = Path.Combine(ProgramData, @"Epic\EpicGamesLauncher\Data\Manifests");
        if (!System.IO.Directory.Exists(manifests))
            yield break;
        foreach (var file in System.IO.Directory.EnumerateFiles(manifests, "*.item"))
            if (ParseEpicManifest(File.ReadAllText(file)) is { } install)
                yield return install;
    }

    public static GameInstall? ParseEpicManifest(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("bIsIncompleteInstall", out var incomplete) && incomplete.ValueKind == JsonValueKind.True)
                return null;
            if (!root.TryGetProperty("InstallLocation", out var location) || location.GetString() is not { Length: > 0 } directory)
                return null;
            var name = root.TryGetProperty("DisplayName", out var display) ? display.GetString() : null;
            return new GameInstall(directory, name);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<GameInstall> Steam()
    {
        if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) is not string steam)
            yield break;
        var folders = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
        if (!File.Exists(folders))
            yield break;
        foreach (var library in VdfValues(File.ReadAllText(folders), "path"))
        {
            var apps = Path.Combine(library, "steamapps");
            if (!System.IO.Directory.Exists(apps))
                continue;
            foreach (var manifest in System.IO.Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
                if (ParseSteamManifest(File.ReadAllText(manifest), library) is { } install)
                    yield return install;
        }
    }

    public static GameInstall? ParseSteamManifest(string acf, string library)
    {
        var folder = VdfValues(acf, "installdir").FirstOrDefault();
        return folder is null ? null : new GameInstall(Path.Combine(library, "steamapps", "common", folder), VdfValues(acf, "name").FirstOrDefault());
    }

    /// <summary>Values of a key in Valve's text format: "key"  "value", with backslashes escaped.</summary>
    public static IEnumerable<string> VdfValues(string vdf, string key) =>
        VdfPair().Matches(vdf)
            .Where(m => string.Equals(m.Groups[1].Value, key, StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Groups[2].Value.Replace(@"\\", @"\"));

    [GeneratedRegex("""
        "([^"]+)"[ \t]+"((?:[^"\\]|\\.)*)"
        """)]
    private static partial Regex VdfPair();

    private static IEnumerable<GameInstall> Riot()
    {
        var metadata = Path.Combine(ProgramData, @"Riot Games\Metadata");
        if (!System.IO.Directory.Exists(metadata))
            yield break;
        foreach (var file in System.IO.Directory.EnumerateFiles(metadata, "*.product_settings.yaml", SearchOption.AllDirectories))
            if (ParseRiotSettings(File.ReadAllText(file)) is { } install)
                yield return install;
    }

    public static GameInstall? ParseRiotSettings(string yaml) =>
        RiotInstallPath().Match(yaml) is { Success: true } match ? new GameInstall(match.Groups[1].Value, null) : null;

    [GeneratedRegex("""^product_install_full_path:\s*"([^"]+)"\s*$""", RegexOptions.Multiline)]
    private static partial Regex RiotInstallPath();

    private static IEnumerable<GameInstall> Gog() =>
        FromRegistry(@"SOFTWARE\WOW6432Node\GOG.com\Games", "path", "gameName");

    private static IEnumerable<GameInstall> Ubisoft() =>
        FromRegistry(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs", "InstallDir", null);

    /// <summary>One subkey per game, holding its folder and sometimes its name.</summary>
    private static IEnumerable<GameInstall> FromRegistry(string keyPath, string directoryValue, string? nameValue)
    {
        using var games = Registry.LocalMachine.OpenSubKey(keyPath);
        if (games is null)
            yield break;
        foreach (var id in games.GetSubKeyNames())
        {
            using var game = games.OpenSubKey(id);
            if (game?.GetValue(directoryValue) is string { Length: > 0 } directory)
                yield return new GameInstall(directory, nameValue is null ? null : game.GetValue(nameValue) as string);
        }
    }
}
