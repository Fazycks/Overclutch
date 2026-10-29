using Overclutch.App.ViewModels;

namespace Overclutch.App.Views;

public partial class GameModePage
{
    public GameModePage()
    {
        InitializeComponent();
        DataContext = new GameModeViewModel();
    }
}
