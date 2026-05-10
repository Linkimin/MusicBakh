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
using System.Windows.Input;

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

        // Поднимаем настройки плеера до создания плеера и ViewModel —
        // громкость и mute должны примениться до первой команды Play.
        var playerSettingsStorage = new JsonPlayerSettingsStorage(JsonPlayerSettingsStorage.DefaultPath);
        PlayerSettings playerSettings = playerSettingsStorage.Load();

        var audioPlayerService = new MediaPlayerAudioService();
        audioPlayerService.Volume = playerSettings.Volume;
        audioPlayerService.IsMuted = playerSettings.IsMuted;

        _viewModel = new MainViewModel(
            repository,
            new FileService(),
            new SaveFileDialogService(),
            audioPlayerService,
            addTrackDialog,
            storage,
            new MessageBoxConfirmationService(),
            playerSettingsStorage);

        // RepeatMode выставляется после конструктора ViewModel, потому что setter
        // также сохраняет настройки — это безвредная повторная запись.
        _viewModel.RepeatMode = playerSettings.RepeatMode;

        DataContext = _viewModel;
    }

    // Drag по seek-слайдеру: ставим флаг, чтобы тик прогресс-таймера не перетёр Value.
    private void OnSeekDragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _viewModel.IsSeeking = true;
    }

    // Отпускание мыши: одинаково отрабатывает и завершение drag, и одиночный клик
    // благодаря IsMoveToPointEnabled — Value уже находится в финальной позиции.
    private void OnSeekPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Slider slider)
        {
            _viewModel.SeekToCommand.Execute(TimeSpan.FromSeconds(slider.Value));
        }

        _viewModel.IsSeeking = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
