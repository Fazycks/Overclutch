using System.ComponentModel;

namespace Overclutch.Core.Native;

/// <summary>
/// The standby list: file data Windows keeps in RAM in case it is needed again. Reading and purging it both
/// need the "profile single process" privilege, which administrators hold but have to switch on.
/// </summary>
public static class StandbyMemory
{
    // SYSTEM_MEMORY_LIST_INFORMATION is 22 pointer-sized counters; the standby pages by priority are 5 to 12.
    private const int CounterCount = 22;
    private const int FirstStandbyCounter = 5;
    private const int StandbyCounterCount = 8;

    public static unsafe long Size()
    {
        EnablePrivilege();
        var counters = new nuint[CounterCount];
        fixed (nuint* buffer = counters)
        {
            var status = NativeMethods.NtQuerySystemInformation(NativeMethods.SystemMemoryListInformation, buffer,
                CounterCount * sizeof(nuint), out _);
            if (status != 0)
                throw new InvalidOperationException($"Lecture de la mémoire en attente impossible (0x{status:X8}).");
        }

        ulong pages = 0;
        for (var i = FirstStandbyCounter; i < FirstStandbyCounter + StandbyCounterCount; i++)
            pages += counters[i];
        return (long)pages * Environment.SystemPageSize;
    }

    public static unsafe void Purge()
    {
        EnablePrivilege();
        var command = NativeMethods.MemoryPurgeStandbyList;
        var status = NativeMethods.NtSetSystemInformation(NativeMethods.SystemMemoryListInformation, &command, sizeof(int));
        if (status != 0)
            throw new InvalidOperationException($"Windows a refusé de vider la mémoire en attente (0x{status:X8}).");
    }

    private static void EnablePrivilege()
    {
        if (!NativeMethods.OpenProcessToken(NativeMethods.GetCurrentProcess(),
                NativeMethods.TokenAdjustPrivileges | NativeMethods.TokenQuery, out var token))
            throw new Win32Exception();
        try
        {
            if (!NativeMethods.LookupPrivilegeValue(null, "SeProfileSingleProcessPrivilege", out var luid))
                throw new Win32Exception();
            var privileges = new NativeMethods.TokenPrivileges
            {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = NativeMethods.SePrivilegeEnabled,
            };
            if (!NativeMethods.AdjustTokenPrivileges(token, false, privileges, 0, 0, 0))
                throw new Win32Exception();
        }
        finally
        {
            NativeMethods.CloseHandle(token);
        }
    }
}
