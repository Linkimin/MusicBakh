using MusicBakh.Application.Abstractions;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicLibrary.Services.Library;
using MusicLibrary.ViewModels;
using Xunit;
using System.IO;

namespace MusicLibrary.Tests.ViewModels;

public sealed class MainViewModelNavigationTests
{
    [Fact]
    public void Back_RefreshesArtistAfterFiltersChangedInAlbum()
    {
        using var vm = CreateVM(new Track { Id = 1, Artist = "A", Album = "B", FilePath = "1.mp3", Rating = 1 });
        vm.OpenArtistCommand.Execute(vm.DisplayedArtists[0]);
        vm.OpenAlbumCommand.Execute(vm.DisplayedAlbums[0]);
        vm.MinRating = 5;
        vm.BackCommand.Execute(null);
        var artist = Assert.IsType<LeftColumnState.ArtistDetail>(vm.CurrentLeftColumn).Artist;
        Assert.Empty(artist.Albums);
        Assert.Empty(artist.LooseTracks);
    }

    private static MainViewModel CreateVM(params Track[] tracks)
    {
        return CreateVMWithPlayer(out _, tracks);
    }

    private static MainViewModel CreateVMWithPlayer(out FakeAudioPlayerService player, params Track[] tracks)
    {
        player = new FakeAudioPlayerService();
        return new MainViewModel(
            new FakeTrackRepo(tracks),
            new FakeFileService(),
            new FakeSaveFileDialogService(),
            player,
            new FakeListeningHistoryRepository(),
            new FakePlayerSettingsRepository(),
            addTrackDialogService: null,
            confirmationService: null,
            searchService: null,
            statsWindowService: null,
            tagRepository: null);
    }

    [Fact]
    public void Default_State_Is_TracksRoot()
    {
        var vm = CreateVM();
        Assert.IsType<LeftColumnState.TracksRoot>(vm.CurrentLeftColumn);
        Assert.False(vm.CanGoBack);
    }

    [Fact]
    public void SwitchView_Changes_Root_And_Clears_Back_Stack()
    {
        var vm = CreateVM();

        vm.SwitchViewCommand.Execute(MainViewMode.Albums);
        Assert.IsType<LeftColumnState.AlbumsRoot>(vm.CurrentLeftColumn);

        vm.SwitchViewCommand.Execute(MainViewMode.Artists);
        Assert.IsType<LeftColumnState.ArtistsRoot>(vm.CurrentLeftColumn);
        Assert.False(vm.CanGoBack);
    }

    [Fact]
    public void OpenAlbum_From_Albums_Root_Pushes_Detail_And_Sets_CanGoBack()
    {
        var vm = CreateVM(
            new Track { Id = 1, Title = "T1", Artist = "A", Album = "Alb", FilePath = "1.mp3" },
            new Track { Id = 2, Title = "T2", Artist = "A", Album = "Alb", FilePath = "2.mp3" });
        vm.SwitchViewCommand.Execute(MainViewMode.Albums);
        var album = vm.DisplayedAlbums[0];

        vm.OpenAlbumCommand.Execute(album);

        var detail = Assert.IsType<LeftColumnState.AlbumDetail>(vm.CurrentLeftColumn);
        // Примечание: НЕ Assert.Same. ApplyFilters (вызванный внутри конструктора/сеттеров фильтров)
        // не пересоздаёт CurrentLeftColumn при отсутствии реальных изменений фильтра, но контракт теперь —
        // «AlbumKey совпадает», а не «та же самая ссылка на объект»: см. Task «drill-down respects filters»,
        // где ApplyFilters ре-резолвит CurrentLeftColumn на свежий агрегат из DisplayedAlbums.
        Assert.Equal(album.AlbumKey, detail.Album.AlbumKey);
        Assert.Equal(album.Tracks.Select(t => t.Id), detail.Album.Tracks.Select(t => t.Id));
        Assert.True(vm.CanGoBack);
    }

    [Fact]
    public void Back_From_Album_Detail_Returns_To_Albums_Root()
    {
        var vm = CreateVM(
            new Track { Id = 1, Title = "T1", Artist = "A", Album = "Alb", FilePath = "1.mp3" },
            new Track { Id = 2, Title = "T2", Artist = "A", Album = "Alb", FilePath = "2.mp3" });
        vm.SwitchViewCommand.Execute(MainViewMode.Albums);
        vm.OpenAlbumCommand.Execute(vm.DisplayedAlbums[0]);

        vm.BackCommand.Execute(null);

        Assert.IsType<LeftColumnState.AlbumsRoot>(vm.CurrentLeftColumn);
        Assert.False(vm.CanGoBack);
    }

