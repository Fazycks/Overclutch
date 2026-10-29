using System.Runtime.InteropServices;

namespace Overclutch.Core.Native;

/// <summary>Full path of the executable a process runs.</summary>
public static class ProcessImage
{
    public static string? PathOf(int pid) => FromHandle(pid) ?? FromKernel(pid);

    private static string? FromHandle(int pid)
    {
        var handle = NativeMethods.OpenProcess(NativeMethods.ProcessQueryLimitedInformation, false, pid);
        if (handle == 0)
            return null;
        try
        {
            var buffer = new char[1024];
            var size = (uint)buffer.Length;
            return NativeMethods.QueryFullProcessImageName(handle, 0, buffer, ref size) ? new string(buffer, 0, (int)size) : null;
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    /// <summary>
    /// Anti-cheats refuse to open their game, even just to read its name. The kernel still gives it out from the
    /// process ID alone, as a device path (\Device\HarddiskVolume3\...).
    /// </summary>
    private static unsafe string? FromKernel(int pid)
    {
        const int capacity = 1024;
        var buffer = stackalloc char[capacity];
        var info = new ProcessIdInformation
        {
            ProcessId = pid,
            ImageName = new UnicodeString { MaximumLength = capacity * sizeof(char), Buffer = (nint)buffer },
        };
        if (NativeMethods.NtQuerySystemInformation(NativeMethods.SystemProcessIdInformation, &info, sizeof(ProcessIdInformation), out _) != 0)
            return null;
        return ToDrivePath(new string(buffer, 0, info.ImageName.Length / sizeof(char)));
    }

    private static string? ToDrivePath(string devicePath)
    {
        var target = new char[512];
        foreach (var drive in Environment.GetLogicalDrives())
        {
            var letter = drive.TrimEnd('\\');
            if (NativeMethods.QueryDosDevice(letter, target, (uint)target.Length) == 0)
                continue;
            var device = new string(target).Split('\0')[0];
            if (devicePath.StartsWith(device + '\\', StringComparison.OrdinalIgnoreCase))
                return letter + devicePath[device.Length..];
        }
        return null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public nint Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessIdInformation
    {
        public nint ProcessId;
        public UnicodeString ImageName;
    }
}
