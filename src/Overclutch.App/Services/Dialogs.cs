using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Overclutch.App.Services;

public static class Dialogs
{
    public static Task<bool> ConfirmAsync(string title, string message, string confirm) =>
        ShowAsync(title, message, confirm);

    /// <summary>For a message of unknown length: it scrolls instead of growing past the screen.</summary>
    public static Task<bool> ConfirmScrollingAsync(string title, string message, string confirm) =>
        ShowAsync(title, new ScrollViewer
        {
            MaxHeight = 380,
            Width = 460,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
        }, confirm);

    private static async Task<bool> ShowAsync(string title, object content, string confirm)
    {
        var box = new MessageBox
        {
            Title = title,
            Content = content,
            PrimaryButtonText = confirm,
            PrimaryButtonAppearance = ControlAppearance.Primary,
            CloseButtonText = "Annuler",
            Owner = Application.Current.MainWindow is { IsVisible: true } owner ? owner : null,
        };
        return await box.ShowDialogAsync(true, default) == MessageBoxResult.Primary;
    }
}
