using Overclutch.App.ViewModels;

namespace Overclutch.App.Views;

public partial class HomePage
{
    private readonly HomeViewModel _model = new();

    public HomePage()
    {
        InitializeComponent();
        DataContext = _model;
        Loaded += async (_, _) => await _model.LoadAsync();
    }
}
