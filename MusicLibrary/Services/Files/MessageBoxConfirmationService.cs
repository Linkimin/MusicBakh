using System.Windows;

namespace MusicLibrary.Services.Files;

public sealed class MessageBoxConfirmationService : IConfirmationService
{
    public bool Confirm(string title, string message)
    {
        MessageBoxResult result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question,
            MessageBoxResult.Cancel);

        return result == MessageBoxResult.OK;
    }
}
