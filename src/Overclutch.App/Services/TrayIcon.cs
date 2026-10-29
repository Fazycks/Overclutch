using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Overclutch.Core.Native;

namespace Overclutch.App.Services;

/// <summary>Notification-area icon: reopen the window, toggle game mode, quit.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly MenuItem _gameModeItem;
    private readonly DispatcherTimer _pendingTimer;
    private (string Title, string Message)? _pending;
    private int _freeTicks;

    public TrayIcon()
    {
        var services = AppServices.Instance;

        _gameModeItem = new MenuItem { Header = "Mode jeu automatique", IsCheckable = true };
        _gameModeItem.Click += (_, _) => services.SetGameModeEnabled(_gameModeItem.IsChecked);

        var open = new MenuItem { Header = "Ouvrir Overclutch" };
        open.Click += (_, _) => App.Current.ShowMainWindow();
        var quit = new MenuItem { Header = "Quitter" };
        quit.Click += (_, _) => App.Current.ExitApplication();

        var menu = new ContextMenu();
        menu.Items.Add(open);
        menu.Items.Add(_gameModeItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(quit);
        menu.Opened += (_, _) => _gameModeItem.IsChecked = services.Settings.GameModeEnabled;

        _icon = new TaskbarIcon
        {
            ToolTipText = "Overclutch",
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/overclutch.ico")),
            ContextMenu = menu,
            DoubleClickCommand = new RelayCommand(App.Current.ShowMainWindow),
            LeftClickCommand = new RelayCommand(App.Current.ShowMainWindow),
        };
        _icon.ForceCreate(false);

        services.GameMode.GameStarted += session => Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _icon.ToolTipText = $"Overclutch · En jeu : {session.Name}";
            NotifyWhenFree($"Mode jeu activé : {session.Name}", string.Join(" · ", session.Actions));
        });
        services.GameMode.GameEnded += session => Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _icon.ToolTipText = "Overclutch";
            var actions = session.Actions.Count > 0 ? string.Join(" · ", session.Actions) + Environment.NewLine : "";
            NotifyWhenFree($"Mode jeu terminé : {session.Name} ({Played(DateTime.Now - session.StartedAt)})",
                actions + "Tout est revenu comme avant.");
        });

        _pendingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _pendingTimer.Tick += (_, _) => FlushPending();
    }

    private static string Played(TimeSpan time) => time.TotalHours >= 1
        ? $"{(int)time.TotalHours} h {time.Minutes:00}"
        : $"{Math.Max(1, (int)time.TotalMinutes)} min";

    /// <summary>
    /// Windows throws notifications away while a game holds the screen, and for a few seconds after it closes.
    /// This one waits until Windows takes them again; a newer one replaces it, so a start notification never
    /// seen in full screen gives way to the end recap.
    /// </summary>
    private void NotifyWhenFree(string title, string message)
    {
        _pending = (title, message);
        _freeTicks = 0;
        _pendingTimer.Start();
    }

    private void FlushPending()
    {
        // Two checks in a row, so a game that is still closing does not swallow it.
        if (!UserNotificationState.AcceptsNotifications())
            _freeTicks = 0;
        else if (++_freeTicks >= 2 && _pending is { } pending)
        {
            Notify(pending.Title, pending.Message);
            _pending = null;
            _pendingTimer.Stop();
        }
    }

    public void Notify(string title, string message) =>
        _icon.ShowNotification(title, message, NotificationIcon.Info);

    public void Dispose()
    {
        _pendingTimer.Stop();
        _icon.Dispose();
    }
}
