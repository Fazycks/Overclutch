using Overclutch.App.ViewModels;

namespace Overclutch.App.Views;

public partial class ProfilesPage
{
    public ProfilesPage()
    {
        InitializeComponent();
        DataContext = new ProfilesViewModel();
    }
}
