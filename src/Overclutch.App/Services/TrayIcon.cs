using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace Overclutch.App.Services;

/// <summary>Notification-area icon: reopen the window, toggle game mode, quit.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly MenuItem _gameModeItem;

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
            Notify($"Mode jeu activé : {session.Name}", string.Join(" · ", session.Actions)));
        services.GameMode.GameEnded += session => Application.Current.Dispatcher.BeginInvoke(() =>
            Notify("Mode jeu terminé", $"{session.Name} est fermé, réglages normaux rétablis."));
    }

    public void Notify(string title, string message) =>
        _icon.ShowNotification(title, message, NotificationIcon.Info);

    public void Dispose() => _icon.Dispose();
}
