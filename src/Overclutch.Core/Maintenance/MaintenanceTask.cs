using System.Globalization;

namespace Overclutch.Core.Maintenance;

public enum MaintenanceCategory
{
    Disk,
    Games,
    System,
}

public enum MaintenanceSchedule
{
    Never,
    Daily,
    Weekly,
}

/// <summary>A one-shot cleanup. Unlike a tweak there is nothing to revert: it only removes caches and leftovers.</summary>
public sealed class MaintenanceTask
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public string? Warning { get; init; }
    public required MaintenanceCategory Category { get; init; }

    /// <summary>Bytes the task would free right now; null when that cannot be known beforehand.</summary>
    public Func<long>? Measure { get; init; }

    /// <summary>Runs the task and returns the bytes it freed.</summary>
    public required Func<long> Run { get; init; }
}

public sealed record MaintenanceFailure(MaintenanceTask Task, string Message);

public sealed record MaintenanceReport(long Freed, IReadOnlyList<MaintenanceFailure> Failures);

public static class MaintenanceRunner
{
    public static MaintenanceReport RunAll(IEnumerable<MaintenanceTask> tasks)
    {
        long freed = 0;
        var failures = new List<MaintenanceFailure>();
        foreach (var task in tasks)
        {
            try
            {
                freed += task.Run();
            }
            catch (Exception e)
            {
                failures.Add(new MaintenanceFailure(task, e.Message));
            }
        }
        return new MaintenanceReport(freed, failures);
    }

    public static bool IsDue(MaintenanceSchedule schedule, DateTime? lastRun, DateTime now) => schedule switch
    {
        MaintenanceSchedule.Daily => lastRun is not { } last || now - last >= TimeSpan.FromDays(1),
        MaintenanceSchedule.Weekly => lastRun is not { } last || now - last >= TimeSpan.FromDays(7),
        _ => false,
    };
}

public static class SizeText
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private const double Mb = 1024d * 1024;
    private const double Gb = Mb * 1024;

    public static string Format(long bytes) => bytes switch
    {
        <= 0 => "0 Mo",
        < (long)Mb => "< 1 Mo",
        < (long)Gb => (bytes / Mb).ToString("N0", French) + " Mo",
        _ => (bytes / Gb).ToString("0.0", French) + " Go",
    };
}
