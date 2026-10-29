using Microsoft.Win32;

namespace Overclutch.Core.Native;

public static class RegistryAccess
{
    public static RegistryKey? Open(RegistryHive hive, string path, bool writable)
    {
        using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        return root.OpenSubKey(path, writable);
    }

    public static RegistryKey Create(RegistryHive hive, string path)
    {
        using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        return root.CreateSubKey(path, writable: true);
    }

    public static bool KeyExists(RegistryHive hive, string path)
    {
        using var key = Open(hive, path, writable: false);
        return key is not null;
    }

    public static object? Read(RegistryHive hive, string path, string name)
    {
        using var key = Open(hive, path, writable: false);
        return key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
    }

    public static IReadOnlyList<string> SubKeyNames(RegistryHive hive, string path)
    {
        using var key = Open(hive, path, writable: false);
        return key?.GetSubKeyNames() ?? [];
    }
}
