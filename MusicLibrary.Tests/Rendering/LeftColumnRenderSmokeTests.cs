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
        });
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
        }
        finally
        {
            window.Close();
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
    /// Мы не вызываем Run()/OnStartup — значит Host/DI/MainWindow из App.xaml.cs не создаются,
    /// только резолвится дерево ресурсов.
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

        var app = new MusicLibrary.App();
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
            new Track { Id = 1, Title = "Track1", Artist = "Artist1", Album = "Album1", TrackNumber = 1, FilePath = "1.mp3" },
            new Track { Id = 2, Title = "Track2", Artist = "Artist1", Album = "Album1", TrackNumber = 2, FilePath = "2.mp3" },
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
            thread.Join();

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
        public event EventHandler<string>? MediaOpened;
        public event EventHandler? MediaEnded;
        public event EventHandler<string>? MediaFailed;

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
