using System.ServiceProcess;
using Microsoft.Win32;
using Overclutch.Core.Native;

namespace Overclutch.Core.Tweaks;

/// <summary>One atomic system change that a tweak is made of.</summary>
public interface ITweakOperation
{
    /// <summary>False when the target does not exist on this PC (missing service, no NVIDIA GPU...).</summary>
    bool IsAvailable { get; }

    bool IsApplied { get; }

    BackupEntry Capture();

    void Apply();
}

public sealed class RegistryValueOp(RegistryHive hive, string path, string name, object value,
    RegistryValueKind kind = RegistryValueKind.DWord) : ITweakOperation
{
    public bool IsAvailable => true;

    public bool IsApplied => Equals(RegistryAccess.Read(hive, path, name), value);

    public BackupEntry Capture() => RegistryBackup.Read(hive, path, name);

    public void Apply()
    {
        using var key = RegistryAccess.Create(hive, path);
        key.SetValue(name, value, kind);
    }

    public static RegistryValueOp Hklm(string path, string name, int value) => new(RegistryHive.LocalMachine, path, name, value);

    public static RegistryValueOp Hkcu(string path, string name, object value, RegistryValueKind kind = RegistryValueKind.DWord) =>
        new(RegistryHive.CurrentUser, path, name, value, kind);
}

/// <summary>Sets a service's start type (applies at next boot) and stops it right away when disabling.</summary>
public sealed class ServiceStartOp(string serviceName, ServiceStartMode startMode) : ITweakOperation
{
    private string KeyPath => $@"SYSTEM\CurrentControlSet\Services\{serviceName}";
    private RegistryValueOp StartValue => RegistryValueOp.Hklm(KeyPath, "Start", (int)startMode);

    public bool IsAvailable => RegistryAccess.KeyExists(RegistryHive.LocalMachine, KeyPath);

    public bool IsApplied => StartValue.IsApplied;

    public BackupEntry Capture() => StartValue.Capture();

    public void Apply()
    {
        StartValue.Apply();
        if (startMode != ServiceStartMode.Disabled)
            return;
        try
        {
            using var service = new ServiceController(serviceName);
            if (service.Status != ServiceControllerStatus.Stopped && service.CanStop)
                service.Stop();
        }
        catch (InvalidOperationException)
        {
            // Some services refuse to stop while in use; the start type still applies after a reboot.
        }
    }
}

public sealed class PowerPlanOp : ITweakOperation
{
    public bool IsAvailable => true;

    public bool IsApplied => PowerPlans.GetName(PowerPlans.GetActive()) == PowerPlans.OverclutchPlanName;

    public BackupEntry Capture() => new PowerSchemeBackup(PowerPlans.GetActive());

    public void Apply() => PowerPlans.SetActive(PowerPlans.EnsureOverclutchPlan());
}

/// <summary>Writes an AC value of the active power plan.</summary>
public sealed class PowerSettingOp(Guid subgroup, Guid setting, uint value) : ITweakOperation
{
    public bool IsAvailable => true;

    public bool IsApplied => PowerPlans.ReadAc(PowerPlans.GetActive(), subgroup, setting) == value;

    public BackupEntry Capture()
    {
        var scheme = PowerPlans.GetActive();
        return new PowerSettingBackup(scheme, subgroup, setting, PowerPlans.ReadAc(scheme, subgroup, setting));
    }

    public void Apply() => PowerPlans.WriteAc(PowerPlans.GetActive(), subgroup, setting, value);
}

/// <summary>Sets a boot element, or removes it when <paramref name="value"/> is null.</summary>
public sealed class BcdOp(string element, string? value) : ITweakOperation
{
    public bool IsAvailable => true;

    public bool IsApplied
    {
        get
        {
            var current = Bcd.Read(element);
            if (value is null)
                return current is null;
            return Bcd.IsYes(value) ? Bcd.IsYes(current) : string.Equals(current, value, StringComparison.OrdinalIgnoreCase);
        }
    }

    public BackupEntry Capture() => new BcdBackup(element, Bcd.ToWritable(Bcd.Read(element)));

    public void Apply()
    {
        if (value is null)
            Bcd.Delete(element);
        else
            Bcd.Set(element, value);
    }
}

public sealed class NvidiaSettingOp(uint settingId, uint value) : ITweakOperation
{
    public bool IsAvailable => NvidiaDrs.IsAvailable;

    public bool IsApplied => NvidiaDrs.Read(settingId) == value;

    public BackupEntry Capture() => new NvidiaBackup(settingId, NvidiaDrs.Read(settingId));

    public void Apply() => NvidiaDrs.Write(settingId, value);
}

/// <summary>Disables a Task Scheduler task.</summary>
public sealed class ScheduledTaskOp(string path) : ITweakOperation
{
    public bool IsAvailable => ScheduledTasks.IsEnabled(path) is not null;

    public bool IsApplied => ScheduledTasks.IsEnabled(path) == false;

    public BackupEntry Capture() => new ScheduledTaskBackup(path, ScheduledTasks.IsEnabled(path) ?? true);

    public void Apply() => ScheduledTasks.SetEnabled(path, enabled: false);
}
