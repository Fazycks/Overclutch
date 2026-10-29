using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Overclutch.App.Services;
using Overclutch.Core.Maintenance;

namespace Overclutch.App.ViewModels;

public sealed partial class MaintenanceItemViewModel : ObservableObject
{
    private readonly MaintenanceTask _task;
    private readonly Action? _selectionChanged;

    public MaintenanceItemViewModel(MaintenanceTask task, bool isSelected, Action selectionChanged)
    {
        _task = task;
        IsSelected = isSelected;
        // Set after the initial value, so only the user's clicks are reported.
        _selectionChanged = selectionChanged;
    }

    public string Id => _task.Id;
    public string Title => _task.Title;
    public string Description => _task.Description;
    public string? Warning => _task.Warning;
    public bool IsMeasurable => _task.Measure is not null;

    /// <summary>Checked: part of « Lancer la sélection » and of the automatic maintenance.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Bytes the task would free; null while measuring, when unknown, or when not measurable.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SizeText))]
    public partial long? Size { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SizeText))]
    public partial bool IsMeasuring { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    public string? SizeText =>
        !IsMeasurable ? null
        : IsMeasuring ? "…"
        : Size is { } size ? Core.Maintenance.SizeText.Format(size)
        : "—";

    partial void OnIsSelectedChanged(bool value) => _selectionChanged?.Invoke();

    /// <summary>Blocking (walks folders): call off the UI thread.</summary>
    public long? Measure()
    {
        try
        {
            return _task.Measure?.Invoke();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task RefreshAsync()
    {
        if (!IsMeasurable)
            return;
        IsMeasuring = true;
        Size = await Task.Run(Measure);
        IsMeasuring = false;
    }

    public void BeginMeasure() => IsMeasuring = IsMeasurable;

    public void ShowSize(long? size)
    {
        Size = size;
        IsMeasuring = false;
    }

    /// <summary>Runs the task and shows the outcome. Returns the freed bytes, or null when it failed.</summary>
    public async Task<long?> RunAsync()
    {
        IsBusy = true;
        StatusText = "En cours…";
        try
        {
            var freed = await AppServices.Instance.Maintenance.RunAsync(_task);
            StatusText = IsMeasurable ? $"✓ {Core.Maintenance.SizeText.Format(freed)} libérés" : "✓ Terminé";
            return freed;
        }
        catch (Exception e)
        {
            StatusText = $"Échec : {e.Message}";
            return null;
        }
        finally
        {
            IsBusy = false;
            await RefreshAsync();
        }
    }

    private bool CanRun() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task Run() => RunAsync();
}
