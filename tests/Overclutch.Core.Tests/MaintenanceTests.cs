using Overclutch.Core.Maintenance;

namespace Overclutch.Core.Tests;

public sealed class MaintenanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "overclutch-tests-" + Guid.NewGuid().ToString("N"));

    public MaintenanceTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Catalog_ids_are_unique_and_default_selection_exists()
    {
        var ids = MaintenanceCatalog.All.Select(t => t.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(new Settings.AppSettings().MaintenanceTasks, id => Assert.NotNull(MaintenanceCatalog.Find(id)));
    }

    [Fact]
    public void Cleaning_empties_folders_but_keeps_them()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "cache"));
        File.WriteAllBytes(Path.Combine(folder.FullName, "a.bin"), new byte[1000]);
        var sub = folder.CreateSubdirectory("nested");
        File.WriteAllBytes(Path.Combine(sub.FullName, "b.bin"), new byte[500]);
        var loose = Path.Combine(_root, "loose.dmp");
        File.WriteAllBytes(loose, new byte[200]);
        string[] targets = [folder.FullName, loose, Path.Combine(_root, "missing")];

        Assert.Equal(1700, Cleanup.Measure(targets));
        Assert.Equal(1700, Cleanup.Clean(targets));

        Assert.True(folder.Exists);
        Assert.Empty(folder.EnumerateFileSystemInfos());
        Assert.False(File.Exists(loose));
        Assert.Equal(0, Cleanup.Measure(targets));
    }

    [Fact]
    public void Cleaning_skips_files_in_use()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "busy"));
        var path = Path.Combine(folder.FullName, "locked.log");
        File.WriteAllBytes(path, new byte[300]);
        File.WriteAllBytes(Path.Combine(folder.FullName, "free.log"), new byte[100]);

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.Equal(100, Cleanup.Clean([folder.FullName]));

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Cleaning_with_a_minimum_age_keeps_recent_files()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "temp"));
        var old = Path.Combine(folder.FullName, "old.tmp");
        var recent = Path.Combine(folder.FullName, "recent.tmp");
        var extracted = Path.Combine(folder.FullName, "extracted.msi");
        File.WriteAllBytes(old, new byte[400]);
        File.WriteAllBytes(recent, new byte[50]);
        File.WriteAllBytes(extracted, new byte[70]);
        var twoDaysAgo = DateTime.UtcNow.AddDays(-2);
        File.SetCreationTimeUtc(old, twoDaysAgo);
        File.SetLastWriteTimeUtc(old, twoDaysAgo);
        // Unpacked just now from an archive: old write time, new creation time.
        File.SetLastWriteTimeUtc(extracted, twoDaysAgo);
        var oldEmptyDir = folder.CreateSubdirectory("old-empty");
        oldEmptyDir.CreationTimeUtc = twoDaysAgo;
        var newEmptyDir = folder.CreateSubdirectory("new-empty");
        var minAge = TimeSpan.FromDays(1);

        Assert.Equal(400, Cleanup.Measure([folder.FullName], minAge));
        Assert.Equal(400, Cleanup.Clean([folder.FullName], minAge));

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(extracted));
        Assert.False(Directory.Exists(oldEmptyDir.FullName));
        Assert.True(Directory.Exists(newEmptyDir.FullName));
    }

    [Fact]
    public void Run_all_keeps_going_after_a_failure()
    {
        MaintenanceTask Task(string id, Func<long> run) =>
            new() { Id = id, Title = id, Description = "", Category = MaintenanceCategory.Disk, Run = run };

        var report = MaintenanceRunner.RunAll(
        [
            Task("a", () => 10),
            Task("b", () => throw new InvalidOperationException("boom")),
            Task("c", () => 5),
        ]);

        Assert.Equal(15, report.Freed);
        var failure = Assert.Single(report.Failures);
        Assert.Equal("b", failure.Task.Id);
        Assert.Equal("boom", failure.Message);
    }

    [Theory]
    [InlineData(MaintenanceSchedule.Never, null, 100, false)]
    [InlineData(MaintenanceSchedule.Daily, null, 0, true)]
    [InlineData(MaintenanceSchedule.Daily, 0, 23, false)]
    [InlineData(MaintenanceSchedule.Daily, 0, 24, true)]
    [InlineData(MaintenanceSchedule.Weekly, 0, 24 * 6, false)]
    [InlineData(MaintenanceSchedule.Weekly, 0, 24 * 7, true)]
    public void Schedule_is_due_after_its_interval(MaintenanceSchedule schedule, int? lastRunHour, int nowHour, bool due)
    {
        var origin = new DateTime(2026, 1, 1);
        DateTime? lastRun = lastRunHour is { } h ? origin.AddHours(h) : null;
        Assert.Equal(due, MaintenanceRunner.IsDue(schedule, lastRun, origin.AddHours(nowHour)));
    }

    [Theory]
    [InlineData(0, "0 Mo")]
    [InlineData(1000, "< 1 Mo")]
    [InlineData(5L * 1024 * 1024, "5 Mo")]
    [InlineData(3L * 1024 * 1024 * 1024 / 2, "1,5 Go")]
    public void Sizes_read_in_french(long bytes, string expected) => Assert.Equal(expected, SizeText.Format(bytes));
}
