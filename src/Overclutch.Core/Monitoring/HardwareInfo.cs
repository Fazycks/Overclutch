using System.Management;

namespace Overclutch.Core.Monitoring;

public sealed record HardwareInfo(string Cpu, string CpuCores, string Gpu, string GpuDriver, string Ram, string Motherboard, string Os)
{
    public bool HasNvidiaGpu => Gpu.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase);

    public static HardwareInfo Detect()
    {
        var cpu = First("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
        var gpus = All("SELECT Name, DriverVersion FROM Win32_VideoController");
        // Prefer the discrete NVIDIA card over integrated graphics.
        var gpu = gpus.FirstOrDefault(g => Str(g, "Name").Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)) ?? gpus.FirstOrDefault();
        var sticks = All("SELECT Capacity, ConfiguredClockSpeed FROM Win32_PhysicalMemory");
        var board = First("SELECT Manufacturer, Product FROM Win32_BaseBoard");
        var os = First("SELECT Caption, BuildNumber FROM Win32_OperatingSystem");

        var ramBytes = sticks.Sum(s => Convert.ToDouble(s["Capacity"] ?? 0));
        var ramSpeed = sticks.Select(s => Convert.ToInt32(s["ConfiguredClockSpeed"] ?? 0)).DefaultIfEmpty(0).Max();

        return new HardwareInfo(
            Cpu: Str(cpu, "Name").Trim(),
            CpuCores: $"{Str(cpu, "NumberOfCores")} cœurs / {Str(cpu, "NumberOfLogicalProcessors")} threads",
            Gpu: gpu is null ? "Inconnu" : Str(gpu, "Name"),
            GpuDriver: gpu is null ? "" : Str(gpu, "DriverVersion"),
            Ram: $"{ramBytes / (1024d * 1024 * 1024):0} Go" + (ramSpeed > 0 ? $" @ {ramSpeed} MT/s" : "") + $" ({sticks.Count} barrette{(sticks.Count > 1 ? "s" : "")})",
            Motherboard: $"{Str(board, "Manufacturer")} {Str(board, "Product")}".Trim(),
            Os: $"{Str(os, "Caption")} (build {Str(os, "BuildNumber")})");
    }

    private static List<ManagementBaseObject> All(string query)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            return [.. searcher.Get().Cast<ManagementBaseObject>()];
        }
        catch (ManagementException)
        {
            return [];
        }
    }

    private static ManagementBaseObject? First(string query) => All(query).FirstOrDefault();

    private static string Str(ManagementBaseObject? obj, string property) => obj?[property]?.ToString() ?? "?";
}
