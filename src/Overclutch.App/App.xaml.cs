using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Overclutch.App.Services;
using Overclutch.Core.Settings;
using Wpf.Ui.Appearance;

namespace Overclutch.App;

public partial class App : Application
{
    public static readonly Color Accent = Color.FromRgb(0x7C, 0x4D, 0xFF);

    private TrayIcon? _tray;
    private EventWaitHandle? _showEvent;

    /// <summary>Started at logon with <c>--tray</c>: stay in the notification area.</summary>
    public bool StartHidden { get; init; }

    public static new App Current => (App)Application.Current;

    public bool IsExiting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        ApplicationThemeManager.Apply(ApplicationTheme.Dark);
        ApplicationAccentColorManager.Apply(Accent, ApplicationTheme.Dark);

        var services = AppServices.Instance;
        _tray = new TrayIcon();

        MainWindow = new MainWindow();
        if (!StartHidden)
            MainWindow.Show();

        if (services.Settings.GameModeEnabled)
            services.GameMode.Start();
        services.Maintenance.Start();

        // Lets the uninstall hook know about a startup task created by an older version.
        _ = Task.Run(Autostart.SyncMarker);

        ListenForShowRequests();
    }

    public void ShowMainWindow()
    {
        if (MainWindow is not { } window)
            return;
        window.Show();
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
    }

    public void NotifyTray(string title, string message) => _tray?.Notify(title, message);

    public void ExitApplication()
    {
        IsExiting = true;
        // Stopping game mode puts back the power plan and process priorities it changed.
        AppServices.Instance.GameMode.Stop();
        _tray?.Dispose();
        _showEvent?.Dispose();
        Shutdown();
    }

    /// <summary>A second launch signals this event instead of opening another window.</summary>
    private void ListenForShowRequests()
    {
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ShowEventName);
        var thread = new Thread(() =>
        {
            try
            {
                while (_showEvent.WaitOne())
                    Dispatcher.Invoke(ShowMainWindow);
            }
            catch (ObjectDisposedException)
            {
            }
        })
        { IsBackground = true, Name = "Overclutch show listener" };
        thread.Start();
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Trace.WriteLine(e.Exception);
        MessageBox.Show(e.Exception.Message, "Overclutch — erreur inattendue", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
