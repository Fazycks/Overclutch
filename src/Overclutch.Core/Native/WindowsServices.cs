using System.ServiceProcess;

namespace Overclutch.Core.Native;

public static class WindowsServices
{
    /// <summary>Stops a service without touching the services that depend on it. Returns whether it was running,
    /// even when it refused to stop, so <see cref="Resume"/> knows whether to start it again.</summary>
    public static bool Pause(string name, TimeSpan timeout)
    {
        using var service = new ServiceController(name);
        bool wasRunning;
        try
        {
            wasRunning = service.Status != ServiceControllerStatus.Stopped;
        }
        catch (InvalidOperationException)
        {
            return false; // Not installed.
        }

        try
        {
            if (wasRunning && service.CanStop)
            {
                service.Stop(stopDependentServices: false);
                service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
            }
        }
        catch (Exception e) when (e is InvalidOperationException or System.ServiceProcess.TimeoutException)
        {
            // Running dependents or a slow stop: it keeps running.
        }
        return wasRunning;
    }

    /// <summary>Starts a paused service again, unless something else already did.</summary>
    public static void Resume(string name)
    {
        using var service = new ServiceController(name);
        try
        {
            if (service.Status == ServiceControllerStatus.Stopped)
                service.Start();
        }
        catch (InvalidOperationException)
        {
            // Trigger-started services come back on their own when needed.
        }
    }

    public static bool IsStopped(string name)
    {
        using var service = new ServiceController(name);
        try
        {
            return service.Status == ServiceControllerStatus.Stopped;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }
}
