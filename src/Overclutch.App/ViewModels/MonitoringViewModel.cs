using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Overclutch.Core.Monitoring;

namespace Overclutch.App.ViewModels;

public sealed partial class MonitoringViewModel : ObservableObject
{
    /// <summary>Samples kept for the graph: one minute at one reading per second.</summary>
    public const int HistoryLength = 60;
    public const double GraphWidth = 600;
    public const double GraphHeight = 100;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Queue<double> _cpuHistory = new();
    private readonly Queue<double> _gpuHistory = new();
    private SystemMonitor? _monitor;
    private bool _reading;

    public MonitoringViewModel() => _timer.Tick += async (_, _) => await TickAsync();

    [ObservableProperty]
    public partial MonitorSnapshot? Snapshot { get; private set; }

    [ObservableProperty]
    public partial string? GpuName { get; private set; }

    [ObservableProperty]
    public partial PointCollection CpuGraph { get; private set; } = [];

    [ObservableProperty]
    public partial PointCollection GpuGraph { get; private set; } = [];

    public bool HasGpu => Snapshot?.GpuPercent is not null;

    public string CpuText => Snapshot is { } s ? $"{s.CpuPercent:N0} %" : "—";
    public double CpuPercent => Snapshot?.CpuPercent ?? 0;

    public string RamText => Snapshot is { } s ? $"{s.RamUsedGb:N1} / {s.RamTotalGb:N0} Go" : "—";
    public double RamPercent => Snapshot is { RamTotalGb: > 0 } s ? s.RamUsedGb * 100 / s.RamTotalGb : 0;

    public string GpuText => Snapshot?.GpuPercent is { } g ? $"{g:N0} %" : "—";
    public double GpuPercent => Snapshot?.GpuPercent ?? 0;

    public string GpuTempText => Snapshot?.GpuTempC is { } t ? $"{t:N0} °C" : "—";
    public double GpuTempPercent => Math.Min(100, Snapshot?.GpuTempC ?? 0);

    public string VramText => Snapshot is { VramUsedGb: { } used, VramTotalGb: { } total } ? $"{used:N1} / {total:N0} Go" : "—";
    public double VramPercent => Snapshot is { VramUsedGb: { } used, VramTotalGb: > 0 and var total } ? used * 100 / total : 0;

    public string GpuClockText => Snapshot?.GpuClockMhz is { } c ? $"{c:N0} MHz" : "—";

    /// <summary>Only sample while the page is on screen.</summary>
    public void Start()
    {
        _timer.Start();
        _ = TickAsync();
    }

    public void Stop() => _timer.Stop();

    private async Task TickAsync()
    {
        if (_reading)
            return;
        _reading = true;
        try
        {
            var first = _monitor is null;
            _monitor ??= await Task.Run(() => new SystemMonitor());
            if (first)
                GpuName = _monitor.GpuName;

            Snapshot = await Task.Run(_monitor.Read);
            Push(_cpuHistory, Snapshot.CpuPercent);
            Push(_gpuHistory, Snapshot.GpuPercent ?? 0);
            CpuGraph = ToPoints(_cpuHistory);
            GpuGraph = ToPoints(_gpuHistory);
        }
        finally
        {
            _reading = false;
        }
    }

    partial void OnSnapshotChanged(MonitorSnapshot? value)
    {
        foreach (var name in new[]
                 {
                     nameof(HasGpu), nameof(CpuText), nameof(CpuPercent), nameof(RamText), nameof(RamPercent),
                     nameof(GpuText), nameof(GpuPercent), nameof(GpuTempText), nameof(GpuTempPercent),
                     nameof(VramText), nameof(VramPercent), nameof(GpuClockText),
                 })
            OnPropertyChanged(name);
    }

    private static void Push(Queue<double> history, double value)
    {
        history.Enqueue(Math.Clamp(value, 0, 100));
        while (history.Count > HistoryLength)
            history.Dequeue();
    }

    /// <summary>Right-aligned so the newest reading is always at the right edge.</summary>
    private static PointCollection ToPoints(Queue<double> history)
    {
        var step = GraphWidth / (HistoryLength - 1);
        var offset = HistoryLength - history.Count;
        var points = new PointCollection(history.Select((v, i) =>
            new Point((offset + i) * step, GraphHeight - v / 100 * GraphHeight)));
        points.Freeze();
        return points;
    }
}
