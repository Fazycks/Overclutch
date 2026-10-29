using System.Runtime.InteropServices;
using System.Text;

namespace Overclutch.Core.Monitoring;

/// <summary>NVIDIA Management Library (nvml.dll ships with the driver in System32): usage and temperature without a kernel driver.</summary>
internal static partial class Nvml
{
    private const string Dll = "nvml.dll";

    [StructLayout(LayoutKind.Sequential)]
    internal struct Utilization
    {
        public uint Gpu;
        public uint Memory;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Memory
    {
        public ulong Total;
        public ulong Free;
        public ulong Used;
    }

    [LibraryImport(Dll, EntryPoint = "nvmlInit_v2")]
    internal static partial int Init();

    [LibraryImport(Dll, EntryPoint = "nvmlShutdown")]
    internal static partial int Shutdown();

    [LibraryImport(Dll, EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
    internal static partial int GetHandleByIndex(uint index, out nint device);

    [LibraryImport(Dll, EntryPoint = "nvmlDeviceGetName")]
    internal static partial int GetName(nint device, [Out] byte[] name, uint length);

    [LibraryImport(Dll, EntryPoint = "nvmlDeviceGetTemperature")]
    internal static partial int GetTemperature(nint device, int sensor, out uint celsius);

    [LibraryImport(Dll, EntryPoint = "nvmlDeviceGetUtilizationRates")]
    internal static partial int GetUtilization(nint device, out Utilization utilization);

    [LibraryImport(Dll, EntryPoint = "nvmlDeviceGetMemoryInfo")]
    internal static partial int GetMemory(nint device, out Memory memory);

    [LibraryImport(Dll, EntryPoint = "nvmlDeviceGetClockInfo")]
    internal static partial int GetClock(nint device, int clockType, out uint mhz);

    internal const int ClockGraphics = 0;

    internal static string? ReadName(nint device)
    {
        var buffer = new byte[96];
        if (GetName(device, buffer, (uint)buffer.Length) != 0)
            return null;
        return Encoding.ASCII.GetString(buffer).TrimEnd('\0');
    }
}