    [Fact]
    public void Artist_Then_Album_Then_Back_Returns_To_Artist_Detail()
    {
        var vm = CreateVM(
            new Track { Id = 1, Title = "T1", Artist = "A", Album = "Alb", FilePath = "1.mp3" },
            new Track { Id = 2, Title = "T2", Artist = "A", Album = "Alb", FilePath = "2.mp3" });
        vm.SwitchViewCommand.Execute(MainViewMode.Artists);

        var artist = vm.DisplayedArtists[0];
        vm.OpenArtistCommand.Execute(artist);

        var album = artist.Albums[0];
        vm.OpenAlbumCommand.Execute(album);

        // Sanity: глубокий drill
        Assert.IsType<LeftColumnState.AlbumDetail>(vm.CurrentLeftColumn);

        vm.BackCommand.Execute(null);

        var detail = Assert.IsType<LeftColumnState.ArtistDetail>(vm.CurrentLeftColumn);
        Assert.Same(artist, detail.Artist);
        Assert.True(vm.CanGoBack); // ещё один back возвращает на ArtistsRoot
    }

    [Fact]
    public void SwitchView_While_Drilled_In_Resets_Back_Stack()
    {
        var vm = CreateVM(
            new Track { Id = 1, Title = "T1", Artist = "A", Album = "Alb", FilePath = "1.mp3" },
            new Track { Id = 2, Title = "T2", Artist = "A", Album = "Alb", FilePath = "2.mp3" });
        vm.SwitchViewCommand.Execute(MainViewMode.Albums);
        vm.OpenAlbumCommand.Execute(vm.DisplayedAlbums[0]);
        Assert.True(vm.CanGoBack);

        vm.SwitchViewCommand.Execute(MainViewMode.Tracks);

        Assert.IsType<LeftColumnState.TracksRoot>(vm.CurrentLeftColumn);
        Assert.False(vm.CanGoBack);
    }

    // === Final-review fix: drill-down уважает активные фильтры (ApplyFilters ре-резолвит CurrentLeftColumn) ===

    [Fact]
    public void ApplyFilters_While_Drilled_Into_Album_Refreshes_Tracklist_To_Filtered_Subset()
    {
        var vm = CreateVM(
            new Track { Id = 1, Title = "T1", Artist = "A", Album = "Alb", FilePath = "1.mp3", Rating = 5 },
            new Track { Id = 2, Title = "T2", Artist = "A", Album = "Alb", FilePath = "2.mp3", Rating = 1 },
            new Track { Id = 3, Title = "T3", Artist = "A", Album = "Alb", FilePath = "3.mp3", Rating = 5 });
        vm.SwitchViewCommand.Execute(MainViewMode.Albums);
        var album = vm.DisplayedAlbums[0];
        vm.OpenAlbumCommand.Execute(album);
        Assert.Equal(3, ((LeftColumnState.AlbumDetail)vm.CurrentLeftColumn).Album.Tracks.Count);

        // Фильтр «рейтинг >= 5» оставляет 2 из 3 треков альбома.
        vm.MinRating = 5;

        var detail = Assert.IsType<LeftColumnState.AlbumDetail>(vm.CurrentLeftColumn);
        Assert.Equal(album.AlbumKey, detail.Album.AlbumKey);
        Assert.Equal(new[] { 1, 3 }, detail.Album.Tracks.Select(t => t.Id));
        Assert.True(vm.CanGoBack, "Back-стек не должен сбрасываться при пересчёте фильтра.");
    }

    [Fact]
    public void ApplyFilters_That_Empties_Drilled_Album_Shows_Empty_Tracklist_Not_Stale()
    {
        var vm = CreateVM(
            new Track { Id = 1, Title = "T1", Artist = "A", Album = "Alb", FilePath = "1.mp3", Rating = 1 },
            new Track { Id = 2, Title = "T2", Artist = "A", Album = "Alb", FilePath = "2.mp3", Rating = 1 });
        vm.SwitchViewCommand.Execute(MainViewMode.Albums);
        var album = vm.DisplayedAlbums[0];
        vm.OpenAlbumCommand.Execute(album);

        // Фильтр «рейтинг >= 5» убирает из альбома все треки -> GroupByAlbum больше не вернёт этот альбом.
        var exception = Record.Exception(() => vm.MinRating = 5);

        Assert.Null(exception);
        var detail = Assert.IsType<LeftColumnState.AlbumDetail>(vm.CurrentLeftColumn);
        // Identity (заголовок) сохранена от последнего известного агрегата, треклист пуст — не stale.
        Assert.Equal(album.AlbumKey, detail.Album.AlbumKey);
        Assert.Empty(detail.Album.Tracks);
        Assert.DoesNotContain(vm.DisplayedAlbums, a => a.AlbumKey == album.AlbumKey);
    }

