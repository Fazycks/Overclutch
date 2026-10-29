using System.Runtime.InteropServices;

namespace Overclutch.Core.Native;

internal static partial class NativeMethods
{
    // ---- powrprof.dll ----

    [LibraryImport("powrprof.dll")]
    internal static partial uint PowerGetActiveScheme(nint userRootPowerKey, out nint activePolicyGuid);

    [LibraryImport("powrprof.dll")]
    internal static partial uint PowerSetActiveScheme(nint userRootPowerKey, in Guid schemeGuid);

    [LibraryImport("powrprof.dll")]
    internal static partial uint PowerDeleteScheme(nint rootPowerKey, in Guid schemeGuid);

    [LibraryImport("powrprof.dll")]
    internal static partial uint PowerDuplicateScheme(nint rootPowerKey, in Guid sourceSchemeGuid, ref nint destinationSchemeGuid);

    [LibraryImport("powrprof.dll")]
    internal static partial uint PowerWriteFriendlyName(nint rootPowerKey, in Guid schemeGuid, nint subGroupOfPowerSettingsGuid,
        nint powerSettingGuid, [MarshalAs(UnmanagedType.LPArray)] byte[] buffer, uint bufferSize);

    [LibraryImport("powrprof.dll")]
    internal static partial uint PowerReadFriendlyName(nint rootPowerKey, in Guid schemeGuid, nint subGroupOfPowerSettingsGuid,
        nint powerSettingGuid, [MarshalAs(UnmanagedType.LPArray)] byte[]? buffer, ref uint bufferSize);

    [LibraryImport("powrprof.dll")]
    internal static partial uint PowerEnumerate(nint rootPowerKey, nint schemeGuid, nint subGroupOfPowerSettingsGuid,
        uint accessFlags, uint index, out Guid buffer, ref uint bufferSize);

    [LibraryImport("powrprof.dll")]
    internal static partial uint PowerReadACValueIndex(nint rootPowerKey, in Guid schemeGuid, in Guid subGroupOfPowerSettingsGuid,
        in Guid powerSettingGuid, out uint acValueIndex);

    [LibraryImport("powrprof.dll")]
    internal static partial uint PowerWriteACValueIndex(nint rootPowerKey, in Guid schemeGuid, in Guid subGroupOfPowerSettingsGuid,
        in Guid powerSettingGuid, uint acValueIndex);

    internal const uint AccessScheme = 16;

    [LibraryImport("kernel32.dll")]
    internal static partial nint LocalFree(nint hMem);

    // ---- processes ----

    internal const uint ProcessQueryLimitedInformation = 0x1000;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial nint OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(nint handle);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool QueryFullProcessImageName(nint process, uint flags, [Out] char[] exeName, ref uint size);

    // ---- memory ----

    [StructLayout(LayoutKind.Sequential)]
    internal struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    // ---- misc ----

    internal const uint SpiSetMouse = 0x0004;
    internal const uint SpifUpdateIniFile = 0x01;
    internal const uint SpifSendChange = 0x02;

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SystemParametersInfo(uint action, uint param, [In] int[] vparam, uint winIni);

    [LibraryImport("dnsapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DnsFlushResolverCache();
}
