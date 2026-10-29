using Overclutch.App.ViewModels;

namespace Overclutch.App.Views;

public partial class MaintenancePage
{
    private readonly MaintenanceViewModel _model = new();

    public MaintenancePage()
    {
        InitializeComponent();
        DataContext = _model;
        Loaded += async (_, _) =>
        {
            if (_model.AnalyzeCommand.CanExecute(null))
                await _model.AnalyzeCommand.ExecuteAsync(null);
        };
    }
}
