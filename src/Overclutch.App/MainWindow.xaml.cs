using System.ComponentModel;
using Overclutch.App.Services;
using Overclutch.App.Views;
using Wpf.Ui.Abstractions;

namespace Overclutch.App;

public partial class MainWindow
{
    private bool _toldAboutTray;

    public MainWindow()
    {
        InitializeComponent();
        Navigation.SetPageProviderService(new PageCache());
        Loaded += (_, _) => Navigation.Navigate(typeof(HomePage));
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (App.Current.IsExiting || !AppServices.Instance.Settings.MinimizeToTray)
            return;

        // Game mode keeps watching from the notification area.
        e.Cancel = true;
        Hide();
        if (!_toldAboutTray)
        {
            _toldAboutTray = true;
            App.Current.NotifyTray("Overclutch reste actif", "Le mode jeu continue en arrière-plan. Clic droit sur l'icône pour quitter.");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (!App.Current.IsExiting)
            App.Current.ExitApplication();
    }

    /// <summary>Keeps one instance of each page so their state survives navigation.</summary>
    private sealed class PageCache : INavigationViewPageProvider
    {
        private readonly Dictionary<Type, object> _pages = [];

        public object? GetPage(Type pageType)
        {
            if (!_pages.TryGetValue(pageType, out var page))
                _pages[pageType] = page = Activator.CreateInstance(pageType)!;
            return page;
        }
    }
}