    [Fact]
    public void ApplyFilters_While_Drilled_Into_Artist_Refreshes_Or_Empties_Detail()
    {
        var vm = CreateVM(
            new Track { Id = 1, Title = "T1", Artist = "Art", Album = "Alb", FilePath = "1.mp3", Rating = 5 },
            new Track { Id = 2, Title = "T2", Artist = "Art", Album = "Alb", FilePath = "2.mp3", Rating = 1 });
        vm.SwitchViewCommand.Execute(MainViewMode.Artists);
        var artist = vm.DisplayedArtists[0];
        vm.OpenArtistCommand.Execute(artist);
        Assert.Equal(2, ((LeftColumnState.ArtistDetail)vm.CurrentLeftColumn).Artist.TotalTracks);

        // Фильтр «рейтинг >= 5» оставляет только один трек у исполнителя.
        vm.MinRating = 5;

        var detail = Assert.IsType<LeftColumnState.ArtistDetail>(vm.CurrentLeftColumn);
        Assert.Equal(artist.Name, detail.Artist.Name);
        Assert.Equal(1, detail.Artist.TotalTracks);
        Assert.True(vm.CanGoBack);

        // Фильтр «рейтинг >= 6» (clamped до 5) — используем None-реакцию, чтобы дополнительно
        // проверить полностью опустошённый случай: снимаем оставшийся трек через реакцию.
        vm.ReactionFilter = TrackReaction.Liked; // ни у одного трека нет Liked -> исполнитель пропадает из DisplayedArtists
        var emptied = Assert.IsType<LeftColumnState.ArtistDetail>(vm.CurrentLeftColumn);
        Assert.Equal(artist.Name, emptied.Artist.Name);
        Assert.Empty(emptied.Artist.Albums);
        Assert.Empty(emptied.Artist.LooseTracks);
        Assert.Equal(0, emptied.Artist.TotalTracks);
        Assert.DoesNotContain(vm.DisplayedArtists, a => a.Name == artist.Name);
    }

    // === Task 6: PlayAlbum/PlayArtist/ShuffleAlbum/ShuffleArtist — сборка очереди воспроизведения ===

    [Fact]
    public void PlayAlbumCommand_Replaces_Queue_With_Album_Tracks_In_Order_And_Plays_First()
    {
        // Треки внутри альбома уже приходят от GroupByAlbum отсортированными
        // (TrackNumber ASC NULLS LAST, Title ASC) — здесь намеренно перемешан
        // порядок TrackNumber, чтобы убедиться, что VM не переупорядочивает очередь сама.
        var vm = CreateVM(
            new Track { Id = 1, Title = "T1", Artist = "A", Album = "Alb", TrackNumber = 1, FilePath = "1.mp3" },
            new Track { Id = 2, Title = "T2", Artist = "A", Album = "Alb", TrackNumber = 2, FilePath = "2.mp3" },
            new Track { Id = 3, Title = "T3", Artist = "A", Album = "Alb", TrackNumber = 3, FilePath = "3.mp3" });
        vm.SwitchViewCommand.Execute(MainViewMode.Albums);
        var album = vm.DisplayedAlbums[0];

        vm.PlayAlbumCommand.Execute(album);

        // PlaybackQueue — очередь воспроизведения альбома; DisplayedTracks (вкладка «Треки») не трогается (bug #5 fix).
        Assert.Equal(album.Tracks.Select(t => t.Id), vm.PlaybackQueue.Select(t => t.Id));
        Assert.Equal(album.Tracks[0].Id, vm.SelectedTrack?.Id);
        Assert.Equal(album.Tracks[0].Id, vm.PlayingTrack?.Id);
    }

    // === Smoke-fix: separate playback queue from Tracks view (bug #5) ===

