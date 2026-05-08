using MusicLibrary.Services.Files;
using MusicLibrary.Services.Playback;
using MusicLibrary.Services.Tracks;
using MusicLibrary.ViewModels;
using System.Windows;

namespace MusicLibrary;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        // В учебной работе логика располагалась в окне. Здесь окно только собирает зависимости,
        // а сценарии приложения выполняет MainViewModel через сервисы.
        _viewModel = new MainViewModel(
            new InMemoryTrackRepository(),
            new FileService(),
            new SaveFileDialogService(),
            new MediaPlayerAudioService());

        DataContext = _viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
