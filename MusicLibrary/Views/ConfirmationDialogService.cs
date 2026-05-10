using MusicLibrary.Services.Files;
using System.Windows;

namespace MusicLibrary.Views;

public sealed class ConfirmationDialogService : IConfirmationService
{
    public bool Confirm(string title, string message)
    {
        var window = new ConfirmationDialogWindow(title, message);
        Window? owner = Application.Current?.MainWindow;
        if (owner is not null && owner.IsVisible)
        {
            window.Owner = owner;
        }

        return window.ShowDialog() == true;
    }
}
