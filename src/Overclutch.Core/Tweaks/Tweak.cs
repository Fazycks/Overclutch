namespace Overclutch.Core.Tweaks;

public enum TweakCategory
{
    System,
    Background,
    Network,
    GpuGamingInput,
}

public enum TweakRisk
{
    /// <summary>Part of the one-click profile.</summary>
    Safe,

    /// <summary>Debated or hardware-dependent: only on the Advanced page, never in the profile.</summary>
    Advanced,
}

public enum TweakState
{
    NotApplied,
    Applied,
    Partial,
    Unavailable,
}

public sealed class Tweak
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required TweakCategory Category { get; init; }
    public TweakRisk Risk { get; init; } = TweakRisk.Safe;
    public bool RequiresReboot { get; init; }

    /// <summary>Shown on Advanced tweaks: what can go wrong.</summary>
    public string? Warning { get; init; }

    /// <summary>Built on demand because some targets (network interfaces...) depend on the current machine.</summary>
    public required Func<IReadOnlyList<ITweakOperation>> Operations { get; init; }

    /// <summary>Runs after apply and revert, e.g. to broadcast a setting change.</summary>
    public Action? AfterChange { get; init; }
}
