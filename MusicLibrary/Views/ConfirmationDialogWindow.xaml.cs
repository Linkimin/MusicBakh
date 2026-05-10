using System.Windows;

namespace MusicLibrary.Views;

public partial class ConfirmationDialogWindow : Window
{
    public ConfirmationDialogWindow(string title, string message)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => NativeWindowAppearance.Apply(this);

        Title = title;
        TitleText.Text = title;

        (string trackName, string warning) = SplitDeleteMessage(message);
        TrackNameText.Text = trackName;
        MessageText.Text = warning;
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static (string TrackName, string Warning) SplitDeleteMessage(string message)
    {
        const string fallbackWarning = "Файл и обложка будут удалены безвозвратно.";

        int start = message.IndexOf('«');
        int end = message.IndexOf('»');
        if (start >= 0 && end > start)
        {
            string trackName = message[(start + 1)..end].Trim();
            return (trackName, fallbackWarning);
        }

        return (message, fallbackWarning);
    }
}