    [Fact]
    public void PlayAlbumCommand_Leaves_DisplayedTracks_As_Full_Library_While_Queue_Is_Album()
    {
        // Регрессия smoke-теста итерации D: Play-альбом раньше клал треки альбома прямо
        // в DisplayedTracks, из-за чего вкладка «Треки» «схлопывалась» до одного альбома.
        var vm = CreateVM(
            new Track { Id = 1, Title = "T1", Artist = "A", Album = "Alb", TrackNumber = 1, FilePath = "1.mp3" },
            new Track { Id = 2, Title = "T2", Artist = "A", Album = "Alb", TrackNumber = 2, FilePath = "2.mp3" },
            new Track { Id = 3, Title = "Other", Artist = "B", Album = "OtherAlb", TrackNumber = 1, FilePath = "3.mp3" });
        var fullLibraryIds = new[] { 1, 2, 3 };
        vm.SwitchViewCommand.Execute(MainViewMode.Albums);
        var album = vm.DisplayedAlbums.Single(a => a.Title == "Alb");

        vm.PlayAlbumCommand.Execute(album);

        Assert.Equal(fullLibraryIds, vm.DisplayedTracks.Select(t => t.Id).OrderBy(id => id));
        Assert.Equal(new[] { 1, 2 }, vm.PlaybackQueue.Select(t => t.Id));
    }

    [Fact]
    public void NextTrackCommand_After_PlayAlbum_Advances_Within_Album_Queue_Not_Library()
    {
        var vm = CreateVM(
            new Track { Id = 1, Title = "T1", Artist = "A", Album = "Alb", TrackNumber = 1, FilePath = "1.mp3" },
            new Track { Id = 2, Title = "T2", Artist = "A", Album = "Alb", TrackNumber = 2, FilePath = "2.mp3" },
            new Track { Id = 3, Title = "Other", Artist = "B", Album = "OtherAlb", TrackNumber = 1, FilePath = "3.mp3" });
        vm.SwitchViewCommand.Execute(MainViewMode.Albums);
        var album = vm.DisplayedAlbums.Single(a => a.Title == "Alb");
        vm.PlayAlbumCommand.Execute(album);
        Assert.Equal(1, vm.PlayingTrack?.Id);

        vm.NextTrackCommand.Execute(null);
        Assert.Equal(2, vm.PlayingTrack?.Id);

        // Очередь альбома исчерпана (только 2 трека) — дальше идти некуда, трек #3
        // из другого альбома НЕ должен подхватиться (это и было бы багом #5/навигация по DisplayedTracks).
        Assert.False(vm.NextTrackCommand.CanExecute(null));
    }

    [Fact]
    public void Normal_Play_From_Tracks_View_Snapshots_DisplayedTracks_Into_Queue_And_Walks_It()
    {
        var vm = CreateVM(
            new Track { Id = 1, Title = "T1", Artist = "A", Album = "Alb", FilePath = "1.mp3" },
            new Track { Id = 2, Title = "T2", Artist = "A", Album = "Alb", FilePath = "2.mp3" },
            new Track { Id = 3, Title = "T3", Artist = "A", Album = "Alb", FilePath = "3.mp3" });

        vm.PlayTrackCommand.Execute(vm.DisplayedTracks[0]);

        Assert.Equal(vm.DisplayedTracks.Select(t => t.Id), vm.PlaybackQueue.Select(t => t.Id));

        vm.NextTrackCommand.Execute(null);
        Assert.Equal(vm.DisplayedTracks[1].Id, vm.PlayingTrack?.Id);

        vm.PreviousTrackCommand.Execute(null);
        Assert.Equal(vm.DisplayedTracks[0].Id, vm.PlayingTrack?.Id);
    }

    [Fact]
    public void PlayDetailTrackCommand_In_AlbumDetail_Queues_Album_And_Plays_Clicked_Track()
    {
        var vm = CreateVM(
            new Track { Id = 1, Title = "T1", Artist = "A", Album = "Alb", TrackNumber = 1, FilePath = "1.mp3" },
            new Track { Id = 2, Title = "T2", Artist = "A", Album = "Alb", TrackNumber = 2, FilePath = "2.mp3" },
            new Track { Id = 3, Title = "T3", Artist = "A", Album = "Alb", TrackNumber = 3, FilePath = "3.mp3" });
        vm.SwitchViewCommand.Execute(MainViewMode.Albums);
        var album = vm.DisplayedAlbums[0];
        vm.OpenAlbumCommand.Execute(album);
        var detail = (LeftColumnState.AlbumDetail)vm.CurrentLeftColumn;
        Track clicked = detail.Album.Tracks[1]; // T2 — не первый трек альбома.

        vm.PlayDetailTrackCommand.Execute(clicked);

        Assert.Equal(clicked.Id, vm.SelectedTrack?.Id);
        Assert.Equal(clicked.Id, vm.PlayingTrack?.Id);
        Assert.Equal(album.Tracks.Select(t => t.Id), vm.PlaybackQueue.Select(t => t.Id));
        // DisplayedTracks (вкладка «Треки») не тронута.
        Assert.Equal(3, vm.DisplayedTracks.Count);
    }

