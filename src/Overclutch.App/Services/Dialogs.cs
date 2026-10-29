using System.Windows;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace Overclutch.App.Services;

public static class Dialogs
{
    public static async Task<bool> ConfirmAsync(string title, string message, string confirm)
    {
        var box = new MessageBox
        {
            Title = title,
            Content = message,
            PrimaryButtonText = confirm,
            PrimaryButtonAppearance = ControlAppearance.Primary,
            CloseButtonText = "Annuler",
            Owner = Application.Current.MainWindow is { IsVisible: true } owner ? owner : null,
        };
        return await box.ShowDialogAsync(true, default) == MessageBoxResult.Primary;
    }
}
