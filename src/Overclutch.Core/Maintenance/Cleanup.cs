using Overclutch.Core.Native;

namespace Overclutch.Core.Maintenance;

public static class Cleanup
{
    private static readonly EnumerationOptions AllFiles = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    /// <summary>
    /// Size in bytes of the given files, and of everything inside the given folders. Files written or created less
    /// than <paramref name="minAge"/> ago are left out.
    /// </summary>
    public static long Measure(IEnumerable<string> targets, TimeSpan minAge = default)
    {
        var cutoff = Cutoff(minAge);
        long size = 0;
        foreach (var target in targets)
        {
            try
            {
                if (File.Exists(target) && new FileInfo(target) is var file && IsOld(file, cutoff))
                    size += file.Length;
                else if (Directory.Exists(target))
                    size += new DirectoryInfo(target).EnumerateFiles("*", AllFiles).Where(f => IsOld(f, cutoff)).Sum(f => f.Length);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Vanished or protected while measuring: it will be skipped when cleaning too.
            }
        }
        return size;
    }

    /// <summary>
    /// Deletes the given files and empties the given folders (the folders themselves stay), then returns the freed
    /// size in bytes. Files in use, and files written or created less than <paramref name="minAge"/> ago, are skipped.
    /// </summary>
    public static long Clean(IEnumerable<string> targets, TimeSpan minAge = default)
    {
        var cutoff = Cutoff(minAge);
        long freed = 0;
        foreach (var target in targets)
        {
            if (File.Exists(target) && new FileInfo(target) is var file && IsOld(file, cutoff))
                freed += TryDelete(file);
            else if (Directory.Exists(target))
                freed += CleanFolder(new DirectoryInfo(target), cutoff);
        }
        return freed;
    }

    public static bool FlushDns() => NativeMethods.DnsFlushResolverCache();

    public static long RecycleBinSize()
    {
        var info = new NativeMethods.ShQueryRbInfo { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.ShQueryRbInfo>() };
        return NativeMethods.SHQueryRecycleBin(null, ref info) == 0 ? info.TotalBytes : 0;
    }

    public static long EmptyRecycleBin()
    {
        var size = RecycleBinSize();
        if (size == 0)
            return 0;
        var result = NativeMethods.SHEmptyRecycleBin(0, null,
            NativeMethods.SherbNoConfirmation | NativeMethods.SherbNoProgressUi | NativeMethods.SherbNoSound);
        if (result != 0)
            throw new InvalidOperationException($"Windows a refusé de vider la corbeille (0x{result:X8}).");
        return size;
    }

    public static long PurgeStandbyMemory()
    {
        var before = StandbyMemory.Size();
        StandbyMemory.Purge();
        return Math.Max(0, before - StandbyMemory.Size());
    }

    /// <summary>Runs <paramref name="work"/> with a service stopped, so it releases its files, then restarts it
    /// if it was running.</summary>
    public static long WithServiceStopped(string serviceName, Func<long> work)
    {
        // A service that refuses to stop keeps its files: they are skipped as files in use.
        var wasRunning = WindowsServices.Pause(serviceName, TimeSpan.FromSeconds(30));
        try
        {
            return work();
        }
        finally
        {
            if (wasRunning)
                WindowsServices.Resume(serviceName);
        }
    }

    private static DateTime Cutoff(TimeSpan minAge) => minAge > TimeSpan.Zero ? DateTime.UtcNow - minAge : DateTime.MaxValue;

    // Creation time counts too: an installer extracting an archive keeps the files' old write times. A folder's
    // write time moves whenever its content changes, emptying it included, so only its creation time is used.
    private static bool IsOld(FileSystemInfo entry, DateTime cutoff) =>
        entry.CreationTimeUtc < cutoff && (entry is DirectoryInfo || entry.LastWriteTimeUtc < cutoff);

    private static long CleanFolder(DirectoryInfo folder, DateTime cutoff)
    {
        long freed = 0;
        foreach (var entry in SafeEnumerate(folder))
        {
            // Never follow junctions out of the folder being cleaned.
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                continue;

            if (entry is FileInfo file)
            {
                if (IsOld(file, cutoff))
                    freed += TryDelete(file);
            }
            else if (entry is DirectoryInfo dir)
            {
                freed += CleanFolder(dir, cutoff);
                try
                {
                    dir.Refresh();
                    if (IsOld(dir, cutoff) && !dir.EnumerateFileSystemInfos().Any())
                        dir.Delete();
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // In use or protected: leave it.
                }
            }
        }
        return freed;
    }

    private static long TryDelete(FileInfo file)
    {
        try
        {
            var size = file.Length;
            if (file.IsReadOnly)
                file.IsReadOnly = false;
            file.Delete();
            return size;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // In use or protected: leave it.
            return 0;
        }
    }

    private static IEnumerable<FileSystemInfo> SafeEnumerate(DirectoryInfo folder)
    {
        try { return folder.EnumerateFileSystemInfos().ToList(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; }
    }
}
