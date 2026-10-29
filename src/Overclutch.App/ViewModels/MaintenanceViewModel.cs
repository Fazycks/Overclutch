using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Overclutch.App.Services;
using Overclutch.Core.Maintenance;
using Overclutch.Core.Settings;
using Wpf.Ui.Controls;

namespace Overclutch.App.ViewModels;

public sealed record MaintenanceGroup(string Title, SymbolRegular Icon, IReadOnlyList<MaintenanceItemViewModel> Items);

public sealed partial class MaintenanceViewModel : ObservableObject
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private static AppSettings Settings => AppServices.Instance.Settings;

    private readonly List<MaintenanceItemViewModel> _items;

    public MaintenanceViewModel()
    {
        Groups =
        [
            .. MaintenanceCatalog.All
                .GroupBy(t => t.Category)
                .OrderBy(g => g.Key)
                .Select(g => new MaintenanceGroup(CategoryTitle(g.Key), CategoryIcon(g.Key),
                    [.. g.Select(t => new MaintenanceItemViewModel(t, Settings.MaintenanceTasks.Contains(t.Id), OnSelectionChanged))])),
        ];
        _items = [.. Groups.SelectMany(g => g.Items)];
        foreach (var item in _items)
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MaintenanceItemViewModel.Size))
                    OnPropertyChanged(nameof(TotalText));
            };
        AppServices.Instance.Maintenance.Ran += () =>
        {
            OnPropertyChanged(nameof(LastRunText));
            if (!IsBusy)
                _ = AnalyzeAsync();
        };
    }

    public IReadOnlyList<MaintenanceGroup> Groups { get; }

    public IReadOnlyList<string> ScheduleOptions { get; } = ["Jamais", "Chaque jour", "Chaque semaine"];

    public int ScheduleIndex
    {
        get => (int)Settings.MaintenanceSchedule;
        set
        {
            Settings.MaintenanceSchedule = (MaintenanceSchedule)value;
            Settings.Save();
            OnPropertyChanged();
        }
    }

    public string LastRunText => Settings.LastMaintenanceRun is { } last
        ? "Dernier entretien : " + last.ToString("dddd d MMMM 'à' HH:mm", French)
        : "Aucun entretien lancé pour l'instant.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalText))]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand), nameof(RunSelectionCommand))]
    public partial bool IsAnalyzing { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand), nameof(RunSelectionCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial bool HasResult { get; set; }

    [ObservableProperty]
    public partial string? ResultTitle { get; private set; }

    [ObservableProperty]
    public partial string? ResultMessage { get; private set; }

    [ObservableProperty]
    public partial InfoBarSeverity ResultSeverity { get; private set; }

    public string TotalText => IsAnalyzing
        ? "…"
        : SizeText.Format(_items.Where(i => i.IsSelected).Sum(i => i.Size ?? 0));

    private bool CanRun() => !IsBusy && !IsAnalyzing;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task AnalyzeAsync()
    {
        IsAnalyzing = true;
        try
        {
            foreach (var item in _items)
                item.BeginMeasure();
            // One pass in the background: the folders are walked one after the other, not all at once.
            var sizes = await Task.Run(() => _items.Select(i => i.Measure()).ToList());
            for (var i = 0; i < _items.Count; i++)
                _items[i].ShowSize(sizes[i]);
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    private bool CanRunSelection() => CanRun() && _items.Any(i => i.IsSelected);

    [RelayCommand(CanExecute = nameof(CanRunSelection))]
    private async Task RunSelectionAsync()
    {
        IsBusy = true;
        HasResult = false;
        try
        {
            long freed = 0;
            var failed = new List<MaintenanceItemViewModel>();
            foreach (var item in _items.Where(i => i.IsSelected).ToList())
            {
                if (await item.RunAsync() is { } bytes)
                    freed += bytes;
                else
                    failed.Add(item);
            }
            AppServices.Instance.Maintenance.MarkRun();

            var summary = $"{SizeText.Format(freed)} libérés. Les fichiers encore utilisés ont été laissés en place.";
            if (failed.Count == 0)
                ShowResult(InfoBarSeverity.Success, "Entretien terminé", summary);
            else
                ShowResult(InfoBarSeverity.Warning, "Terminé avec des erreurs",
                    summary + "\n" + string.Join("\n", failed.Select(i => $"• {i.Title} : {i.StatusText}")));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnSelectionChanged()
    {
        Settings.MaintenanceTasks = [.. _items.Where(i => i.IsSelected).Select(i => i.Id)];
        Settings.Save();
        OnPropertyChanged(nameof(TotalText));
        RunSelectionCommand.NotifyCanExecuteChanged();
    }

    private void ShowResult(InfoBarSeverity severity, string title, string message)
    {
        ResultSeverity = severity;
        ResultTitle = title;
        ResultMessage = message;
        HasResult = true;
    }

    private static string CategoryTitle(MaintenanceCategory category) => category switch
    {
        MaintenanceCategory.Disk => "Fichiers inutiles",
        MaintenanceCategory.Games => "Jeux et carte graphique",
        MaintenanceCategory.System => "Mémoire et réseau",
        _ => category.ToString(),
    };

    private static SymbolRegular CategoryIcon(MaintenanceCategory category) => category switch
    {
        MaintenanceCategory.Disk => SymbolRegular.Delete24,
        MaintenanceCategory.Games => SymbolRegular.Games24,
        MaintenanceCategory.System => SymbolRegular.DeveloperBoard24,
        _ => SymbolRegular.Broom24,
    };
}