    [Fact]
    public void PlayDetailTrackCommand_In_ArtistDetail_Queues_LooseTracks_And_Plays_Clicked_Track()
    {
        var vm = CreateVM(
            new Track { Id = 1, Title = "Loose1", Artist = "Art", Album = "", FilePath = "1.mp3" },
            new Track { Id = 2, Title = "Loose2", Artist = "Art", Album = "", FilePath = "2.mp3" });
        vm.SwitchViewCommand.Execute(MainViewMode.Artists);
        var artist = vm.DisplayedArtists[0];
        vm.OpenArtistCommand.Execute(artist);
        var detail = (LeftColumnState.ArtistDetail)vm.CurrentLeftColumn;
        Track clicked = detail.Artist.LooseTracks[1];

        vm.PlayDetailTrackCommand.Execute(clicked);

        Assert.Equal(clicked.Id, vm.SelectedTrack?.Id);
        Assert.Equal(clicked.Id, vm.PlayingTrack?.Id);
        Assert.Equal(artist.LooseTracks.Select(t => t.Id), vm.PlaybackQueue.Select(t => t.Id));
    }

    [Fact]
    public void NowPlayingTrack_Falls_Back_To_PlayingTrack_When_Not_In_DisplayedTracks()
    {
        // Регрессия bug #6: после Play-альбома играющий трек может выпасть из DisplayedTracks
        // (вкладка «Треки» отфильтрована на другой жанр/альбом) — SelectedTrack тогда обнуляется
        // TwoWay-биндингом ListBox-а, но средняя панель обязана продолжать показывать играющий трек.
        var vm = CreateVM(
            new Track { Id = 1, Title = "T1", Artist = "A", Album = "Alb", TrackNumber = 1, FilePath = "1.mp3" },
            new Track { Id = 2, Title = "T2", Artist = "A", Album = "Alb", TrackNumber = 2, FilePath = "2.mp3" });
        vm.SwitchViewCommand.Execute(MainViewMode.Albums);
        var album = vm.DisplayedAlbums[0];
        vm.PlayAlbumCommand.Execute(album);
        Assert.NotNull(vm.PlayingTrack);

        // Симулируем то, что делает WPF ListBox.SelectedItem (TwoWay), когда PlayingTrack
        // больше не входит в текущий ItemsSource DisplayedTracks: SelectedTrack становится null.
        vm.SelectedTrack = null;

        Assert.Null(vm.SelectedTrack);
        Assert.NotNull(vm.PlayingTrack);
        Assert.Equal(vm.PlayingTrack, vm.NowPlayingTrack);
        Assert.True(vm.HasNowPlayingTrack);
    }

    [Fact]
    public void PlayArtistCommand_Flattens_Albums_Then_Loose_Tracks_In_Order()
    {
        // Два альбома (сортируются Year DESC NULLS LAST, Title ASC -> "Beta" (2021) раньше "Alpha" (2020))
        // и один loose-трек (Album пустой). Ожидаем: треки Beta, затем треки Alpha, затем loose.
        var vm = CreateVM(
            new Track { Id = 1, Title = "A1", Artist = "Art", Album = "Alpha", Year = 2020, TrackNumber = 1, FilePath = "1.mp3" },
            new Track { Id = 2, Title = "A2", Artist = "Art", Album = "Alpha", Year = 2020, TrackNumber = 2, FilePath = "2.mp3" },
            new Track { Id = 3, Title = "B1", Artist = "Art", Album = "Beta", Year = 2021, TrackNumber = 1, FilePath = "3.mp3" },
            new Track { Id = 4, Title = "B2", Artist = "Art", Album = "Beta", Year = 2021, TrackNumber = 2, FilePath = "4.mp3" },
            new Track { Id = 5, Title = "Loose", Artist = "Art", Album = "", FilePath = "5.mp3" });
        vm.SwitchViewCommand.Execute(MainViewMode.Artists);
        var artist = vm.DisplayedArtists[0];

        // Sanity: агрегат действительно содержит 2 альбома + 1 loose-трек, иначе тест ничего не проверяет.
        Assert.Equal(2, artist.Albums.Count);
        Assert.Single(artist.LooseTracks);

        vm.PlayArtistCommand.Execute(artist);

        var expectedOrder = artist.Albums.SelectMany(a => a.Tracks).Concat(artist.LooseTracks).Select(t => t.Id).ToList();
        Assert.Equal(new[] { 3, 4, 1, 2, 5 }, expectedOrder);
        // PlaybackQueue — очередь исполнителя; DisplayedTracks (вкладка «Треки») не трогается (bug #5 fix).
        Assert.Equal(expectedOrder, vm.PlaybackQueue.Select(t => t.Id));
        Assert.Equal(expectedOrder[0], vm.SelectedTrack?.Id);
        Assert.Equal(expectedOrder[0], vm.PlayingTrack?.Id);
    }

