using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using MusicBakh.Application.Abstractions;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicLibrary.Selectors;
using MusicLibrary.Services.Library;
using MusicLibrary.ViewModels;
using Xunit;

namespace MusicLibrary.Tests.Rendering;

/// <summary>
/// CI-сторож урока итерации D: конвертер, объявленный ТОЛЬКО в прямых дочерних
/// ресурсах App.xaml (после MergedDictionaries), невидим для {StaticResource} внутри
/// DataTemplate из смёрженного словаря -> XamlParseException только в рантайме при
/// переключении вкладки, билд его не ловит.
///
/// Тест воспроизводит реальное окружение ресурсов приложения: на STA-потоке инстанцирует
/// настоящий MusicLibrary.App и вызывает его App.InitializeComponent() (тот же метод,
/// что вызывает сгенерированный Main() перед Run()) — то есть точно так же, как это
/// делает WPF при старте (сначала MergedDictionaries по порядку, потом прямые дочерние
/// ресурсы), но БЕЗ Run()/OnStartup (значит без Host/DI/MainWindow).
/// Затем на реальном MainViewModel (сид-данные: альбом, исполнитель, плейлист с 2 треками,
/// играющий трек) инстанцирует все 7 вариантов LeftColumnState через настоящий
/// LeftColumnTemplateSelector и форсит рендер в видимом Window.
/// </summary>
public sealed class LeftColumnRenderSmokeTests
{
    [Fact]
    public void AllLeftColumnTemplates_Render_Without_Exception()
    {
        StaRunner.Run(() =>
        {
            EnsureApplicationWithMergedDictionaries();
            VerifyResourceEnvironmentIsFaithful();

            var (vm, playlist) = BuildSeedViewModel();

            // Навигация через реальные команды VM — тот же путь, которым идёт прод,
            // а не ручная сборка LeftColumnState-объектов в обход навигационного стека.
            RenderCurrentState(vm); // TracksRoot (дефолт после сборки + PlayTrackCommand)

            vm.SwitchViewCommand.Execute(MainViewMode.Albums);
            RenderCurrentState(vm); // AlbumsRoot

            var album = vm.DisplayedAlbums.First(a => a.Title == "Album1");
            vm.OpenAlbumCommand.Execute(album);
            RenderCurrentState(vm); // AlbumDetail
            vm.BackCommand.Execute(null);

            vm.SwitchViewCommand.Execute(MainViewMode.Artists);
            RenderCurrentState(vm); // ArtistsRoot

            var artist = vm.DisplayedArtists.First(a => a.Name == "Artist1");
            vm.OpenArtistCommand.Execute(artist);
            RenderCurrentState(vm); // ArtistDetail
            vm.BackCommand.Execute(null);

            vm.SwitchViewCommand.Execute(MainViewMode.Playlists);
            RenderCurrentState(vm); // PlaylistsRoot

            vm.OpenPlaylistCommand.Execute(playlist);
            RenderCurrentState(vm); // PlaylistDetail

            // Sanity: прошли ровно через все 7 вариантов дискриминированного union'а.
            Assert.IsType<LeftColumnState.PlaylistDetail>(vm.CurrentLeftColumn);
            RenderFullWindow(vm);
        });
    }

