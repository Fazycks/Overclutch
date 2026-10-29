using System.Diagnostics;
using Overclutch.Core.Native;

namespace Overclutch.Core.Monitoring;

public sealed record MonitorSnapshot(
    double CpuPercent,
    double RamUsedGb,
    double RamTotalGb,
    double? GpuPercent,
    double? GpuTempC,
    double? VramUsedGb,
    double? VramTotalGb,
    double? GpuClockMhz);

/// <summary>Live usage readings without any kernel driver: Windows counters for CPU/RAM, NVML for the GPU.</summary>
public sealed class SystemMonitor : IDisposable
{
    private readonly PerformanceCounter? _cpu;
    private readonly nint _gpu;
    private readonly bool _nvml;

    public SystemMonitor()
    {
        try
        {
            // "% Processor Utility" matches what Task Manager shows (accounts for boost clocks).
            _cpu = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total");
            _cpu.NextValue();
        }
        catch (Exception)
        {
            _cpu = null;
        }

        try
        {
            _nvml = Nvml.Init() == 0 && Nvml.GetHandleByIndex(0, out _gpu) == 0;
        }
        catch (DllNotFoundException)
        {
            _nvml = false;
        }
    }

    public string? GpuName => _nvml ? Nvml.ReadName(_gpu) : null;

    public MonitorSnapshot Read()
    {
        var cpu = Math.Min(100, _cpu?.NextValue() ?? 0);

        var memory = new NativeMethods.MemoryStatusEx { Length = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MemoryStatusEx>() };
        NativeMethods.GlobalMemoryStatusEx(ref memory);
        var ramTotal = memory.TotalPhys / Gb;
        var ramUsed = (memory.TotalPhys - memory.AvailPhys) / Gb;

        if (!_nvml)
            return new(cpu, ramUsed, ramTotal, null, null, null, null, null);

        double? gpu = Nvml.GetUtilization(_gpu, out var util) == 0 ? util.Gpu : null;
        double? temp = Nvml.GetTemperature(_gpu, 0, out var celsius) == 0 ? celsius : null;
        double? clock = Nvml.GetClock(_gpu, Nvml.ClockGraphics, out var mhz) == 0 ? mhz : null;
        var hasVram = Nvml.GetMemory(_gpu, out var vram) == 0;
        return new(cpu, ramUsed, ramTotal, gpu, temp,
            hasVram ? vram.Used / Gb : null, hasVram ? vram.Total / Gb : null, clock);
    }

    public void Dispose()
    {
        _cpu?.Dispose();
        if (_nvml)
            Nvml.Shutdown();
    }

    private const double Gb = 1024d * 1024 * 1024;
}
