using Overclutch.App.ViewModels;

namespace Overclutch.App.Views;

public partial class SettingsPage
{
    private readonly SettingsViewModel _model = new();

    public SettingsPage()
    {
        InitializeComponent();
        DataContext = _model;
        Loaded += async (_, _) => await _model.LoadAsync();
    }
}
