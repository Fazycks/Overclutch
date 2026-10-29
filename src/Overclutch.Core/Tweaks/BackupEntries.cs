using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using Overclutch.Core.Native;

namespace Overclutch.Core.Tweaks;

/// <summary>
/// The original state of one thing a tweak changed. Entries are self-describing so a revert never
/// depends on the tweak definition that created them (which may have changed between versions).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RegistryBackup), "registry")]
[JsonDerivedType(typeof(PowerSchemeBackup), "powerScheme")]
[JsonDerivedType(typeof(PowerSettingBackup), "powerSetting")]
[JsonDerivedType(typeof(BcdBackup), "bcd")]
[JsonDerivedType(typeof(NvidiaBackup), "nvidia")]
[JsonDerivedType(typeof(ScheduledTaskBackup), "scheduledTask")]
public abstract record BackupEntry
{
    /// <summary>Identifies what was changed, so a re-apply only captures targets not backed up yet.</summary>
    [JsonIgnore]
    public abstract string Target { get; }

    public abstract void Restore();
}

/// <summary><see cref="Kind"/> is null when the value did not exist.</summary>
public sealed record RegistryBackup(RegistryHive Hive, string Path, string Name, RegistryValueKind? Kind, string? Data) : BackupEntry
{
    public override string Target => $@"reg:{Hive}\{Path}\{Name}";

    public static RegistryBackup Read(RegistryHive hive, string path, string name)
    {
        using var key = RegistryAccess.Open(hive, path, writable: false);
        if (key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not { } value)
            return new(hive, path, name, null, null);

        var kind = key.GetValueKind(name);
        var data = value switch
        {
            int i => i.ToString(CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture),
            string s => s,
            string[] multi => JsonSerializer.Serialize(multi),
            byte[] bytes => Convert.ToBase64String(bytes),
            _ => value.ToString(),
        };
        return new(hive, path, name, kind, data);
    }

    public override void Restore()
    {
        if (Kind is not { } kind)
        {
            using var existing = RegistryAccess.Open(Hive, Path, writable: true);
            existing?.DeleteValue(Name, throwOnMissingValue: false);
            return;
        }

        object value = kind switch
        {
            RegistryValueKind.DWord => int.Parse(Data!, CultureInfo.InvariantCulture),
            RegistryValueKind.QWord => long.Parse(Data!, CultureInfo.InvariantCulture),
            RegistryValueKind.MultiString => JsonSerializer.Deserialize<string[]>(Data!)!,
            RegistryValueKind.Binary => Convert.FromBase64String(Data!),
            _ => Data!,
        };
        using var key = RegistryAccess.Create(Hive, Path);
        key.SetValue(Name, value, kind);
    }
}

public sealed record PowerSchemeBackup(Guid Scheme) : BackupEntry
{
    public override string Target => "powerScheme";

    public override void Restore()
    {
        if (PowerPlans.Exists(Scheme))
            PowerPlans.SetActive(Scheme);
        PowerPlans.DeleteOverclutchPlan();
    }
}

public sealed record PowerSettingBackup(Guid Scheme, Guid Subgroup, Guid Setting, uint AcValue) : BackupEntry
{
    public override string Target => $"power:{Subgroup}/{Setting}";

    public override void Restore()
    {
        if (PowerPlans.Exists(Scheme))
            PowerPlans.WriteAc(Scheme, Subgroup, Setting, AcValue);
    }
}

/// <summary><see cref="Value"/> is null when the element was not set.</summary>
public sealed record BcdBackup(string Element, string? Value) : BackupEntry
{
    public override string Target => $"bcd:{Element}";

    public override void Restore()
    {
        if (Value is null)
            Bcd.Delete(Element);
        else
            Bcd.Set(Element, Bcd.ToWritable(Value)!);
    }
}

/// <summary><see cref="Value"/> is null when the setting was at its driver default.</summary>
public sealed record NvidiaBackup(uint SettingId, uint? Value) : BackupEntry
{
    public override string Target => $"nvidia:{SettingId:X8}";

    public override void Restore() => NvidiaDrs.Write(SettingId, Value);
}

public sealed record ScheduledTaskBackup(string Path, bool Enabled) : BackupEntry
{
    public override string Target => $"task:{Path}";

    public override void Restore()
    {
        // The task may have been removed by a Windows update since.
        if (ScheduledTasks.IsEnabled(Path) is { } current && current != Enabled)
            ScheduledTasks.SetEnabled(Path, Enabled);
    }
}
