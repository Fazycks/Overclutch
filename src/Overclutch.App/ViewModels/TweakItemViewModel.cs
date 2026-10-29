using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Overclutch.App.Services;
using Overclutch.Core.Tweaks;

namespace Overclutch.App.ViewModels;

public sealed partial class TweakItemViewModel(Tweak tweak) : ObservableObject
{
    private static TweakEngine Engine => AppServices.Instance.Engine;

    public Tweak Tweak => tweak;
    public string Title => tweak.Title;
    public string Description => tweak.Description;
    public string? Warning => tweak.Warning;
    public bool RequiresReboot => tweak.RequiresReboot;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOn), nameof(CanToggle), nameof(StatusText))]
    public partial TweakState State { get; private set; } = TweakState.NotApplied;

    /// <summary>Overclutch made the change and holds the original values, so it can undo it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggle), nameof(StatusText))]
    public partial bool HasBackup { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggle))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial string? Error { get; private set; }

    public bool IsOn => State == TweakState.Applied;

    /// <summary>
    /// Already set this way before Overclutch touched it: there is nothing to undo, so the switch is locked on.
    /// </summary>
    private bool AlreadyNative => State == TweakState.Applied && !HasBackup;

    public bool CanToggle => !IsBusy && State != TweakState.Unavailable && !AlreadyNative;

    public string? StatusText =>
        Error is not null ? $"Échec : {Error}"
        : State == TweakState.Unavailable ? "Non disponible sur ce PC"
        : AlreadyNative ? "Déjà réglé ainsi sur ton PC"
        : State == TweakState.Partial ? "Partiellement appliqué"
        : null;

    /// <summary>Reads the live state. Blocking (registry, bcdedit, NVIDIA driver): call off the UI thread.</summary>
    public (TweakState State, bool HasBackup) Read() => (Engine.GetState(tweak), Engine.CanRevert(tweak));

    public void Show((TweakState State, bool HasBackup) read)
    {
        State = read.State;
        HasBackup = read.HasBackup;
        // The switch flips itself on click; re-push the real value even when it did not change.
        OnPropertyChanged(nameof(IsOn));
    }

    public async Task RefreshAsync() => Show(await Task.Run(Read));

    [RelayCommand]
    private async Task ToggleAsync()
    {
        IsBusy = true;
        Error = null;
        var revert = IsOn;
        try
        {
            await Task.Run(() =>
            {
                if (revert)
                    Engine.Revert(tweak);
                else
                    Engine.Apply(tweak);
            });
            AppServices.Instance.NotifyTweaksChanged([tweak]);
        }
        catch (Exception e)
        {
            Error = e.Message;
        }
        finally
        {
            IsBusy = false;
            await RefreshAsync();
        }
    }
}
