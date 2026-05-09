using MusicLibrary.Services.Covers;
using MusicLibrary.Services.Files;
using MusicLibrary.Services.Import;
using MusicLibrary.Services.Metadata;
using MusicLibrary.Services.Playback;
using MusicLibrary.Services.Storage;
using MusicLibrary.Services.Tracks;
using MusicLibrary.ViewModels;
using MusicLibrary.Views;
using System.Net.Http;
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
        var storage = new JsonUserTrackStorage();
        var repository = new CompositeTrackRepository(new InMemoryTrackRepository(), storage);

        var sharedHttpClient = new HttpClient();
        var musicBrainz = new MusicBrainzClient(sharedHttpClient);
        var tagReader = new TagLibSharpTagReader();
        var genreNormalizer = new RussianGenreNormalizer();
        var itunesClient = new ItunesCoverClient(sharedHttpClient);
        var metadataResolver = new DefaultMetadataResolver(tagReader, musicBrainz, itunesClient, genreNormalizer);

        var procedural = new ProceduralCoverGenerator();
        var coverResolver = new CompositeCoverResolver(itunesClient, procedural);

        var importer = new TrackImporter(storage, metadataResolver, coverResolver, sharedHttpClient);
        var openFileDialog = new OpenFileDialogService();
        var addTrackDialog = new AddTrackDialogService(openFileDialog, importer);

        _viewModel = new MainViewModel(
            repository,
            new FileService(),
            new SaveFileDialogService(),
            new MediaPlayerAudioService(),
            addTrackDialog,
            storage,
            new MessageBoxConfirmationService());

        DataContext = _viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
