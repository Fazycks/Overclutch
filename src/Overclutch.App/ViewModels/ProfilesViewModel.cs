using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Overclutch.App.Services;
using Overclutch.Core.GameMode;
using Overclutch.Core.Maintenance;
using Overclutch.Core.Profiles;
using Overclutch.Core.Settings;
using Overclutch.Core.Tweaks;
using Wpf.Ui.Controls;

namespace Overclutch.App.ViewModels;

public sealed record ProfileItem(Profile Profile)
{
    public string Name => Profile.Name;

    public string Summary
    {
        get
        {
            var parts = new List<string>
            {
                Profile.Tweaks.Count == 1 ? "1 réglage" : $"{Profile.Tweaks.Count} réglages",
                $"mode jeu {ProfilesViewModel.LevelTitle(Profile.GameModeLevel)}",
            };
            if (Profile.ExtraGames.Count > 0)
                parts.Add(Profile.ExtraGames.Count == 1 ? "1 jeu en plus" : $"{Profile.ExtraGames.Count} jeux en plus");
            return string.Join(" · ", parts);
        }
    }
}

public sealed partial class ProfilesViewModel : ObservableObject
{
    private static AppServices Services => AppServices.Instance;
    private static AppSettings Settings => Services.Settings;

    public ProfilesViewModel()
    {
        Items = [.. Settings.Profiles.Select(p => new ProfileItem(p))];
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
    }

    public ObservableCollection<ProfileItem> Items { get; }

    public bool IsEmpty => Items.Count == 0;

    [ObservableProperty]
    public partial string NewName { get; set; } = "";

