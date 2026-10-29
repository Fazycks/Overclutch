using System.ComponentModel;
using System.Diagnostics;
using Overclutch.Core.Native;

namespace Overclutch.Core.Settings;

/// <summary>Starts Overclutch at logon through a scheduled task, which can run elevated without a UAC prompt.</summary>
public static class Autostart
{
    private const string TaskName = "Overclutch";
    private const int UninstallTimeoutMs = 20_000;

    private static string Schtasks => Path.Combine(Environment.SystemDirectory, "schtasks.exe");

    // The uninstall hook runs without admin rights and cannot see a task created elevated,
    // so this file (readable by every user) tells it whether there is a task to remove.
    private static string MarkerPath => Path.Combine(AppPaths.DataDirectory, "autostart");

    public static bool IsEnabled => ProcessRunner.Run(Schtasks, $"/query /tn {TaskName}").ExitCode == 0;

    public static void Enable(string exePath)
    {
        var (code, output) = ProcessRunner.Run(Schtasks,
            $"/create /f /tn {TaskName} /sc onlogon /rl highest /tr \"\\\"{exePath}\\\" --tray\"");
        if (code != 0)
            throw new InvalidOperationException($"Impossible de créer la tâche de démarrage : {output.Trim()}");
        SetMarker(true);
    }

    public static void Disable()
    {
        if (IsEnabled)
            ProcessRunner.Run(Schtasks, $"/delete /f /tn {TaskName}");
        SetMarker(false);
    }

    /// <summary>Aligns the marker with the real task, which versions before 0.1.1 did not record.</summary>
    public static void SyncMarker() => SetMarker(IsEnabled);

    /// <summary>
    /// Removes the task when the app is uninstalled. Runs without elevation, so it asks for it,
    /// and only when the marker says a task exists.
    /// </summary>
    public static void RemoveForUninstall()
    {
        if (!File.Exists(MarkerPath))
            return;

        var start = new ProcessStartInfo(Schtasks, $"/delete /f /tn {TaskName}")
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        try
        {
            using var process = Process.Start(start);
            process?.WaitForExit(UninstallTimeoutMs);
        }
        catch (Win32Exception)
        {
            // UAC declined: the task stays, pointing at a missing executable, and does nothing at logon.
        }
    }

    private static void SetMarker(bool enabled)
    {
        if (enabled)
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            File.WriteAllText(MarkerPath, "");
        }
        else
        {
            File.Delete(MarkerPath);
        }
    }
}