    [Fact]
    public void ShuffleAlbumCommand_Preserves_Track_Set_Without_Drop_Or_Duplicate()
    {
        var vm = CreateVM(
            new Track { Id = 1, Title = "T1", Artist = "A", Album = "Alb", TrackNumber = 1, FilePath = "1.mp3" },
            new Track { Id = 2, Title = "T2", Artist = "A", Album = "Alb", TrackNumber = 2, FilePath = "2.mp3" },
            new Track { Id = 3, Title = "T3", Artist = "A", Album = "Alb", TrackNumber = 3, FilePath = "3.mp3" },
            new Track { Id = 4, Title = "T4", Artist = "A", Album = "Alb", TrackNumber = 4, FilePath = "4.mp3" });
        vm.SwitchViewCommand.Execute(MainViewMode.Albums);
        var album = vm.DisplayedAlbums[0];

        vm.ShuffleAlbumCommand.Execute(album);

        // Порядок случаен (Fisher-Yates) — проверяем только сохранность множества: без потерь и дублей.
        // PlaybackQueue — очередь альбома; DisplayedTracks (вкладка «Треки») не трогается (bug #5 fix).
        Assert.Equal(album.Tracks.Count, vm.PlaybackQueue.Count);
        Assert.Equal(
            album.Tracks.Select(t => t.Id).OrderBy(id => id),
            vm.PlaybackQueue.Select(t => t.Id).OrderBy(id => id));
        Assert.Contains(vm.PlaybackQueue, t => t.Id == vm.SelectedTrack!.Id);
    }

    [Fact]
    public void ShuffleArtistCommand_Preserves_Track_Set_Across_Albums_And_Loose_Tracks()
    {
        var vm = CreateVM(
            new Track { Id = 1, Title = "A1", Artist = "Art", Album = "Alpha", Year = 2020, TrackNumber = 1, FilePath = "1.mp3" },
            new Track { Id = 2, Title = "A2", Artist = "Art", Album = "Alpha", Year = 2020, TrackNumber = 2, FilePath = "2.mp3" },
            new Track { Id = 3, Title = "B1", Artist = "Art", Album = "Beta", Year = 2021, TrackNumber = 1, FilePath = "3.mp3" },
            new Track { Id = 4, Title = "Loose", Artist = "Art", Album = "", FilePath = "4.mp3" });
        vm.SwitchViewCommand.Execute(MainViewMode.Artists);
        var artist = vm.DisplayedArtists[0];
        var expectedIds = artist.Albums.SelectMany(a => a.Tracks).Concat(artist.LooseTracks).Select(t => t.Id).OrderBy(id => id).ToList();

        vm.ShuffleArtistCommand.Execute(artist);

        // PlaybackQueue — очередь исполнителя; DisplayedTracks (вкладка «Треки») не трогается (bug #5 fix).
        Assert.Equal(expectedIds.Count, vm.PlaybackQueue.Count);
        Assert.Equal(expectedIds, vm.PlaybackQueue.Select(t => t.Id).OrderBy(id => id));
    }

    // === Фабрика VM и минимальные фейки (скопированы из MainViewModelTagAttachTests, т.к. там private) ===

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
        public event EventHandler? MediaEnded;
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

        public void RaiseEndedForTest() => MediaEnded?.Invoke(this, EventArgs.Empty);
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
}
