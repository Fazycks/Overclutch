namespace Overclutch.Core;

public static class AppPaths
{
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Overclutch");
}