    [ObservableProperty]
    public partial string CodeText { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCurrentCommand), nameof(ImportCommand), nameof(ApplyCommand), nameof(DeleteCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial string? BusyText { get; private set; }

    [ObservableProperty]
    public partial bool HasResult { get; set; }

    [ObservableProperty]
    public partial string? ResultTitle { get; private set; }

    [ObservableProperty]
    public partial string? ResultMessage { get; private set; }

    [ObservableProperty]
    public partial InfoBarSeverity ResultSeverity { get; private set; }

    private bool CanRun() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task SaveCurrentAsync() => RunAsync(async () =>
    {
        BusyText = "Lecture de ta configuration…";
        var name = NewName.Trim() is { Length: > 0 } typed ? typed : "Ma configuration";
        var profile = await Task.Run(() => Profile.Capture(name, Services.Engine, Settings));
        var item = Add(profile);
        NewName = "";
        ShowResult(InfoBarSeverity.Success, "Profil enregistré", $"« {item.Name} » : {item.Summary}.");
    });

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void Import()
    {
        HasResult = false;
        try
        {
            var item = Add(ShareCode.Decode(CodeText));
            CodeText = "";
            ShowResult(InfoBarSeverity.Success, "Profil importé",
                $"« {item.Name} » est dans ta liste. Rien n'a changé sur ton PC : « Appliquer » montre son contenu avant de l'utiliser.");
        }
        catch (FormatException e)
        {
            ShowResult(InfoBarSeverity.Error, "Code refusé", e.Message);
        }
    }

    [RelayCommand]
    private void CopyCode(ProfileItem item)
    {
        try
        {
            Clipboard.SetDataObject(ShareCode.Encode(item.Profile), true);
            ShowResult(InfoBarSeverity.Success, "Code copié", $"Colle-le à qui tu veux : il contient tout le profil « {item.Name} », sans aucune info sur ton PC.");
        }
        catch (ExternalException)
        {
            ShowResult(InfoBarSeverity.Error, "Copie impossible", "Une autre application bloque le presse-papiers. Réessaie dans un instant.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task DeleteAsync(ProfileItem item)
    {
        if (!await Dialogs.ConfirmAsync("Supprimer le profil",
                $"Supprimer « {item.Name} » de ta liste ? Les réglages déjà appliqués ne changent pas.", "Supprimer"))
            return;

        Items.Remove(item);
        Save();
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task ApplyAsync(ProfileItem item) => RunAsync(async () =>
    {
        BusyText = "Comparaison avec ton PC…";
        var profile = item.Profile;
        var plan = await Task.Run(() => profile.Plan(Services.Engine, Settings));
        if (!await Dialogs.ConfirmScrollingAsync($"Appliquer « {profile.Name} »", Describe(profile, plan), "Appliquer"))
            return;

        var failures = (IReadOnlyList<TweakFailure>)[];
        if (plan.ToApply.Count > 0)
        {
            BusyText = "Création d'un point de restauration…";
            if (await Task.Run(() => RestorePoint.Create("Overclutch - avant profil")) is { } reason
                && !await Dialogs.ConfirmAsync("Point de restauration impossible",
                    $"{reason}\n\nContinuer quand même ? Chaque réglage restera annulable depuis Overclutch.",
                    "Continuer"))
                return;

            BusyText = "Application du profil…";
            failures = await Task.Run(() => Services.Engine.ApplyMany(plan.ToApply));
            Services.NotifyTweaksChanged(plan.ToApply.Except(failures.Select(f => f.Tweak)));
        }

        profile.ApplySettingsTo(Settings);
        Settings.Save();
        Services.NotifyGameModeSettingsChanged();

        var summary = $"« {profile.Name} » est appliqué : {plan.ToApply.Count - failures.Count} réglage(s) modifié(s).";
        if (failures.Count == 0)
            ShowResult(InfoBarSeverity.Success, "Terminé", summary);
        else
            ShowResult(InfoBarSeverity.Warning, "Terminé avec des erreurs",
                summary + "\n" + string.Join("\n", failures.Select(f => $"• {f.Tweak.Title} : {f.Message}")));
    });

    public static string LevelTitle(GameModeLevel level) => level switch
    {
        GameModeLevel.Performance => "Performance",
        GameModeLevel.Custom => "Personnalisé",
        _ => "Équilibré",
    };

    private static string Describe(Profile profile, ProfilePlan plan)
    {
        var text = new StringBuilder();
        text.AppendLine(plan.ToApply.Count switch
        {
            0 => "Aucun réglage à modifier.",
            1 => "1 réglage à appliquer.",
            var count => $"{count} réglages à appliquer.",
        });

        // First, so that a long list of safe tweaks cannot push the warnings out of sight.
        if (plan.Advanced.Any())
        {
            text.AppendLine().AppendLine("Réglages avancés, à lire avant de continuer :");
            foreach (var tweak in plan.Advanced)
                text.AppendLine($"• {tweak.Title} : {tweak.Warning}");
        }

        var safe = plan.ToApply.Where(t => t.Risk == TweakRisk.Safe).ToList();
        if (safe.Count > 0)
        {
            text.AppendLine().AppendLine("Réglages sûrs :");
            foreach (var tweak in safe)
                text.AppendLine($"• {tweak.Title}");
        }

        text.AppendLine().AppendLine($"Mode jeu : niveau {LevelTitle(profile.GameModeLevel)}.");
        if (plan.NewGames.Count > 0)
            text.AppendLine($"Ajoutés aux jeux : {string.Join(", ", plan.NewGames)}.");
        if (plan.NewBackgroundApps.Count > 0)
            text.AppendLine($"Ajoutées aux applis à calmer : {string.Join(", ", plan.NewBackgroundApps)}.");

        text.AppendLine();
        if (plan.AlreadyActive > 0)
            text.AppendLine($"Déjà actifs : {plan.AlreadyActive}.");
        if (plan.Unavailable.Count > 0)
            text.AppendLine($"Non disponibles sur ce PC, ignorés : {string.Join(", ", plan.Unavailable.Select(t => t.Title))}.");
        if (plan.UnknownTweaks > 0)
            text.AppendLine($"Ignorés car créés avec une version plus récente d'Overclutch : {plan.UnknownTweaks}.");
        text.Append("Les réglages hors du profil ne changent pas, et tout reste annulable.");
        return text.ToString();
    }

    private ProfileItem Add(Profile profile)
    {
        var item = new ProfileItem(profile with { Name = UniqueName(profile.Name) });
        Items.Add(item);
        Save();
        return item;
    }

    private void Save()
    {
        Settings.Profiles = [.. Items.Select(i => i.Profile)];
        Settings.Save();
    }

    private string UniqueName(string name)
    {
        var unique = name;
        for (var i = 2; Items.Any(item => string.Equals(item.Name, unique, StringComparison.OrdinalIgnoreCase)); i++)
            unique = $"{name} ({i})";
        return unique;
    }

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
        }
    }

    private void ShowResult(InfoBarSeverity severity, string title, string message)
    {
        ResultSeverity = severity;
        ResultTitle = title;
        ResultMessage = message;
        HasResult = true;
    }
}
