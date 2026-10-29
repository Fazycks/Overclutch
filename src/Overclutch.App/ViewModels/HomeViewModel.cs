using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Overclutch.App.Services;
using Overclutch.Core.Maintenance;
using Overclutch.Core.Monitoring;
using Overclutch.Core.Tweaks;
using Wpf.Ui.Controls;

namespace Overclutch.App.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    private static TweakEngine Engine => AppServices.Instance.Engine;

    public AppServices Services => AppServices.Instance;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Score), nameof(ScoreText))]
    public partial int AppliedCount { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Score), nameof(ScoreText))]
    public partial int AvailableCount { get; private set; }

    public int Score => AvailableCount == 0 ? 0 : AppliedCount * 100 / AvailableCount;

    public string ScoreText => $"{AppliedCount} / {AvailableCount} optimisations actives";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OptimizeCommand), nameof(RestoreAllCommand), nameof(CleanTempCommand), nameof(FlushDnsCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial string? BusyText { get; private set; }

    [ObservableProperty]
    public partial HardwareInfo? Hardware { get; private set; }

    [ObservableProperty]
    public partial bool HasResult { get; set; }

    [ObservableProperty]
    public partial string? ResultTitle { get; private set; }

    [ObservableProperty]
    public partial string? ResultMessage { get; private set; }

    [ObservableProperty]
    public partial InfoBarSeverity ResultSeverity { get; private set; }

    public async Task LoadAsync()
    {
        Hardware ??= await Task.Run(() => Services.Hardware.Value);
        await RefreshScoreAsync();
    }

    private async Task RefreshScoreAsync()
    {
        var states = await Task.Run(() => TweakCatalog.Profile.Select(Engine.GetState).ToList());
        AvailableCount = states.Count(s => s != TweakState.Unavailable);
        AppliedCount = states.Count(s => s == TweakState.Applied);
    }

    private bool CanRun() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task OptimizeAsync()
    {
        if (!await Dialogs.ConfirmAsync("Optimiser mon PC",
                "Overclutch va créer un point de restauration Windows, puis appliquer toutes les optimisations sûres.\n\n" +
                "Les valeurs d'origine sont sauvegardées : tu pourras tout annuler depuis l'accueil, ou réglage par réglage.",
                "Optimiser"))
            return;

        await RunAsync(async () =>
        {
            BusyText = "Création d'un point de restauration…";
            if (await Task.Run(() => RestorePoint.Create("Overclutch - avant optimisation")) is { } reason
                && !await Dialogs.ConfirmAsync("Point de restauration impossible",
                    $"{reason}\n\nContinuer quand même ? Chaque réglage restera annulable depuis Overclutch.",
                    "Continuer"))
                return;

            BusyText = "Application des optimisations…";
            var targets = await Task.Run(() => TweakCatalog.Profile
                .Where(t => Engine.GetState(t) is TweakState.NotApplied or TweakState.Partial)
                .ToList());
            if (targets.Count == 0)
            {
                ShowResult(InfoBarSeverity.Success, "Déjà optimisé", "Toutes les optimisations disponibles sont déjà actives.");
                return;
            }

            var failures = await Task.Run(() => Engine.ApplyMany(targets));
            Services.NotifyTweaksChanged(targets.Except(failures.Select(f => f.Tweak)));
            ReportFailures(failures, $"{targets.Count - failures.Count} optimisation(s) appliquée(s).");
        });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RestoreAllAsync()
    {
        var targets = TweakCatalog.All.Where(Engine.CanRevert).ToList();
        if (targets.Count == 0)
        {
            ShowResult(InfoBarSeverity.Informational, "Rien à restaurer", "Overclutch n'a modifié aucun réglage sur ce PC.");
            return;
        }

        if (!await Dialogs.ConfirmAsync("Tout restaurer",
                $"Remettre les valeurs d'origine des {targets.Count} réglage(s) modifié(s) par Overclutch ?", "Restaurer"))
            return;

        await RunAsync(async () =>
        {
            BusyText = "Restauration des réglages d'origine…";
            var failures = await Task.Run(() => Engine.RevertMany(targets));
            Services.NotifyTweaksChanged(targets);
            ReportFailures(failures, $"{targets.Count - failures.Count} réglage(s) restauré(s).");
        });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task CleanTempAsync() => RunAsync(async () =>
    {
        BusyText = "Nettoyage des fichiers temporaires…";
        var freed = await Task.Run(Cleanup.CleanTempFiles);
        ShowResult(InfoBarSeverity.Success, "Nettoyage terminé",
            $"{freed / 1024d / 1024:N0} Mo libérés. Les fichiers encore utilisés ont été laissés en place.");
    });

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task FlushDnsAsync() => RunAsync(async () =>
    {
        var ok = await Task.Run(Cleanup.FlushDns);
        if (ok)
            ShowResult(InfoBarSeverity.Success, "Cache DNS vidé", "Les prochaines connexions résoudront les adresses à neuf.");
        else
            ShowResult(InfoBarSeverity.Error, "Échec", "Windows a refusé de vider le cache DNS.");
    });

    private async Task RunAsync(Func<Task> work)
    {
        IsBusy = true;
        HasResult = false;
        try
        {
            await work();
        }
        catch (Exception e)
        {
            ShowResult(InfoBarSeverity.Error, "Erreur", e.Message);
        }
        finally
        {
            IsBusy = false;
            BusyText = null;
            await RefreshScoreAsync();
        }
    }

    private void ReportFailures(IReadOnlyList<TweakFailure> failures, string summary)
    {
        if (failures.Count == 0)
            ShowResult(InfoBarSeverity.Success, "Terminé", summary);
        else
            ShowResult(InfoBarSeverity.Warning, "Terminé avec des erreurs",
                summary + "\n" + string.Join("\n", failures.Select(f => $"• {f.Tweak.Title} : {f.Message}")));
    }

    private void ShowResult(InfoBarSeverity severity, string title, string message)
    {
        ResultSeverity = severity;
        ResultTitle = title;
        ResultMessage = message;
        HasResult = true;
    }
}
