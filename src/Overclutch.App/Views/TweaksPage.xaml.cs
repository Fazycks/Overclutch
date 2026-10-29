using Overclutch.App.ViewModels;
using Overclutch.Core.Tweaks;

namespace Overclutch.App.Views;

public partial class TweaksPage
{
    private readonly TweakListViewModel _model = new(TweakRisk.Safe);

    public TweaksPage()
    {
        InitializeComponent();
        DataContext = _model;
        // States can change from elsewhere (the home page, Windows Update...): re-read on every visit.
        Loaded += async (_, _) => await _model.RefreshAsync();
    }
}
