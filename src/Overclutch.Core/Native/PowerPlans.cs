using System.Runtime.InteropServices;
using System.Text;

namespace Overclutch.Core.Native;

/// <summary>Thin wrapper over the powrprof API (no powercfg output parsing, which is localized).</summary>
public static class PowerPlans
{
    public static readonly Guid UltimatePerformance = new("e9a42b02-d5df-448d-aa00-03f14749eb61");
    public static readonly Guid HighPerformance = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    public static readonly Guid ProcessorSubgroup = new("54533251-82be-4824-96c1-47b60b740d00");

    public const string OverclutchPlanName = "Overclutch Performance";

    public static Guid GetActive()
    {
        Check(NativeMethods.PowerGetActiveScheme(0, out var ptr), "PowerGetActiveScheme");
        try { return Marshal.PtrToStructure<Guid>(ptr); }
        finally { NativeMethods.LocalFree(ptr); }
    }

    public static void SetActive(Guid scheme) =>
        Check(NativeMethods.PowerSetActiveScheme(0, scheme), "PowerSetActiveScheme");

    public static IEnumerable<Guid> Enumerate()
    {
        for (uint i = 0; ; i++)
        {
            uint size = (uint)Marshal.SizeOf<Guid>();
            if (NativeMethods.PowerEnumerate(0, 0, 0, NativeMethods.AccessScheme, i, out var guid, ref size) != 0)
                yield break;
            yield return guid;
        }
    }

    public static string? GetName(Guid scheme)
    {
        uint size = 0;
        if (NativeMethods.PowerReadFriendlyName(0, scheme, 0, 0, null, ref size) != 0 || size == 0)
            return null;
        var buffer = new byte[size];
        if (NativeMethods.PowerReadFriendlyName(0, scheme, 0, 0, buffer, ref size) != 0)
            return null;
        return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
    }

    public static bool Exists(Guid scheme) => Enumerate().Contains(scheme);

    public static Guid? FindOverclutchPlan() =>
        Enumerate().Cast<Guid?>().FirstOrDefault(g => GetName(g!.Value) == OverclutchPlanName);

    /// <summary>Returns the Overclutch plan, creating it from Ultimate Performance (or High Performance) if needed.</summary>
    public static Guid EnsureOverclutchPlan()
    {
        if (FindOverclutchPlan() is { } existing)
            return existing;

        var created = Duplicate(UltimatePerformance) ?? Duplicate(HighPerformance)
            ?? throw new InvalidOperationException("Impossible de créer le plan d'alimentation Overclutch.");

        var name = Encoding.Unicode.GetBytes(OverclutchPlanName + "\0");
        NativeMethods.PowerWriteFriendlyName(0, created, 0, 0, name, (uint)name.Length);
        return created;
    }

    /// <summary>
    /// Removes the Overclutch plan unless it is the active one (Windows refuses to delete it then).
    /// Game mode re-creates it on demand.
    /// </summary>
    public static void DeleteOverclutchPlan()
    {
        if (FindOverclutchPlan() is { } plan && GetActive() != plan)
            NativeMethods.PowerDeleteScheme(0, plan);
    }

    private static Guid? Duplicate(Guid source)
    {
        nint dest = 0;
        if (NativeMethods.PowerDuplicateScheme(0, source, ref dest) != 0 || dest == 0)
            return null;
        try { return Marshal.PtrToStructure<Guid>(dest); }
        finally { NativeMethods.LocalFree(dest); }
    }

    public static uint ReadAc(Guid scheme, Guid subgroup, Guid setting)
    {
        Check(NativeMethods.PowerReadACValueIndex(0, scheme, subgroup, setting, out var value), "PowerReadACValueIndex");
        return value;
    }

    public static void WriteAc(Guid scheme, Guid subgroup, Guid setting, uint value)
    {
        Check(NativeMethods.PowerWriteACValueIndex(0, scheme, subgroup, setting, value), "PowerWriteACValueIndex");
        // Re-activating the scheme is what makes a written value take effect.
        if (GetActive() == scheme)
            SetActive(scheme);
    }

    private static void Check(uint result, string api)
    {
        if (result != 0)
            throw new InvalidOperationException($"{api} a échoué (code {result}).");
    }
}
