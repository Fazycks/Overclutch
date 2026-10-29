using Overclutch.Core.Native;

namespace Overclutch.Core.Maintenance;

public static class Cleanup
{
    /// <summary>Deletes what can be deleted in the temp folders and returns the freed size in bytes.
    /// Files in use are skipped.</summary>
    public static long CleanTempFiles()
    {
        string[] folders =
        [
            Path.GetTempPath(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
        ];

        long freed = 0;
        foreach (var folder in folders.Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists))
            freed += CleanFolder(new DirectoryInfo(folder));
        return freed;
    }

    public static bool FlushDns() => NativeMethods.DnsFlushResolverCache();

    private static long CleanFolder(DirectoryInfo folder)
    {
        long freed = 0;
        foreach (var entry in SafeEnumerate(folder))
        {
            try
            {
                // Never follow junctions out of the temp folder.
                if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    continue;

                if (entry is FileInfo file)
                {
                    var size = file.Length;
                    file.Delete();
                    freed += size;
                }
                else if (entry is DirectoryInfo dir)
                {
                    freed += CleanFolder(dir);
                    if (!dir.EnumerateFileSystemInfos().Any())
                        dir.Delete();
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // In use or protected: leave it.
            }
        }
        return freed;
    }

    private static IEnumerable<FileSystemInfo> SafeEnumerate(DirectoryInfo folder)
    {
        try { return folder.EnumerateFileSystemInfos().ToList(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; }
    }
}
