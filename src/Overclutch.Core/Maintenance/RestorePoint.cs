using System.Management;
using Microsoft.Win32;
using Overclutch.Core.Tweaks;

namespace Overclutch.Core.Maintenance;

public static class RestorePoint
{
    private const string FrequencyKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
    private const string FrequencyValue = "SystemRestorePointCreationFrequency";
    private const uint ModifySettings = 12;
    private const uint BeginSystemChange = 100;

    /// <summary>Creates a System Restore point. Returns null on success, otherwise a reason.</summary>
    public static string? Create(string description)
    {
        // Windows silently skips a restore point if one was made in the last 24 h; lift that for this call.
        var frequency = RegistryBackup.Read(RegistryHive.LocalMachine, FrequencyKey, FrequencyValue);
        try
        {
            RegistryValueOp.Hklm(FrequencyKey, FrequencyValue, 0).Apply();

            using var restore = new ManagementClass(@"\\.\root\default", "SystemRestore", null);
            using var args = restore.GetMethodParameters("CreateRestorePoint");
            args["Description"] = description;
            args["RestorePointType"] = ModifySettings;
            args["EventType"] = BeginSystemChange;
            using var result = restore.InvokeMethod("CreateRestorePoint", args, null);
            var code = Convert.ToUInt32(result["ReturnValue"]);
            return code switch
            {
                0 => null,
                1058 => "La protection du système est désactivée sur ce PC.",
                _ => $"Windows a refusé de créer le point de restauration (code {code}).",
            };
        }
        catch (ManagementException e)
        {
            return $"Point de restauration impossible : {e.Message}";
        }
        finally
        {
            frequency.Restore();
        }
    }
}
