using System.Xml.Linq;

namespace Overclutch.Core.Native;

/// <summary>Enables and disables Task Scheduler tasks through schtasks.</summary>
public static class ScheduledTasks
{
    private static readonly XNamespace TaskNamespace = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    private static string Exe => Path.Combine(Environment.SystemDirectory, "schtasks.exe");

    /// <summary>Returns null when the task does not exist.</summary>
    public static bool? IsEnabled(string path)
    {
        // The XML export is not localized, unlike the status column of the normal output.
        var (code, output) = ProcessRunner.Run(Exe, $"/query /tn \"{path}\" /xml");
        return code == 0 ? ParseEnabled(output) : null;
    }

    /// <summary>Reads Settings/Enabled from a task definition; it is omitted when the task is enabled.</summary>
    public static bool ParseEnabled(string xml)
    {
        var enabled = XDocument.Parse(xml).Root?.Element(TaskNamespace + "Settings")?.Element(TaskNamespace + "Enabled");
        return enabled is null || bool.Parse(enabled.Value);
    }

    public static void SetEnabled(string path, bool enabled)
    {
        var arguments = $"/change /tn \"{path}\" {(enabled ? "/enable" : "/disable")}";
        var (code, output) = ProcessRunner.Run(Exe, arguments);
        if (code != 0)
            throw new InvalidOperationException($"schtasks {arguments} a échoué : {output.Trim()}");
    }
}
