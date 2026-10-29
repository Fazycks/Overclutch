using Overclutch.App.ViewModels;

namespace Overclutch.App.Views;

public partial class MonitoringPage
{
    private readonly MonitoringViewModel _model = new();

    public MonitoringPage()
    {
        InitializeComponent();
        DataContext = _model;
        Loaded += (_, _) => _model.Start();
        Unloaded += (_, _) => _model.Stop();
    }
}
