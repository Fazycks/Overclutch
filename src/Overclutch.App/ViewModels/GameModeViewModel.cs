using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Overclutch.App.Services;
using Overclutch.Core.GameMode;
using Overclutch.Core.Settings;

namespace Overclutch.App.ViewModels;

public sealed partial class GameModeViewModel : ObservableObject
{
    private static AppServices Services => AppServices.Instance;
    private static AppSettings Settings => Services.Settings;

    public GameModeViewModel()
    {
        ExtraGamesText = string.Join(Environment.NewLine, Settings.ExtraGames);
        BackgroundAppsText = string.Join(Environment.NewLine, Settings.BackgroundApps);

        Services.GameModeEnabledChanged += () => OnPropertyChanged(nameof(Enabled));
        Services.GameMode.GameStarted += _ => Application.Current.Dispatcher.BeginInvoke(UpdateSession);
        Services.GameMode.GameEnded += _ => Application.Current.Dispatcher.BeginInvoke(UpdateSession);
        UpdateSession();
    }

    public bool Enabled
    {
        get => Settings.GameModeEnabled;
        set => Services.SetGameModeEnabled(value);
    }

    public bool BoostPowerPlan
    {
        get => Settings.GameModeBoostPowerPlan;
        set
        {
            Settings.GameModeBoostPowerPlan = value;
            Settings.Save();
            OnPropertyChanged();
        }
    }

    public bool LowerBackgroundApps
    {
        get => Settings.GameModeLowerBackgroundApps;
        set
        {
            Settings.GameModeLowerBackgroundApps = value;
            Settings.Save();
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    public partial string ExtraGamesText { get; set; }

    [ObservableProperty]
    public partial string BackgroundAppsText { get; set; }

    [ObservableProperty]
    public partial string? SavedText { get; private set; }

    [ObservableProperty]
    public partial GameSession? Session { get; private set; }

    public string SessionTitle => Session is { } s ? $"En jeu : {s.Name}" : "Aucun jeu en cours";

    public string SessionDetail => Session is { } s
        ? $"Depuis {s.StartedAt:HH:mm} · {string.Join(" · ", s.Actions)}"
        : "Overclutch surveille le lancement de tes jeux (Steam, Epic, Riot, EA, Ubisoft, GOG, Xbox, Rockstar et ta liste perso).";

    partial void OnSessionChanged(GameSession? value)
    {
        OnPropertyChanged(nameof(SessionTitle));
        OnPropertyChanged(nameof(SessionDetail));
    }

    partial void OnExtraGamesTextChanged(string value) => SavedText = null;

    partial void OnBackgroundAppsTextChanged(string value) => SavedText = null;

    [RelayCommand]
    private void SaveLists()
    {
        Settings.ExtraGames = ParseList(ExtraGamesText);
        Settings.BackgroundApps = ParseList(BackgroundAppsText);
        Settings.Save();
        ExtraGamesText = string.Join(Environment.NewLine, Settings.ExtraGames);
        BackgroundAppsText = string.Join(Environment.NewLine, Settings.BackgroundApps);
        SavedText = "Listes enregistrées.";
    }

    private void UpdateSession() => Session = Services.GameMode.Current;

    /// <summary>One executable per line or comma; ".exe" is optional.</summary>
    private static List<string> ParseList(string text) =>
    [
        .. text.Split(['\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(GameDetector.TrimExe)
            .Distinct(StringComparer.OrdinalIgnoreCase),
    ];
}
