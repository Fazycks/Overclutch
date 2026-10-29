using System.Text.Json;
using Overclutch.Core.Maintenance;

namespace Overclutch.Core.Settings;

public sealed class AppSettings
{
    public bool GameModeEnabled { get; set; } = true;

    /// <summary>Switch to the Overclutch power plan while a game runs.</summary>
    public bool GameModeBoostPowerPlan { get; set; } = true;

    /// <summary>Lower the priority of <see cref="BackgroundApps"/> while a game runs.</summary>
    public bool GameModeLowerBackgroundApps { get; set; } = true;

    /// <summary>Executables (without .exe) treated as games even outside known game libraries.</summary>
    public List<string> ExtraGames { get; set; } = [];

    /// <summary>Executables (without .exe) set to below-normal priority while a game runs.</summary>
    public List<string> BackgroundApps { get; set; } =
        ["OneDrive", "ms-teams", "Widgets", "PhoneExperienceHost", "SearchHost", "msedge", "Spotify"];

    public bool MinimizeToTray { get; set; } = true;

    /// <summary>Ids of the checked maintenance tasks, run together by hand or on <see cref="MaintenanceSchedule"/>.</summary>
    public List<string> MaintenanceTasks { get; set; } = ["temp", "windows-update", "error-reports"];

    public MaintenanceSchedule MaintenanceSchedule { get; set; } = MaintenanceSchedule.Never;

    public DateTime? LastMaintenanceRun { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static string FilePath => Path.Combine(AppPaths.DataDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new()
                : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
