using Overclutch.App.ViewModels;
using Overclutch.Core.Tweaks;

namespace Overclutch.App.Views;

public partial class AdvancedPage
{
    private readonly TweakListViewModel _model = new(TweakRisk.Advanced);

    public AdvancedPage()
    {
        InitializeComponent();
        DataContext = _model;
        Loaded += async (_, _) => await _model.RefreshAsync();
    }
}
