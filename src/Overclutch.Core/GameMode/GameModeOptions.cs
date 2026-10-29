namespace Overclutch.Core.GameMode;

public enum GameModeLevel
{
    Balanced,
    Performance,
    Custom,
}

/// <summary>What the game mode does while a game runs.</summary>
public sealed record GameModeOptions
{
    /// <summary>Give the game a high CPU priority.</summary>
    public bool RaisePriority { get; set; } = true;

    /// <summary>Switch to the Overclutch power plan.</summary>
    public bool BoostPowerPlan { get; set; } = true;

    /// <summary>Lower the priority of the background apps list.</summary>
    public bool LowerBackgroundApps { get; set; } = true;

    /// <summary>Empty the standby list once, when the game starts.</summary>
    public bool PurgeStandbyMemory { get; set; }

    /// <summary>Stop search indexing and update downloads until the game exits.</summary>
    public bool PauseServices { get; set; }

    public static GameModeOptions Balanced => new();

    public static GameModeOptions Performance => new() { PurgeStandbyMemory = true, PauseServices = true };

    public static GameModeOptions For(GameModeLevel level, GameModeOptions custom) => level switch
    {
        GameModeLevel.Performance => Performance,
        GameModeLevel.Custom => custom,
        _ => Balanced,
    };
}