    private static void RenderFullWindow(MainViewModel vm)
    {
        vm.SwitchViewCommand.Execute(MainViewMode.Tracks);
        vm.PlayTrackCommand.Execute(vm.DisplayedTracks[0]);
        vm.PlaybackHistory.Add(new PlaybackEntry { Track = vm.DisplayedTracks[0], PlayedAt = DateTime.UtcNow });
        var window = new MusicLibrary.MainWindow(vm, new FakePlayerSettingsRepository())
            { ShowInTaskbar = false, ShowActivated = false };
        try
        {
            window.Show();
            foreach (var size in new[] { new Size(980, 640), new Size(1180, 760) })
            {
                window.Width = size.Width;
                window.Height = size.Height;
                window.UpdateLayout();
                var button = (FrameworkElement)window.FindName("TransportPlayButton");
                var point = button.TranslatePoint(new Point(), window);

                string? output = Environment.GetEnvironmentVariable("MUSICBAKH_RENDER_OUTPUT");
                if (!string.IsNullOrEmpty(output))
                {
                    Directory.CreateDirectory(output);
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                        (int)window.ActualWidth, (int)window.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(output, $"main-{size.Width}.png"));
                    encoder.Save(file);
                }
                Assert.True(point.Y > 0 && point.Y + button.ActualHeight <= window.ActualHeight,
                    $"Транспорт: Y={point.Y}, H={button.ActualHeight}, WindowH={window.ActualHeight}.");
                Assert.True(button.ActualWidth >= 40 && button.ActualHeight >= 40);
                var tabs = (FrameworkElement)window.FindName("LibraryNavigation");
                var header = (FrameworkElement)window.FindName("LibraryHeader");
                Assert.True(tabs.ActualHeight >= 30);
                Assert.True(tabs.TranslatePoint(new Point(), header).Y + tabs.ActualHeight <= header.ActualHeight);
                var rating = (FrameworkElement)window.FindName("RatingControls");
                var editor = (FrameworkElement)window.FindName("TrackEditor");
                Assert.True(rating.TranslatePoint(new Point(), editor).X >= 0);
                Assert.True(rating.TranslatePoint(new Point(), editor).X + rating.ActualWidth <= editor.ActualWidth);
                Assert.True(editor.ActualWidth >= 350);
                var selectedPlay = (Button)window.FindName("PlaySelectedButton");
                Assert.True(selectedPlay.ActualHeight >= 36);
                Assert.True(Descendants(selectedPlay).OfType<ContentPresenter>().First().ActualHeight >= selectedPlay.FontSize * 1.2,
                    "Button padding must leave enough height for its label.");
                Assert.True(selectedPlay.TranslatePoint(new Point(), window).Y + selectedPlay.ActualHeight <= point.Y);
            }
            // Проверяем и историю: её double-click должен воспроизводить трек.
            var historyRow = Descendants(window).OfType<FrameworkElement>().First(e => e.DataContext is PlaybackEntry && e.InputBindings.Count > 0);
            var historyClick = historyRow.InputBindings.OfType<System.Windows.Input.MouseBinding>().Single();
            Assert.NotNull(historyClick.Command);
            vm.StopCommand.Execute(null);
            historyClick.Command.Execute(historyClick.CommandParameter);
            Assert.True(vm.IsPlaying);
            vm.MinRating = 5;
            window.UpdateLayout();
            Assert.Empty(vm.DisplayedTracks);
            Assert.Contains(Descendants(window).OfType<TextBlock>(), t => t.Text == "Треки не найдены" && t.IsVisible);
            vm.ClearFiltersCommand.Execute(null);
            window.UpdateLayout();
            Assert.NotEmpty(vm.DisplayedTracks);
            var longTrack = new Track { Id = 999, Title = new string('Ж', 180), Artist = new string('И', 120), Genre = new string('Р', 80), FilePath = "long.mp3" };
            vm.AddTrack(longTrack);
            vm.SelectedTrack = longTrack;
            window.Width = 980;
            window.Height = 640;
            window.UpdateLayout();
            var compactPlay = (Button)window.FindName("PlaySelectedButton");
            Assert.True(compactPlay.TranslatePoint(new Point(), window).Y + compactPlay.ActualHeight < window.ActualHeight);
            var scaled = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(window.ActualWidth * 1.5), (int)(window.ActualHeight * 1.5), 144, 144, System.Windows.Media.PixelFormats.Pbgra32);
            scaled.Render(window);
            if (Environment.GetEnvironmentVariable("MUSICBAKH_RENDER_OUTPUT") is { Length: > 0 } outputPath)
            {
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(scaled));
                using var file = File.Create(Path.Combine(outputPath, "long-text-150-percent.png"));
                encoder.Save(file);
            }
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// Рендерит текущее значение vm.CurrentLeftColumn в реальном визуальном дереве Window,
    /// с настоящим LeftColumnTemplateSelector, чьи шаблоны взяты из загруженных
    /// ресурсов приложения (те же ключи, что использует MainWindow.xaml).
    /// </summary>
    private static void RenderCurrentState(MainViewModel vm)
    {
        var host = new ContentControl
        {
            Content = vm.CurrentLeftColumn,
            ContentTemplateSelector = BuildSelector(),
        };

        var window = new Window
        {
            Content = host,
            DataContext = vm,
            Width = 800,
            Height = 600,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
        };

        try
        {
            window.Show(); // форсит полный цикл шаблонизации/биндинга
            host.UpdateLayout();
            Assert.True(host.ActualWidth > 0 && host.ActualHeight > 0);
            Assert.True(System.Windows.Media.VisualTreeHelper.GetChildrenCount(host) > 0);
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            int playbackBindings = 0;
            foreach (var element in Descendants(host).OfType<FrameworkElement>())
            {
                foreach (var binding in element.InputBindings.OfType<System.Windows.Input.MouseBinding>())
                {
                    if (binding.MouseAction != System.Windows.Input.MouseAction.LeftDoubleClick || element.DataContext is not Track track) continue;
                    playbackBindings++;
                    Assert.NotNull(binding.Command);
                    Assert.True(binding.Command.CanExecute(binding.CommandParameter));
                    vm.StopCommand.Execute(null);
                    binding.Command.Execute(binding.CommandParameter);
                    Assert.Equal(track.Id, vm.PlayingTrack?.Id);
                    Assert.True(vm.IsPlaying);
                }
            }
            if (vm.CurrentLeftColumn is LeftColumnState.TracksRoot or LeftColumnState.AlbumDetail or LeftColumnState.PlaylistDetail)
                Assert.True(playbackBindings > 0, "Track rows must expose working double-click bindings.");
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static LeftColumnTemplateSelector BuildSelector() => new()
    {
        TracksTemplate = GetTemplate("TracksRootTemplate"),
        AlbumsTemplate = GetTemplate("AlbumsRootTemplate"),
        ArtistsTemplate = GetTemplate("ArtistsRootTemplate"),
        AlbumDetailTemplate = GetTemplate("AlbumDetailTemplate"),
        ArtistDetailTemplate = GetTemplate("ArtistDetailTemplate"),
        PlaylistsTemplate = GetTemplate("PlaylistsRootTemplate"),
        PlaylistDetailTemplate = GetTemplate("PlaylistDetailTemplate"),
    };

    private static DataTemplate GetTemplate(string key)
    {
        var resource = Application.Current.Resources[key]
            ?? throw new InvalidOperationException(
                $"Ресурс '{key}' не найден в Application.Current.Resources — " +
                "смёрженные словари App.xaml не загрузились как надо.");
        return (DataTemplate)resource;
    }

    /// <summary>
    /// Инстанцирует реальный MusicLibrary.App (сгенерированный partial-класс из App.xaml)
    /// и вызывает его собственный InitializeComponent() — тот самый метод, что вызывает
    /// App.xaml.cs.Main() перед Run(). InitializeComponent() внутри делает ровно
    /// Application.LoadComponent(this, resourceLocater) с pack-URI, зашитым компилятором
    /// (включая версию сборки, см. App.g.cs) — т.е. смёржит словари App.xaml (все
    /// MergedDictionaries по порядку, ЗАТЕМ прямые дочерние ресурсы — конвертеры) в
    /// Application.Current.Resources ТОЧНО так же, как при обычном запуске.
    ///
    /// startServices:false явно отключает Host/DI/storage: WPF ставит OnStartup в очередь
    /// даже без Run(), поэтому одного отсутствия Run() для изоляции недостаточно.
    ///
    /// Почему не голый Application.LoadComponent(new Application(), uri) с URI, собранным
    /// руками: он резолвится через статическое Application.ResourceAssembly, которое WPF
    /// устанавливает ровно один раз за процесс (на entry assembly, т.е. тестовый хост) —
    /// переустановить его после первого чтения нельзя (InvalidOperationException). Использование
    /// самого App.InitializeComponent() обходит эту проблему: URI строится компилятором
    /// как "/MusicBakh;V{version};component/app.xaml" и резолвится через сборку App,
    /// а не через ResourceAssembly.
    /// </summary>
    private static void EnsureApplicationWithMergedDictionaries()
    {
        if (Application.Current is not null &&
            Application.Current.Resources["TracksRootTemplate"] is not null)
        {
            return; // уже загружено (несколько тестов в одном процессе/потоке).
        }

        var app = new MusicLibrary.App(startServices: false);
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        app.InitializeComponent();
    }

    /// <summary>
    /// Явная проверка добросовестности харнесса: ресурсы должны содержать И шаблон
    /// из смёрженного словаря (PlaylistsRootTemplate из PlaylistsTemplates.xaml), И
    /// конвертер, объявленный ТОЛЬКО как прямой ребёнок Application.Resources в App.xaml
    /// (BooleanToVisibilityConverter, идёт в XAML ПОСЛЕ блока MergedDictionaries).
    /// Если это условие не выполняется — тест ничего не гарантирует, и мы обязаны
    /// упасть здесь явно, а не молча "пройти" пустой рендер.
    /// </summary>
    private static void VerifyResourceEnvironmentIsFaithful()
    {
        var mergedTemplate = Application.Current.Resources["PlaylistsRootTemplate"];
        Assert.True(mergedTemplate is DataTemplate,
            "Ожидался DataTemplate 'PlaylistsRootTemplate' из смёрженного словаря PlaylistsTemplates.xaml — " +
            "харнесс не воспроизводит реальное окружение ресурсов приложения.");

        var directConverter = Application.Current.Resources["BooleanToVisibilityConverter"];
        Assert.True(directConverter is System.Windows.Data.IValueConverter,
            "Ожидался конвертер 'BooleanToVisibilityConverter', объявленный как прямой дочерний " +
            "ресурс Application.Resources в App.xaml (после MergedDictionaries) — именно это " +
            "воспроизводит условие D-бага (StaticResource из смёрженного шаблона на конвертер, " +
            "объявленный только на уровне приложения).");
    }

    /// <summary>
    /// Собирает MainViewModel с фейками (тот же паттерн, что в MainViewModelNavigationTests /
    /// PlaylistsViewModelTests) и сид-данными: альбом (2 трека одного альбома/исполнителя),
    /// отдельный исполнитель, плейлист с >=2 треками, и выставляет PlayingTrack, чтобы
    /// сработал MultiBinding подсветки "сейчас играет". Возвращает VM и созданный Playlist
    /// (для навигации через OpenPlaylistCommand в теле теста).
    /// </summary>
    private static (MainViewModel Vm, Playlist Playlist) BuildSeedViewModel()
    {
        var tracks = new[]
        {
            new Track { Id = 1, Title = "Track1", Artist = "Artist1", Album = "Album1", TrackNumber = 1, Genre = "Рок", Duration = TimeSpan.FromSeconds(204), CoverPath = Path.Combine(AppContext.BaseDirectory, "Covers", "ya-svoboden.jpg"), FilePath = "1.mp3" },
            new Track { Id = 2, Title = "Track2", Artist = "Artist1", Album = "Album1", TrackNumber = 2, Genre = "Инди", Duration = TimeSpan.FromSeconds(215), CoverPath = Path.Combine(AppContext.BaseDirectory, "Covers", "hayloft-ii.jpg"), FilePath = "2.mp3" },
            new Track { Id = 3, Title = "Track3", Artist = "Artist2", Album = "Album2", TrackNumber = 1, FilePath = "3.mp3" },
        };

        var trackRepo = new FakeTrackRepo(tracks);
        var playlistRepo = new FakePlaylistRepo();
        var playlistsVm = new PlaylistsViewModel(playlistRepo, new YesConfirmationService(), new FixedClock());

        var vm = new MainViewModel(
            trackRepo,
            new FakeFileService(),
            new FakeSaveFileDialogService(),
            new FakeAudioPlayerService(),
            new FakeListeningHistoryRepository(),
            new FakePlayerSettingsRepository(),
            addTrackDialogService: null,
            confirmationService: null,
            searchService: null,
            statsWindowService: null,
            tagRepository: null,
            tagsWindowService: null,
            playlists: playlistsVm);

        // Плейлист с >= 2 треками — заводим через реальный репозиторий, потом обновляем VM.
        var playlist = playlistRepo.Create("Смок-плейлист");
        playlistRepo.AddTracks(playlist.Id, new[] { tracks[0].Id, tracks[1].Id });
        playlistsVm.RefreshPlaylists();

        // Включаем "сейчас играет", чтобы у TracksRootTemplate сработал MultiBinding
        // подсветки текущего трека (дефолтное состояние VM после конструктора — TracksRoot).
        vm.PlayTrackCommand.Execute(vm.DisplayedTracks[0]);

        return (vm, playlist);
    }

    // === STA-хелпер (вариант (b) из брифа: без доп. NuGet-зависимости) ===

    private static class StaRunner
    {
        /// <summary>
        /// Гоняет action на отдельном STA-потоке (WPF Application/Window требуют STA)
        /// и перебрасывает любое пойманное исключение обратно на вызывающий поток —
        /// xUnit по умолчанию не создаёт STA-потоки, добавлять Xunit.StaFact ради
        /// одного теста избыточно.
        /// </summary>
        public static void Run(Action action)
        {
            Exception? captured = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    captured = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("WPF render test не завершился за 30 секунд.");

            if (captured is not null)
            {
                throw new Exception($"Исключение на STA-потоке: {captured.Message}", captured);
            }
        }
    }

    // === Фейки (тот же паттерн, что в MainViewModelNavigationTests/PlaylistsViewModelTests) ===

    private sealed class FakeTrackRepo : ITrackRepository
    {
        private readonly List<Track> _tracks;
        public FakeTrackRepo(IEnumerable<Track> seed) => _tracks = seed.ToList();
        public IReadOnlyList<Track> GetAll() => _tracks;
        public Track? FindById(int id) => _tracks.FirstOrDefault(t => t.Id == id);
        public Track Add(Track track) { _tracks.Add(track); return track; }
        public void Update(Track track) { int i = _tracks.FindIndex(t => t.Id == track.Id); if (i >= 0) _tracks[i] = track; }
        public void Remove(int id) => _tracks.RemoveAll(t => t.Id == id);
    }

    private sealed class FakeFileService : IFileService
    {
        public bool Exists(string path) => true;
        public OperationResult Copy(string sourcePath, string targetPath, bool overwrite) => OperationResult.Success("ok");
        public string GetFileName(string path) => Path.GetFileName(path) ?? "track.mp3";
        public OperationResult Delete(string path) => OperationResult.Success("deleted");
    }

    private sealed class FakeSaveFileDialogService : ISaveFileDialogService
    {
        public string? PickSavePath(string suggestedFileName) => null;
    }

    private sealed class FakeAudioPlayerService : IAudioPlayerService
    {
        public event EventHandler<string>? MediaOpened { add { } remove { } }
        public event EventHandler? MediaEnded { add { } remove { } }
        public event EventHandler<string>? MediaFailed { add { } remove { } }

        public bool IsPlaying => false;
        public TimeSpan Position { get; set; }
        public TimeSpan Duration => TimeSpan.FromSeconds(100);
        public double Volume { get; set; } = 1;
        public bool IsMuted { get; set; }
        public string? LastOpenedFilePath { get; private set; }

        public OperationResult Open(string filePath) { LastOpenedFilePath = filePath; return OperationResult.Success("opened"); }
        public OperationResult Play() => OperationResult.Success("playing");
        public void Pause() { }
        public void Stop() { }
        public void Dispose() { }
    }

    private sealed class FakeListeningHistoryRepository : IListeningHistoryRepository
    {
        private readonly List<PlaybackEntry> _entries = new();
        public IReadOnlyList<PlaybackEntry> GetRecent(int limit = 50) => _entries.OrderByDescending(e => e.PlayedAt).Take(limit).ToList();
        public IReadOnlyList<PlaybackEntry> GetAll() => _entries.OrderByDescending(e => e.PlayedAt).ToList();
        public IReadOnlyList<ListeningStats> GetTop(int limit = 50) => Array.Empty<ListeningStats>();
        public IReadOnlyList<PlaybackEntry> GetRecentUnique(int limit = 50) => Array.Empty<PlaybackEntry>();
        public IReadOnlyList<Track> GetNeverPlayed() => Array.Empty<Track>();
        public void Append(PlaybackEntry entry) => _entries.Add(entry);
    }

    private sealed class FakePlayerSettingsRepository : IPlayerSettingsRepository
    {
        public PlayerSettings Load() => PlayerSettings.Default;
        public void Save(PlayerSettings settings) { }
        public int? LoadActiveViewIndex() => null;
        public void SaveActiveView(MainViewMode view) { }
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private sealed class YesConfirmationService : IConfirmationService
    {
        public bool Confirm(string title, string message) => true;
    }

    // Минимальный in-memory IPlaylistRepository — скопирован из PlaylistsViewModelTests
    // (там private, поэтому не переиспользуем напрямую).
    private sealed class FakePlaylistRepo : IPlaylistRepository
    {
        private readonly List<Playlist> _pl = new();
        private readonly Dictionary<int, List<int>> _tracks = new();
        private int _next = 1;
        public IReadOnlyList<Playlist> GetAll() => _pl.OrderByDescending(p => p.Id).ToList();
        public Playlist? GetById(int id) => _pl.FirstOrDefault(p => p.Id == id);
        public Playlist Create(string name) { var p = new Playlist(_next++, name, DateTime.UtcNow); _pl.Add(p); _tracks[p.Id] = new(); return p; }
        public void Rename(int id, string name) { var i = _pl.FindIndex(p => p.Id == id); if (i >= 0) _pl[i] = _pl[i] with { Name = name }; }
        public void Delete(int id) { _pl.RemoveAll(p => p.Id == id); _tracks.Remove(id); }
        public IReadOnlyList<Track> GetTracks(int id) => _tracks[id].Select(tid => new Track { Id = tid, Title = "t" + tid, FilePath = tid + ".mp3" }).ToList();
        public int GetTrackCount(int id) => _tracks[id].Count;
        public void AddTrack(int id, int tid) => _tracks[id].Add(tid);
        public void AddTracks(int id, IReadOnlyList<int> ids) => _tracks[id].AddRange(ids);
        public void RemoveAt(int id, int pos) => _tracks[id].RemoveAt(pos);
        public void Reorder(int id, int from, int to) { var l = _tracks[id]; var x = l[from]; l.RemoveAt(from); l.Insert(to, x); }
    }
}
