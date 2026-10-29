using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using Velopack;

namespace Overclutch.App;

public static class Program
{
    private const string MutexName = @"Global\Overclutch.SingleInstance";
    public const string ShowEventName = @"Global\Overclutch.Show";
    private const int ErrorCancelled = 1223;

    [STAThread]
    public static void Main(string[] args)
    {
        // Must run first: handles install/update/uninstall hooks and exits when Velopack invokes us for them.
        // Those hooks run without elevation, which is why the manifest does not require it.
        VelopackApp.Build().Run();

        if (!IsElevated())
        {
            RelaunchElevated(args);
            return;
        }

        using var mutex = new Mutex(true, MutexName, out var isFirst);
        if (!isFirst)
        {
            // Bring the running instance to the front instead of starting a second one.
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out var show))
                show.Set();
            return;
        }

        var app = new App { StartHidden = args.Contains("--tray") };
        app.InitializeComponent();
        app.Run();
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void RelaunchElevated(string[] args)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = true,
            Verb = "runas",
            Arguments = string.Join(' ', args.Select(a => $"\"{a}\"")),
        };
        try
        {
            Process.Start(start);
        }
        catch (Win32Exception e) when (e.NativeErrorCode == ErrorCancelled)
        {
            // The user declined the UAC prompt: nothing to do without admin rights.
        }
    }
}
