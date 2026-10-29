namespace Overclutch.Core.Tweaks;

public sealed record TweakFailure(Tweak Tweak, string Message);

public sealed class TweakEngine(BackupStore store)
{
    public BackupStore Store => store;

    public TweakState GetState(Tweak tweak)
    {
        var ops = AvailableOperations(tweak);
        if (ops.Count == 0)
            return TweakState.Unavailable;

        var applied = ops.Count(op => op.IsApplied);
        return applied == ops.Count ? TweakState.Applied
            : applied == 0 ? TweakState.NotApplied
            : TweakState.Partial;
    }

    /// <summary>True when Overclutch changed this tweak and holds the original values.</summary>
    public bool CanRevert(Tweak tweak) => store.Has(tweak.Id);

    public void Apply(Tweak tweak)
    {
        var ops = AvailableOperations(tweak);
        if (ops.Count == 0)
            throw new InvalidOperationException("Ce réglage n'est pas disponible sur ce PC.");

        // Keep the very first capture: re-applying must not overwrite the true original values,
        // but targets that appeared since (a new network adapter...) still get backed up.
        var entries = store.Get(tweak.Id)?.ToList() ?? [];
        var known = entries.Select(e => e.Target).ToHashSet();
        var captured = ops.Select(op => op.Capture()).Where(e => known.Add(e.Target)).ToList();
        if (captured.Count > 0 || entries.Count == 0)
            store.Set(tweak.Id, [.. entries, .. captured]);

        foreach (var op in ops.Where(op => !op.IsApplied))
            op.Apply();

        tweak.AfterChange?.Invoke();
    }

    public void Revert(Tweak tweak)
    {
        if (store.Get(tweak.Id) is not { } entries)
            return;

        foreach (var entry in entries.Reverse())
            entry.Restore();

        store.Remove(tweak.Id);
        tweak.AfterChange?.Invoke();
    }

    public IReadOnlyList<TweakFailure> ApplyMany(IEnumerable<Tweak> tweaks) => Run(tweaks, Apply);

    public IReadOnlyList<TweakFailure> RevertMany(IEnumerable<Tweak> tweaks) => Run(tweaks, Revert);

    private static List<TweakFailure> Run(IEnumerable<Tweak> tweaks, Action<Tweak> action)
    {
        var failures = new List<TweakFailure>();
        foreach (var tweak in tweaks)
        {
            try
            {
                action(tweak);
            }
            catch (Exception e)
            {
                failures.Add(new TweakFailure(tweak, e.Message));
            }
        }
        return failures;
    }

    private static List<ITweakOperation> AvailableOperations(Tweak tweak) =>
        [.. tweak.Operations().Where(op => op.IsAvailable)];
}
