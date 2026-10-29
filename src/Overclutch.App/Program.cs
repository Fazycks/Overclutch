using Velopack;

namespace Overclutch.App;

public static class Program
{
    private const string MutexName = @"Global\Overclutch.SingleInstance";
    public const string ShowEventName = @"Global\Overclutch.Show";

    [STAThread]
    public static void Main(string[] args)
    {
        // Must run first: handles install/update/uninstall hooks and exits when Velopack invokes us for them.
        VelopackApp.Build().Run();

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
}
