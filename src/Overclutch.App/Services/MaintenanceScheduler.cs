using System.Windows.Threading;
using Overclutch.Core.Maintenance;

namespace Overclutch.App.Services;

/// <summary>
/// Runs the checked maintenance tasks on the chosen schedule, never during a game, and makes sure two cleanups
/// never overlap whether they come from the page or from the schedule.
/// </summary>
public sealed class MaintenanceScheduler
{
    // A first look shortly after startup, then a quarter-hourly check: a missed day is caught up within minutes.
    private static readonly TimeSpan FirstCheck = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);

    private readonly AppServices _services;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly DispatcherTimer _timer;

    public MaintenanceScheduler(AppServices services)
    {
        _services = services;
        _timer = new DispatcherTimer { Interval = FirstCheck };
        _timer.Tick += async (_, _) =>
        {
            _timer.Interval = CheckInterval;
            await RunIfDueAsync();
        };
    }

    /// <summary>Raised on the UI thread after the selection ran, by hand or on schedule.</summary>
    public event Action? Ran;

    public void Start() => _timer.Start();

    /// <summary>Runs one task off the UI thread, after any cleanup already in progress.</summary>
    public async Task<long> RunAsync(MaintenanceTask task)
    {
        await _gate.WaitAsync();
        try
        {
            return await Task.Run(task.Run);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void MarkRun()
    {
        _services.Settings.LastMaintenanceRun = DateTime.Now;
        _services.Settings.Save();
        Ran?.Invoke();
    }

    private async Task RunIfDueAsync()
    {
        var settings = _services.Settings;
        if (!MaintenanceRunner.IsDue(settings.MaintenanceSchedule, settings.LastMaintenanceRun, DateTime.Now)
            || _services.GameMode.Current is not null)
            return;

        var tasks = settings.MaintenanceTasks.Select(MaintenanceCatalog.Find).OfType<MaintenanceTask>().ToList();
        if (tasks.Count == 0 || !await _gate.WaitAsync(0))
            return;

        MaintenanceReport report;
        try
        {
            report = await Task.Run(() => MaintenanceRunner.RunAll(tasks));
        }
        finally
        {
            _gate.Release();
        }

        MarkRun();
        App.Current.NotifyTray("Entretien automatique terminé",
            $"{SizeText.Format(report.Freed)} libérés."
            + (report.Failures.Count > 0 ? $" {report.Failures.Count} tâche(s) en échec, détails dans Entretien." : ""));
    }
}
