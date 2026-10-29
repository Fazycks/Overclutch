using System.Text.Json;
using System.Text.Json.Serialization;
using Overclutch.Core.GameMode;
using Overclutch.Core.Maintenance;

namespace Overclutch.Core.Settings;

public sealed class AppSettings
{
    public bool GameModeEnabled { get; set; } = true;

    public GameModeLevel GameModeLevel { get; set; } = GameModeLevel.Balanced;

    /// <summary>Options of the custom level, kept while another level is picked.</summary>
    public GameModeOptions GameModeCustom { get; set; } = GameModeOptions.Balanced;

    [JsonIgnore]
    public GameModeOptions GameModeOptions => GameModeOptions.For(GameModeLevel, GameModeCustom);

    // Up to v0.3.0 these two options were the only ones, and turning one off now means a custom level.
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private bool? GameModeBoostPowerPlan { get; set; }

    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private bool? GameModeLowerBackgroundApps { get; set; }

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
            return File.Exists(FilePath) ? Parse(File.ReadAllText(FilePath)) : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public static AppSettings Parse(string json)
    {
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new();
        if (settings.GameModeBoostPowerPlan is false || settings.GameModeLowerBackgroundApps is false)
        {
            settings.GameModeLevel = GameModeLevel.Custom;
            settings.GameModeCustom = GameModeOptions.Balanced with
            {
                BoostPowerPlan = settings.GameModeBoostPowerPlan ?? true,
                LowerBackgroundApps = settings.GameModeLowerBackgroundApps ?? true,
            };
        }
        settings.GameModeBoostPowerPlan = settings.GameModeLowerBackgroundApps = null;
        return settings;
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public void Save()
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        File.WriteAllText(FilePath, ToJson());
    }
}
