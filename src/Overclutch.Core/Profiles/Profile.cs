using Overclutch.Core.GameMode;
using Overclutch.Core.Settings;
using Overclutch.Core.Tweaks;

namespace Overclutch.Core.Profiles;

/// <summary>
/// A configuration that can be saved and shared. It names tweaks by id and never holds what they change, so a
/// code from someone else can only switch on what the catalog of this version already offers.
/// </summary>
public sealed record Profile
{
    public string Name { get; init; } = "";

    public List<string> Tweaks { get; init; } = [];

    public GameModeLevel GameModeLevel { get; init; } = GameModeLevel.Balanced;

    public GameModeOptions GameModeCustom { get; init; } = GameModeOptions.Balanced;

    public List<string> ExtraGames { get; init; } = [];

    public List<string> BackgroundApps { get; init; } = [];

    /// <summary>
    /// The current configuration. Reads every tweak's state: call off the UI thread. An advanced tweak only counts
    /// when Overclutch changed it, not when Windows merely happens to be set that way.
    /// </summary>
    public static Profile Capture(string name, TweakEngine engine, AppSettings settings, IEnumerable<Tweak>? catalog = null) => new()
    {
        Name = name,
        Tweaks =
        [
            .. (catalog ?? TweakCatalog.All)
                .Where(t => engine.CanRevert(t) || (t.Risk == TweakRisk.Safe && engine.GetState(t) == TweakState.Applied))
                .Select(t => t.Id),
        ],
        GameModeLevel = settings.GameModeLevel,
        GameModeCustom = settings.GameModeCustom,
        ExtraGames = [.. settings.ExtraGames],
        BackgroundApps = [.. settings.BackgroundApps],
    };

    /// <summary>What applying this profile would do on this PC. Reads tweak states: call off the UI thread.</summary>
    public ProfilePlan Plan(TweakEngine engine, AppSettings settings, IEnumerable<Tweak>? catalog = null)
    {
        var known = (catalog ?? TweakCatalog.All).ToDictionary(t => t.Id);
        var ids = Tweaks.Distinct().ToList();
        var states = ids.Where(known.ContainsKey).Select(id => known[id]).ToLookup(engine.GetState);

        return new ProfilePlan(
            [.. states[TweakState.NotApplied], .. states[TweakState.Partial]],
            [.. states[TweakState.Unavailable]],
            states[TweakState.Applied].Count(),
            ids.Count(id => !known.ContainsKey(id)),
            [.. ExtraGames.Except(settings.ExtraGames, StringComparer.OrdinalIgnoreCase)],
            [.. BackgroundApps.Except(settings.BackgroundApps, StringComparer.OrdinalIgnoreCase)]);
    }

    /// <summary>Takes the game mode level and adds to the lists: a profile never removes one of your entries.</summary>
    public void ApplySettingsTo(AppSettings settings)
    {
        settings.GameModeLevel = GameModeLevel;
        settings.GameModeCustom = GameModeCustom;
        settings.ExtraGames = [.. settings.ExtraGames.Union(ExtraGames, StringComparer.OrdinalIgnoreCase)];
        settings.BackgroundApps = [.. settings.BackgroundApps.Union(BackgroundApps, StringComparer.OrdinalIgnoreCase)];
    }
}

/// <param name="ToApply">Tweaks of the profile not active yet.</param>
/// <param name="Unavailable">Tweaks this PC cannot take (no NVIDIA GPU...).</param>
/// <param name="AlreadyActive">Tweaks of the profile that need nothing.</param>
/// <param name="UnknownTweaks">Ids this version does not know: the code comes from a newer one.</param>
/// <param name="NewGames">Entries added to the extra games.</param>
/// <param name="NewBackgroundApps">Entries added to the background apps.</param>
public sealed record ProfilePlan(
    IReadOnlyList<Tweak> ToApply,
    IReadOnlyList<Tweak> Unavailable,
    int AlreadyActive,
    int UnknownTweaks,
    IReadOnlyList<string> NewGames,
    IReadOnlyList<string> NewBackgroundApps)
{
    public IEnumerable<Tweak> Advanced => ToApply.Where(t => t.Risk == TweakRisk.Advanced);
}
