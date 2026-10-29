using Overclutch.Core.Native;

namespace Overclutch.Core.Settings;

/// <summary>Starts Overclutch at logon through a scheduled task, which can run elevated without a UAC prompt.</summary>
public static class Autostart
{
    private const string TaskName = "Overclutch";

    private static string Schtasks => Path.Combine(Environment.SystemDirectory, "schtasks.exe");

    public static bool IsEnabled => ProcessRunner.Run(Schtasks, $"/query /tn {TaskName}").ExitCode == 0;

    public static void Enable(string exePath)
    {
        var (code, output) = ProcessRunner.Run(Schtasks,
            $"/create /f /tn {TaskName} /sc onlogon /rl highest /tr \"\\\"{exePath}\\\" --tray\"");
        if (code != 0)
            throw new InvalidOperationException($"Impossible de créer la tâche de démarrage : {output.Trim()}");
    }

    public static void Disable()
    {
        if (IsEnabled)
            ProcessRunner.Run(Schtasks, $"/delete /f /tn {TaskName}");
    }
}
