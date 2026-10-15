using MusicBakh.Application.Abstractions;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicLibrary.ViewModels;
using System.IO;
using System.Reflection;

namespace MusicLibrary.Tests;

public sealed class MainViewModelTests
{
    [Theory]
    [InlineData("next")]
    [InlineData("double-click")]
    [InlineData("album")]
    public void MissingNextFile_PreservesCurrentQueueAndControls(string action)
    {
        var tracks = new[]
        {
            new Track { Id = 1, Title = "Available", FilePath = "1.mp3" },
            new Track { Id = 2, Title = "Missing", FilePath = "missing.mp3", Artist = "A", Album = "B" }
        };
        var player = new FakeAudioPlayerService();
        using var vm = CreateViewModel(tracks, player, new FakeFileService("missing.mp3"));
        vm.PlayTrackCommand.Execute(tracks[0]);
        if (action == "next") vm.NextTrackCommand.Execute(null);
        else if (action == "album") vm.PlayAlbumCommand.Execute(vm.DisplayedAlbums.Single());
        else vm.PlayTrackCommand.Execute(tracks[1]);
        Assert.Equal(1, vm.PlayingTrack?.Id);
        Assert.True(vm.IsPlaying);
        Assert.False(vm.PreviousTrackCommand.CanExecute(null));
        Assert.True(vm.NextTrackCommand.CanExecute(null));
        Assert.Equal(new[] { 1, 2 }, vm.PlaybackQueue.Select(t => t.Id));
    }

    [Fact]
    public void GlobalTransport_PausesPlayingTrackWithoutChangingSelection()
    {
        var (vm, player, _) = CreateViewModelWithPlayer();
        using (vm)
        {
            vm.PlayTrackCommand.Execute(vm.DisplayedTracks[0]);
            vm.SelectedTrack = vm.DisplayedTracks[1];
            vm.TogglePlaybackCommand.Execute(null);
            Assert.False(player.IsPlaying);
            Assert.Equal(2, vm.SelectedTrack.Id);
            Assert.Equal(1, vm.PlayingTrack?.Id);
        }
    }
    [Fact]
    public void DeleteLibraryEntry_PreservesExternalOriginal()
    {
        string path = Path.GetTempFileName();
        try
        {
            var track = new Track { Id = 1, FilePath = path };
            using var vm = new MainViewModel(new RecordingTrackRepository(new[] { track }),
                new MusicBakh.Infrastructure.FileSystem.FileService(), new FakeSaveFileDialogService(),
                new FakeAudioPlayerService(), new FakeListeningHistoryRepository(), new FakePlayerSettingsRepository());
            vm.SelectedTrack = track;
            vm.DeleteTrackCommand.Execute(null);
            Assert.True(File.Exists(path));
            Assert.Empty(vm.DisplayedTracks);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SettingsFailure_DoesNotInterruptVolumeChange()
    {
        var (vm, player, repo) = CreateViewModelWithPlayer();
        using (vm)
        {
            repo.SaveError = new IOException("disk unavailable");
            vm.Volume = 0.25;
            Assert.Equal(0.25, player.Volume);
            Assert.Equal(OperationMessageKind.Error, vm.StatusKind);
        }
    }

    [Fact]
    public void AddTrack_RefreshesAlbums_AndRespectsRatingFilter()
    {
        using var vm = CreateViewModel();
        vm.MinRating = 5;
        vm.AddTrack(new Track { Id = 100, Title = "New", Album = "New album", Artist = "New artist" });
        Assert.Empty(vm.DisplayedTracks);
        vm.MinRating = 0;
        Assert.Contains(vm.DisplayedAlbums, a => a.Title == "New album");
    }

    [Fact]
    public void DeleteTrack_RemovesCanonicalTrackFromAlbumsQueueAndHistory()
    {
        var a = new Track { Id = 1, Album = "Album", Artist = "A", FilePath = "1.mp3" };
        var b = new Track { Id = 2, Album = "Album", Artist = "A", FilePath = "2.mp3" };
        var repo = new RecordingTrackRepository(new[] { a, b });
        using var vm = CreateViewModelWithRepo(repo);
        vm.PlayAlbumCommand.Execute(vm.DisplayedAlbums[0]);
        // Репозитории истории возвращают другой экземпляр того же трека.
        vm.SelectedTrack = new Track { Id = 2, Album = "Album", Artist = "A", FilePath = "2.mp3" };
        vm.DeleteTrackCommand.Execute(null);
        Assert.Single(vm.PlaybackQueue);
        Assert.Single(Assert.Single(vm.DisplayedAlbums).Tracks);
        Assert.DoesNotContain(vm.DisplayedTracks, t => t.Id == 2);
    }

    [Fact]
    public void RatingChange_RefreshesActiveFilterAndAlbum()
    {
        using var vm = CreateViewModelWithRepo(new RecordingTrackRepository(new[] {
            new Track { Id = 1, Album = "Album", Artist = "A", Rating = 5 }
        }));
        vm.MinRating = 5;
        vm.SelectedTrack = vm.DisplayedTracks[0];
        vm.SetRatingCommand.Execute("1");
        Assert.Empty(vm.DisplayedTracks);
        Assert.Empty(vm.DisplayedAlbums);
    }

    [Fact]
    public void Search_FilterDoesNotLoseMatchesBeyondFirst500()
    {
        var tracks = Enumerable.Range(1, 600).Select(i => new Track {
            Id = i, Title = "Song", Genre = i == 600 ? "Jazz" : "Rock"
        }).ToArray();
        using var vm = CreateViewModelWithSearch(tracks, new RankedSearchService(tracks));
        vm.SearchText = "Song";
        vm.SelectedGenre = "Jazz";
        Assert.Equal(600, Assert.Single(vm.DisplayedTracks).Id);
    }

    [Fact]
    public void PauseResume_PreservesAlbumQueue()
    {
        var tracks = new[] {
            new Track { Id = 1, Album = "Album", Artist = "A", FilePath = "1.mp3" },
            new Track { Id = 2, Album = "Album", Artist = "A", FilePath = "2.mp3" },
            new Track { Id = 3, FilePath = "3.mp3" }
        };
        using var vm = CreateViewModel(tracks, new FakeAudioPlayerService(), new FakeFileService());
        vm.PlayAlbumCommand.Execute(vm.DisplayedAlbums[0]);
        vm.PlayPauseCommand.Execute(null);
        vm.PlayPauseCommand.Execute(null);
        Assert.Equal(new[] { 1, 2 }, vm.PlaybackQueue.Select(t => t.Id));
    }

    [Fact]
    public void DuplicateQueueEntries_AdvanceByPosition_AndFinish()
    {
        var a = new Track { Id = 1, FilePath = "1.mp3" };
        var b = new Track { Id = 2, FilePath = "2.mp3" };
        var player = new FakeAudioPlayerService();
        using var vm = CreateViewModel(new[] { a, b }, player, new FakeFileService());
        vm.PlayPlaylistCommand.Execute(new MusicLibrary.Services.Library.PlaylistView(
            new Playlist(1, "Mix", DateTime.UtcNow), new[] { a, a, b }));
        player.RaiseEndedForTest();
        Assert.Equal(1, vm.PlayingTrack?.Id);
        player.RaiseEndedForTest();
        Assert.Equal(2, vm.PlayingTrack?.Id);
        player.RaiseEndedForTest();
        Assert.Null(vm.PlayingTrack);
    }

    [Fact]
    public void Pause_IsAvailable_WhenSelectionCleared()
    {
        var (vm, player, _) = CreateViewModelWithPlayer();
        using (vm)
        {
            vm.PlayTrackCommand.Execute(vm.DisplayedTracks[0]);
            vm.SelectedTrack = null;
            Assert.True(vm.PlayPauseCommand.CanExecute(null));
            vm.PlayPauseCommand.Execute(null);
            Assert.False(player.IsPlaying);
        }
    }

    [Fact]
    public void Constructor_LoadsGenresWithAllGenresFirst()
    {
        var viewModel = CreateViewModel();

        Assert.Equal("Все жанры", viewModel.Genres[0]);
        Assert.Contains("Рок", viewModel.Genres);
        Assert.Contains("Джаз", viewModel.Genres);
    }

    [Fact]
    public void SelectedGenre_FiltersDisplayedTracks()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedGenre = "Рок";

        Assert.All(viewModel.DisplayedTracks, track => Assert.Equal("Рок", track.Genre));
    }

    [Fact]
    public void MinRatingDisplayText_Reflects_Slider_Value()
    {
        var viewModel = CreateViewModel();
        Assert.Equal("Без фильтра", viewModel.MinRatingDisplayText);
        viewModel.MinRating = 4;
        Assert.Equal("≥ 4★", viewModel.MinRatingDisplayText);
    }

    [Fact]
    public void SetReactionFilterCommand_Toggle_Off_When_Same_Reaction_Clicked_Twice()
    {
        var viewModel = CreateViewModel();
        viewModel.SetReactionFilterCommand.Execute("Liked");
        Assert.Equal(TrackReaction.Liked, viewModel.ReactionFilter);
        viewModel.SetReactionFilterCommand.Execute("Liked");
        Assert.Null(viewModel.ReactionFilter);
    }

    [Fact]
    public void SetReactionFilterCommand_Any_Clears_Filter()
    {
        var viewModel = CreateViewModel();
        viewModel.ReactionFilter = TrackReaction.Disliked;
        viewModel.SetReactionFilterCommand.Execute("Any");
        Assert.Null(viewModel.ReactionFilter);
    }

    [Fact]
    public void ClearFiltersCommand_Resets_All_Filters()
    {
        var viewModel = CreateViewModel();
        viewModel.SearchText = "anything";
        viewModel.MinRating = 3;
        viewModel.ReactionFilter = TrackReaction.Liked;

        viewModel.ClearFiltersCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal(0, viewModel.MinRating);
        Assert.Null(viewModel.ReactionFilter);
        Assert.Empty(viewModel.SelectedTagIds);
    }

    [Fact]
    public void SetRatingCommand_Updates_Selected_Track_And_Repository()
    {
        var tracks = new[] { new Track { Id = 1, Title = "T", Artist = "A", FilePath = "1.mp3" } };
        var trackRepo = new RecordingTrackRepository(tracks);
        var viewModel = CreateViewModelWithRepo(trackRepo);
        viewModel.SelectedTrack = viewModel.DisplayedTracks[0];

        viewModel.SetRatingCommand.Execute("4");

        Assert.Equal(4, viewModel.SelectedTrack!.Rating);
        Assert.Equal(4, viewModel.DisplayedTracks[0].Rating);
        Assert.Single(trackRepo.UpdateCalls);
        Assert.Equal(4, trackRepo.UpdateCalls[0].Rating);
    }

    [Fact]
    public void SetRatingCommand_Preserves_Year_TrackNumber_AlbumArtist()
    {
        // Регрессия: CloneTrack в MainViewModel вручную реконструирует Track
        // (sealed, init-only свойства, `with` недоступен) и не был обновлён
        // при добавлении Year/TrackNumber/AlbumArtist в Task 1 итерации D.
        // В результате простановка рейтинга обнуляла эти поля через Update.
        var tracks = new[]
        {
            new Track
            {
                Id = 1,
                Title = "T",
                Artist = "A",
                FilePath = "1.mp3",
                Year = 1999,
                TrackNumber = 7,
                AlbumArtist = "Various Artists"
            }
        };
        var trackRepo = new RecordingTrackRepository(tracks);
        var viewModel = CreateViewModelWithRepo(trackRepo);
        viewModel.SelectedTrack = viewModel.DisplayedTracks[0];

        viewModel.SetRatingCommand.Execute("4");

        Assert.Single(trackRepo.UpdateCalls);
        Assert.Equal(1999, trackRepo.UpdateCalls[0].Year);
        Assert.Equal(7, trackRepo.UpdateCalls[0].TrackNumber);
        Assert.Equal("Various Artists", trackRepo.UpdateCalls[0].AlbumArtist);
    }

    [Fact]
    public void SetRatingCommand_Toggle_Same_Star_Resets_To_Zero()
    {
        var tracks = new[] { new Track { Id = 1, Title = "T", Artist = "A", FilePath = "1.mp3", Rating = 3 } };
        var viewModel = CreateViewModelWithRepo(new RecordingTrackRepository(tracks));
        viewModel.SelectedTrack = viewModel.DisplayedTracks[0];

        viewModel.SetRatingCommand.Execute("3");

        Assert.Equal(0, viewModel.SelectedTrack!.Rating);
    }

    [Fact]
    public void SetMinRatingCommand_Toggle_Same_Value_Resets_To_Zero()
    {
        var tracks = new[] { new Track { Id = 1, Title = "T", Artist = "A", FilePath = "1.mp3" } };
        var viewModel = CreateViewModelWithRepo(new RecordingTrackRepository(tracks));

        viewModel.SetMinRatingCommand.Execute("4");
        Assert.Equal(4, viewModel.MinRating);

        viewModel.SetMinRatingCommand.Execute("4");
        Assert.Equal(0, viewModel.MinRating);
    }

    [Fact]
    public void SetMinRatingCommand_Clamps_Out_Of_Range_To_0_5()
    {
        var tracks = new[] { new Track { Id = 1, Title = "T", Artist = "A", FilePath = "1.mp3" } };
        var viewModel = CreateViewModelWithRepo(new RecordingTrackRepository(tracks));

        viewModel.SetMinRatingCommand.Execute("9");
        Assert.Equal(5, viewModel.MinRating); // clamp вверх

        viewModel.SetMinRatingCommand.Execute("9"); // тот же → сбрасывает в 0 (toggle логика)
        Assert.Equal(0, viewModel.MinRating);
    }

    [Fact]
    public void SetReactionCommand_Toggles_Off_When_Same_Reaction_Clicked_Twice()
    {
        var tracks = new[] { new Track { Id = 1, Title = "T", Artist = "A", FilePath = "1.mp3" } };
        var trackRepo = new RecordingTrackRepository(tracks);
        var viewModel = CreateViewModelWithRepo(trackRepo);
        viewModel.SelectedTrack = viewModel.DisplayedTracks[0];

        viewModel.SetReactionCommand.Execute("Liked");
        Assert.Equal(TrackReaction.Liked, viewModel.SelectedTrack!.Reaction);

        viewModel.SetReactionCommand.Execute("Liked");
        Assert.Equal(TrackReaction.None, viewModel.SelectedTrack!.Reaction);

        Assert.Equal(2, trackRepo.UpdateCalls.Count);
    }

    [Fact]
    public void SearchText_Preserves_SearchService_Order()
    {
        // ISearchService возвращает треки в порядке релевантности (bm25). UI обязан
        // показать их в том же порядке, иначе ORDER BY bm25 в FTS-сервисе бесполезен.
        // Регрессия итерации B: ApplyFilters использовала _allTracks.Where(...) и
        // тем самым возвращала результат в порядке _allTracks (по Id).
        var tracks = new[]
        {
            new Track { Id = 1, Title = "Alpha",   Artist = "A", Genre = "Рок", FilePath = "1.mp3" },
            new Track { Id = 2, Title = "Beta",    Artist = "B", Genre = "Рок", FilePath = "2.mp3" },
            new Track { Id = 3, Title = "Gamma",   Artist = "C", Genre = "Рок", FilePath = "3.mp3" }
        };
        var rankedSearch = new RankedSearchService(new[] { tracks[1], tracks[0], tracks[2] }); // Beta, Alpha, Gamma
        var viewModel = CreateViewModelWithSearch(tracks, rankedSearch);

        viewModel.SearchText = "anything";

        Assert.Equal(3, viewModel.DisplayedTracks.Count);
        Assert.Equal("Beta",  viewModel.DisplayedTracks[0].Title);
        Assert.Equal("Alpha", viewModel.DisplayedTracks[1].Title);
        Assert.Equal("Gamma", viewModel.DisplayedTracks[2].Title);
    }

    [Fact]
    public void SearchText_FiltersDisplayedTracks_ViaSearchService()
    {
        var tracks = new[]
        {
            new Track { Id = 1, Title = "Bohemian Rhapsody", Artist = "Queen",  Genre = "Рок",  FilePath = "1.mp3" },
            new Track { Id = 2, Title = "Radio Ga Ga",       Artist = "Queen",  Genre = "Рок",  FilePath = "2.mp3" },
            new Track { Id = 3, Title = "Take Five",         Artist = "Brubeck", Genre = "Джаз", FilePath = "3.mp3" }
        };
        var viewModel = CreateViewModelWithSearch(tracks, new StubSearchService(tracks));

        viewModel.SearchText = "queen";

        Assert.Equal(2, viewModel.DisplayedTracks.Count);
        Assert.All(viewModel.DisplayedTracks, t => Assert.Equal("Queen", t.Artist));
    }

    [Fact]
    public void SearchText_Combined_With_GenreFilter()
    {
        var tracks = new[]
        {
            new Track { Id = 1, Title = "Rock A", Artist = "Band", Genre = "Рок",  FilePath = "1.mp3" },
            new Track { Id = 2, Title = "Rock B", Artist = "Band", Genre = "Рок",  FilePath = "2.mp3" },
            new Track { Id = 3, Title = "Jazz A", Artist = "Band", Genre = "Джаз", FilePath = "3.mp3" }
        };
        var viewModel = CreateViewModelWithSearch(tracks, new StubSearchService(tracks));

        viewModel.SearchText = "band";        // все три попадают в поиск
        viewModel.SelectedGenre = "Джаз";     // ...но жанр оставляет только один

        Assert.Single(viewModel.DisplayedTracks);
        Assert.Equal("Jazz A", viewModel.DisplayedTracks[0].Title);
    }

    [Fact]
    public void Clearing_SearchText_Restores_Full_Library()
    {
        var tracks = new[]
        {
            new Track { Id = 1, Title = "One",   Artist = "Band", Genre = "Рок", FilePath = "1.mp3" },
            new Track { Id = 2, Title = "Two",   Artist = "Band", Genre = "Рок", FilePath = "2.mp3" },
            new Track { Id = 3, Title = "Three", Artist = "Band", Genre = "Рок", FilePath = "3.mp3" }
        };
        var viewModel = CreateViewModelWithSearch(tracks, new StubSearchService(tracks));

        viewModel.SearchText = "One";
        Assert.Single(viewModel.DisplayedTracks);

        viewModel.SearchText = string.Empty;
        Assert.Equal(3, viewModel.DisplayedTracks.Count);
    }

    [Fact]
    public void SearchText_Without_SearchService_NoOp()
    {
        // Защита: если поисковый сервис не зарегистрирован (DI ещё не пробросил его —
        // переходный период), установка SearchText не должна валить ViewModel.
        var viewModel = CreateViewModel(); // SearchService=null
        viewModel.SearchText = "anything";

        Assert.NotEmpty(viewModel.DisplayedTracks);
    }

    [Fact]
    public void PlayPauseCommand_DoesNotAddHistory_BeforeMediaOpened()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectedTrack = viewModel.DisplayedTracks.First();

        viewModel.PlayPauseCommand.Execute(null);

        Assert.Empty(viewModel.PlaybackHistory);
    }

    [Fact]
    public void MediaOpened_AddsHistoryForPendingTrack()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        viewModel.SelectedTrack = viewModel.DisplayedTracks.First();

        viewModel.PlayPauseCommand.Execute(null);
        player.RaiseOpenedForTest();

        Assert.Single(viewModel.PlaybackHistory);
        Assert.Equal(viewModel.SelectedTrack, viewModel.PlaybackHistory[0].Track);
    }

    [Fact]
    public void MediaOpened_IgnoresStaleTrackOpenEvent()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        Track firstTrack = viewModel.DisplayedTracks[0];
        Track secondTrack = viewModel.DisplayedTracks[1];

        viewModel.SelectedTrack = firstTrack;
        viewModel.PlayPauseCommand.Execute(null);
        viewModel.SelectedTrack = secondTrack;
        viewModel.PlayPauseCommand.Execute(null);
        player.RaiseOpenedForTest(firstTrack.FilePath);

        Assert.Empty(viewModel.PlaybackHistory);

        player.RaiseOpenedForTest(secondTrack.FilePath);

        Assert.Single(viewModel.PlaybackHistory);
        Assert.Equal(secondTrack, viewModel.PlaybackHistory[0].Track);
    }

    [Fact]
    public void MediaFailed_DoesNotAddPendingHistory()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        viewModel.SelectedTrack = viewModel.DisplayedTracks.First();

        viewModel.PlayPauseCommand.Execute(null);
        player.RaiseFailedForTest("unsupported format");
        player.RaiseOpenedForTest(viewModel.SelectedTrack.FilePath);

        Assert.Empty(viewModel.PlaybackHistory);
    }

    [Fact]
    public void StopCommand_ClearsPendingHistory()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        viewModel.SelectedTrack = viewModel.DisplayedTracks.First();
        Track selectedTrack = viewModel.SelectedTrack!;

        viewModel.PlayPauseCommand.Execute(null);
        viewModel.StopCommand.Execute(null);
        player.RaiseOpenedForTest(selectedTrack.FilePath);

        Assert.Empty(viewModel.PlaybackHistory);
    }

    [Fact]
    public void MediaOpened_DoesNotDuplicateHistory_WhenRaisedTwice()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        viewModel.SelectedTrack = viewModel.DisplayedTracks.First();

        viewModel.PlayPauseCommand.Execute(null);
        player.RaiseOpenedForTest();
        player.RaiseOpenedForTest();

        Assert.Single(viewModel.PlaybackHistory);
    }

    [Fact]
    public void PlayPauseCommand_DoesNotDuplicateHistory_WhenResumingAfterPause()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        viewModel.SelectedTrack = viewModel.DisplayedTracks.First();

        viewModel.PlayPauseCommand.Execute(null);
        player.RaiseOpenedForTest();
        viewModel.PlayPauseCommand.Execute(null);
        viewModel.PlayPauseCommand.Execute(null);

        Assert.Single(viewModel.PlaybackHistory);
        Assert.Equal(1, player.OpenCallCount);
        Assert.Equal(2, player.PlayCallCount);
        Assert.Equal(1, player.PauseCallCount);
    }

    [Fact]
    public void ChangingSelectedTrack_DoesNotStopPlayback()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        Track first = viewModel.DisplayedTracks[0];
        Track second = viewModel.DisplayedTracks[1];

        viewModel.SelectedTrack = first;
        viewModel.PlayPauseCommand.Execute(null);
        player.RaiseOpenedForTest();

        Assert.True(viewModel.IsPlaying);

        viewModel.SelectedTrack = second;

        Assert.True(viewModel.IsPlaying);
        Assert.Equal(first, viewModel.PlayingTrack);
        Assert.False(viewModel.IsSelectedPlaying);
        Assert.True(viewModel.ShowOtherPlayingBadge);
    }

    [Fact]
    public void ChangingGenre_DoesNotStopPlayback()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        viewModel.SelectedTrack = viewModel.DisplayedTracks.First(t => t.Genre == "Рок");

        viewModel.PlayPauseCommand.Execute(null);
        player.RaiseOpenedForTest();
        Assert.True(viewModel.IsPlaying);

        viewModel.SelectedGenre = "Джаз";

        Assert.True(viewModel.IsPlaying);
        Assert.NotNull(viewModel.PlayingTrack);
    }

    [Fact]
    public void PlayPauseCommand_ChangingTrack_StopsPreviousAndStartsNew()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        Track first = viewModel.DisplayedTracks[0];
        Track second = viewModel.DisplayedTracks[1];

        viewModel.SelectedTrack = first;
        viewModel.PlayPauseCommand.Execute(null);
        player.RaiseOpenedForTest();

        viewModel.SelectedTrack = second;
        viewModel.PlayPauseCommand.Execute(null);
        player.RaiseOpenedForTest();

        Assert.Equal(second, viewModel.PlayingTrack);
        Assert.Equal(2, viewModel.PlaybackHistory.Count);
        Assert.Equal(second, viewModel.PlaybackHistory[0].Track);
        Assert.Equal(first, viewModel.PlaybackHistory[1].Track);
    }

    [Fact]
    public void PlayTrackCommand_SelectsTrackAndStartsPlayback()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        Track second = viewModel.DisplayedTracks[1];

        viewModel.PlayTrackCommand.Execute(second);

        Assert.Equal(second, viewModel.SelectedTrack);
        Assert.Equal(second, viewModel.PlayingTrack);
        Assert.True(viewModel.IsPlaying);
        Assert.Equal(second.FilePath, player.LastOpenedFilePath);
    }

    [Fact]
    public void PlayTrackCommand_DoesNotAddHistory_BeforeMediaOpened()
    {
        var (viewModel, _, _) = CreateViewModelWithPlayer();
        Track second = viewModel.DisplayedTracks[1];

        viewModel.PlayTrackCommand.Execute(second);

        Assert.Empty(viewModel.PlaybackHistory);
    }

    [Fact]
    public void PlayTrackCommand_DoesNotRestartSameAlreadyPlayingTrack()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        Track second = viewModel.DisplayedTracks[1];

        viewModel.PlayTrackCommand.Execute(second);
        player.RaiseOpenedForTest();

        Assert.Single(viewModel.PlaybackHistory);

        viewModel.PlayTrackCommand.Execute(second);

        Assert.Equal(second, viewModel.SelectedTrack);
        Assert.Equal(second, viewModel.PlayingTrack);
        Assert.True(viewModel.IsPlaying);
        Assert.Equal(1, player.OpenCallCount);
        Assert.Equal(1, player.PlayCallCount);

        player.RaiseOpenedForTest();

        Assert.Single(viewModel.PlaybackHistory);
    }

    [Fact]
    public void PlayTrackCommand_OpenFailure_ResetsPlaybackState()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        Track second = viewModel.DisplayedTracks[1];
        player.OpenResult = OperationResult.Error("open failed");

        viewModel.PlayTrackCommand.Execute(second);

        Assert.Equal(second, viewModel.SelectedTrack);
        Assert.Null(viewModel.PlayingTrack);
        Assert.False(viewModel.IsPlaying);
        Assert.Equal(1, player.OpenCallCount);
        Assert.Equal(0, player.PlayCallCount);
        Assert.Equal(1, player.StopCallCount);
        Assert.Empty(viewModel.PlaybackHistory);
        Assert.Contains("open failed", viewModel.StatusMessage);
    }

    [Fact]
    public void PlayTrackCommand_PlayFailure_ResetsPlaybackState()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        Track second = viewModel.DisplayedTracks[1];
        player.PlayResult = OperationResult.Error("play failed");

        viewModel.PlayTrackCommand.Execute(second);

        Assert.Equal(second, viewModel.SelectedTrack);
        Assert.Null(viewModel.PlayingTrack);
        Assert.False(viewModel.IsPlaying);
        Assert.Equal(1, player.OpenCallCount);
        Assert.Equal(1, player.PlayCallCount);
        Assert.Equal(1, player.StopCallCount);
        Assert.Empty(viewModel.PlaybackHistory);
        Assert.Contains("play failed", viewModel.StatusMessage);
    }

    [Fact]
    public void PlayTrackCommand_IgnoresNonTrackParameter()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();

        viewModel.PlayTrackCommand.Execute("not a track");

        Assert.Null(viewModel.SelectedTrack);
        Assert.Null(viewModel.PlayingTrack);
        Assert.False(viewModel.IsPlaying);
        Assert.Null(player.LastOpenedFilePath);
    }

    [Fact]
    public void PlayTrackCommand_MissingFile_DoesNotSetPlayingTrack()
    {
        Track[] tracks =
        [
            new Track { Id = 1, Title = "Missing", Artist = "Band", Genre = "Рок", Duration = TimeSpan.FromSeconds(100), FilePath = "missing.mp3" }
        ];
        var player = new FakeAudioPlayerService();
        var viewModel = CreateViewModel(tracks, player, new FakeFileService("missing.mp3"));

        viewModel.PlayTrackCommand.Execute(viewModel.DisplayedTracks[0]);

        Assert.Equal(viewModel.DisplayedTracks[0], viewModel.SelectedTrack);
        Assert.Null(viewModel.PlayingTrack);
        Assert.False(viewModel.IsPlaying);
        Assert.Null(player.LastOpenedFilePath);
        Assert.Empty(viewModel.PlaybackHistory);
        Assert.Contains("Файл не найден", viewModel.StatusMessage);
    }

    [Fact]
    public void ReplayHistoryEntryCommand_SelectsEntryTrackAndStartsPlayback()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        Track second = viewModel.DisplayedTracks[1];
        var entry = new PlaybackEntry { Track = second, PlayedAt = DateTime.Now };
        viewModel.PlaybackHistory.Add(entry);

        viewModel.ReplayHistoryEntryCommand.Execute(entry);

        Assert.Equal(second, viewModel.SelectedTrack);
        Assert.Equal(second, viewModel.PlayingTrack);
        Assert.True(viewModel.IsPlaying);
        Assert.Equal(second.FilePath, player.LastOpenedFilePath);
    }

    [Fact]
    public void ReplayHistoryEntryCommand_DoesNotAddHistory_BeforeMediaOpened()
    {
        var (viewModel, _, _) = CreateViewModelWithPlayer();
        Track second = viewModel.DisplayedTracks[1];
        var entry = new PlaybackEntry { Track = second, PlayedAt = DateTime.Now };
        viewModel.PlaybackHistory.Add(entry);

        viewModel.ReplayHistoryEntryCommand.Execute(entry);

        Assert.Single(viewModel.PlaybackHistory);
        Assert.Equal(entry, viewModel.PlaybackHistory[0]);
    }

    [Fact]
    public void StopCommand_NotInvocable_WhenNothingIsPlaying()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectedTrack = viewModel.DisplayedTracks.First();

        Assert.False(viewModel.StopCommand.CanExecute(null));
    }

    [Fact]
    public void AddTrack_AddsToDisplayedAndIntroducesGenre()
    {
        var viewModel = CreateViewModel();
        int beforeCount = viewModel.DisplayedTracks.Count;

        var newTrack = new Track
        {
            Id = viewModel.GetNextTrackId(),
            Title = "Imported",
            Artist = "Tester",
            Genre = "Фонк",
            Duration = TimeSpan.FromSeconds(120),
            FilePath = "imported.mp3"
        };

        viewModel.AddTrack(newTrack);

        Assert.Contains(newTrack, viewModel.DisplayedTracks);
        Assert.Equal(beforeCount + 1, viewModel.DisplayedTracks.Count);
        Assert.Contains("Фонк", viewModel.Genres);
    }

    [Fact]
    public void MediaFailed_ResetsPlaybackStateAndDuration()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        viewModel.SelectedTrack = viewModel.DisplayedTracks.First();

        viewModel.PlayPauseCommand.Execute(null);
        player.RaiseFailedForTest("unsupported format");

        Assert.False(viewModel.IsPlaying);
        Assert.Equal("Воспроизвести", viewModel.PlayPauseText);
        Assert.Equal(TimeSpan.Zero, viewModel.CurrentPosition);
        Assert.Equal(TimeSpan.Zero, viewModel.CurrentDuration);
        Assert.Contains("unsupported format", viewModel.StatusMessage);
    }

    [Fact]
    public void MediaEnded_RepeatOff_ClearsPlayingTrack()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        Track last = viewModel.DisplayedTracks[1];
        viewModel.SelectedTrack = last;
        viewModel.PlayPauseCommand.Execute(null);
        player.Position = TimeSpan.FromSeconds(42);

        player.RaiseEndedForTest();

        Assert.Null(viewModel.PlayingTrack);
        Assert.False(viewModel.IsPlaying);
        Assert.Equal(TimeSpan.Zero, viewModel.CurrentPosition);
        Assert.Contains("завершено", viewModel.StatusMessage);
    }

    [Fact]
    public void MediaEnded_RepeatCurrent_RestartsSameTrack()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        Track first = viewModel.DisplayedTracks[0];
        viewModel.SelectedTrack = first;
        viewModel.PlayPauseCommand.Execute(null);
        player.Position = TimeSpan.FromSeconds(42);
        viewModel.RepeatMode = RepeatMode.Current;

        player.RaiseEndedForTest();

        Assert.Equal(first, viewModel.SelectedTrack);
        Assert.Equal(first, viewModel.PlayingTrack);
        Assert.True(viewModel.IsPlaying);
        Assert.Equal(TimeSpan.Zero, viewModel.CurrentPosition);
        Assert.Equal(2, player.OpenCallCount);
        Assert.Equal(first.FilePath, player.LastOpenedFilePath);
    }

    [Fact]
    public void MediaEnded_RepeatLibrary_AdvancesAndWrapsAtEnd()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        Track first = viewModel.DisplayedTracks[0];
        Track second = viewModel.DisplayedTracks[1];
        viewModel.SelectedTrack = first;
        viewModel.PlayPauseCommand.Execute(null);
        viewModel.RepeatMode = RepeatMode.Library;

        player.RaiseEndedForTest();

        Assert.Equal(second, viewModel.SelectedTrack);
        Assert.Equal(second, viewModel.PlayingTrack);
        Assert.Equal(second.FilePath, player.LastOpenedFilePath);

        player.RaiseEndedForTest();

        Assert.Equal(first, viewModel.SelectedTrack);
        Assert.Equal(first, viewModel.PlayingTrack);
        Assert.Equal(first.FilePath, player.LastOpenedFilePath);
    }

    [Fact]
    public void MediaEnded_RepeatOff_NotAtLast_AutoAdvances()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        Track first = viewModel.DisplayedTracks[0];
        Track second = viewModel.DisplayedTracks[1];
        viewModel.SelectedTrack = first;
        viewModel.PlayPauseCommand.Execute(null);

        player.RaiseEndedForTest();

        Assert.Equal(second, viewModel.SelectedTrack);
        Assert.Equal(second, viewModel.PlayingTrack);
        Assert.True(viewModel.IsPlaying);
        Assert.Equal(second.FilePath, player.LastOpenedFilePath);
    }

    [Fact]
    public void MediaEnded_AutoNextMissingFile_ClearsPlayingTrack()
    {
        var tracks = new[]
        {
            new Track { Id = 1, Title = "One", Artist = "Band", Genre = "Рок", Duration = TimeSpan.FromSeconds(100), FilePath = "one.mp3" },
            new Track { Id = 2, Title = "Missing", Artist = "Band", Genre = "Рок", Duration = TimeSpan.FromSeconds(100), FilePath = "missing-next.mp3" }
        };
        var player = new FakeAudioPlayerService();
        var viewModel = CreateViewModel(tracks, player, new FakeFileService("missing-next.mp3"));
        viewModel.SelectedTrack = viewModel.DisplayedTracks[0];
        viewModel.PlayPauseCommand.Execute(null);

        player.RaiseEndedForTest();

        Assert.Null(viewModel.PlayingTrack);
        Assert.False(viewModel.IsPlaying);
        Assert.Contains("Файл не найден", viewModel.StatusMessage);
        Assert.Contains("missing-next.mp3", viewModel.StatusMessage);
        Assert.False(viewModel.StopCommand.CanExecute(null));
        Assert.False(viewModel.SkipForwardCommand.CanExecute(null));
        Assert.False(viewModel.SkipBackwardCommand.CanExecute(null));
        Assert.False(viewModel.SeekToCommand.CanExecute(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void MediaFailed_DuringAutoNext_StopsChain()
    {
        var tracks = new[]
        {
            new Track { Id = 1, Title = "One", Artist = "Band", Genre = "Рок", Duration = TimeSpan.FromSeconds(100), FilePath = "one.mp3" },
            new Track { Id = 2, Title = "Two", Artist = "Band", Genre = "Рок", Duration = TimeSpan.FromSeconds(100), FilePath = "two.mp3" },
            new Track { Id = 3, Title = "Three", Artist = "Band", Genre = "Рок", Duration = TimeSpan.FromSeconds(100), FilePath = "three.mp3" }
        };
        var player = new FakeAudioPlayerService();
        var viewModel = CreateViewModel(tracks, player, new FakeFileService());
        viewModel.SelectedTrack = viewModel.DisplayedTracks[0];
        viewModel.PlayPauseCommand.Execute(null);

        player.RaiseEndedForTest();
        player.RaiseFailedForTest("broken file");
        player.RaiseEndedForTest();

        Assert.Null(viewModel.PlayingTrack);
        Assert.False(viewModel.IsPlaying);
        Assert.Equal(2, player.OpenCallCount);
        Assert.Equal("two.mp3", player.LastOpenedFilePath);
        Assert.Contains("broken file", viewModel.StatusMessage);
    }

    [Fact]
    public void Volume_Setter_UpdatesPlayerAndSaves()
    {
        var (viewModel, player, repo) = CreateViewModelWithPlayer();

        viewModel.Volume = 0.42;

        Assert.Equal(0.42, player.Volume);
        Assert.Contains(repo.SavedSnapshots, s => s.Volume == 0.42);
    }

    [Fact]
    public void IsMuted_Setter_UpdatesPlayerAndSaves()
    {
        var (viewModel, player, repo) = CreateViewModelWithPlayer();

        viewModel.IsMuted = true;

        Assert.True(player.IsMuted);
        Assert.Contains(repo.SavedSnapshots, s => s.IsMuted);
    }

    [Fact]
    public void RepeatMode_Setter_Saves()
    {
        var (viewModel, _, repo) = CreateViewModelWithPlayer();

        viewModel.RepeatMode = RepeatMode.Library;

        Assert.Contains(repo.SavedSnapshots, s => s.RepeatMode == RepeatMode.Library);
    }

    [Fact]
    public void SkipForward_AtEnd_ClampsToDuration()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        player.Position = TimeSpan.FromSeconds(95);

        viewModel.SkipForwardCommand.Execute(null);

        // Duration фейка = 100s, шаг = 10, значит должно стать 100, не 105.
        Assert.Equal(TimeSpan.FromSeconds(100), player.Position);
    }

    [Fact]
    public void SkipBackward_AtStart_ClampsToZero()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        player.Position = TimeSpan.FromSeconds(3);

        viewModel.SkipBackwardCommand.Execute(null);

        Assert.Equal(TimeSpan.Zero, player.Position);
    }

    [Fact]
    public void PreviousTrack_AtFirst_CommandDisabled()
    {
        var (viewModel, _, _) = CreateViewModelWithPlayer();
        viewModel.SelectedTrack = viewModel.DisplayedTracks[0];
        viewModel.PlayPauseCommand.Execute(null);

        Assert.False(viewModel.PreviousTrackCommand.CanExecute(null));
    }

    [Fact]
    public void NextTrack_AtLast_CommandDisabled()
    {
        var (viewModel, _, _) = CreateViewModelWithPlayer();
        viewModel.SelectedTrack = viewModel.DisplayedTracks[1];
        viewModel.PlayPauseCommand.Execute(null);

        Assert.False(viewModel.NextTrackCommand.CanExecute(null));
    }

    [Fact]
    public void NextTrack_AtFirst_AdvancesAndOpensSecondTrack()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        Track first = viewModel.DisplayedTracks[0];
        Track second = viewModel.DisplayedTracks[1];

        viewModel.SelectedTrack = first;
        viewModel.PlayPauseCommand.Execute(null);

        viewModel.NextTrackCommand.Execute(null);

        Assert.Equal(second, viewModel.PlayingTrack);
        Assert.Equal(second.FilePath, player.LastOpenedFilePath);
    }

    [Fact]
    public void PreviousTrack_AtSecond_GoesBackToFirst()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        Track first = viewModel.DisplayedTracks[0];
        Track second = viewModel.DisplayedTracks[1];

        viewModel.SelectedTrack = second;
        viewModel.PlayPauseCommand.Execute(null);

        viewModel.PreviousTrackCommand.Execute(null);

        Assert.Equal(first, viewModel.PlayingTrack);
        Assert.Equal(first.FilePath, player.LastOpenedFilePath);
    }

    [Fact]
    public void ToggleMuteCommand_FlipsIsMuted()
    {
        var (viewModel, _, _) = CreateViewModelWithPlayer();

        viewModel.ToggleMuteCommand.Execute(null);
        Assert.True(viewModel.IsMuted);

        viewModel.ToggleMuteCommand.Execute(null);
        Assert.False(viewModel.IsMuted);
    }

    [Fact]
    public void CycleRepeatModeCommand_CyclesOffCurrentLibraryOff()
    {
        var (viewModel, _, _) = CreateViewModelWithPlayer();

        Assert.Equal(RepeatMode.Off, viewModel.RepeatMode);

        viewModel.CycleRepeatModeCommand.Execute(null);
        Assert.Equal(RepeatMode.Current, viewModel.RepeatMode);

        viewModel.CycleRepeatModeCommand.Execute(null);
        Assert.Equal(RepeatMode.Library, viewModel.RepeatMode);

        viewModel.CycleRepeatModeCommand.Execute(null);
        Assert.Equal(RepeatMode.Off, viewModel.RepeatMode);
    }

    [Fact]
    public void SeekToCommand_WritesPositionToPlayer()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        viewModel.SelectedTrack = viewModel.DisplayedTracks[0];
        viewModel.PlayPauseCommand.Execute(null);

        viewModel.SeekToCommand.Execute(TimeSpan.FromSeconds(45));

        Assert.Equal(TimeSpan.FromSeconds(45), player.Position);
    }

    [Fact]
    public void SeekToCommand_NoPlayingTrack_DoesNothing()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        player.Position = TimeSpan.FromSeconds(7);

        viewModel.SeekToCommand.Execute(TimeSpan.FromSeconds(45));

        Assert.Equal(TimeSpan.FromSeconds(7), player.Position);
    }

    [Fact]
    public void RefreshProgress_IsSeeking_DoesNotOverwriteCurrentPosition()
    {
        var (viewModel, player, _) = CreateViewModelWithPlayer();
        typeof(MainViewModel)
            .GetProperty(nameof(MainViewModel.CurrentPosition))!
            .SetValue(viewModel, TimeSpan.FromSeconds(12));
        viewModel.IsSeeking = true;
        player.Position = TimeSpan.FromSeconds(44);

        typeof(MainViewModel)
            .GetMethod("RefreshProgress", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(viewModel, null);

        Assert.Equal(TimeSpan.FromSeconds(12), viewModel.CurrentPosition);
    }

    [Fact]
    public void DisplayedAlbums_And_DisplayedArtists_Recompute_When_Filters_Change()
    {
        var tracks = new[]
        {
            new Track { Id = 1, Title = "A", Artist = "X", Album = "Q", Genre = "Рок", FilePath = "1.mp3" },
            new Track { Id = 2, Title = "B", Artist = "X", Album = "Q", Genre = "Рок", FilePath = "2.mp3" },
            new Track { Id = 3, Title = "C", Artist = "Y", Album = "R", Genre = "Поп", FilePath = "3.mp3" },
        };
        var viewModel = CreateViewModel(tracks, new FakeAudioPlayerService(), new FakeFileService());

        // По умолчанию все три → два альбома, два исполнителя.
        Assert.Equal(2, viewModel.DisplayedAlbums.Count);
        Assert.Equal(2, viewModel.DisplayedArtists.Count);

        viewModel.SelectedGenre = "Рок";
        Assert.Single(viewModel.DisplayedAlbums);
        Assert.Equal("Q", viewModel.DisplayedAlbums[0].Title);
        Assert.Single(viewModel.DisplayedArtists);
        Assert.Equal("X", viewModel.DisplayedArtists[0].Name);
    }

    private static MainViewModel CreateViewModel()
    {
        return CreateViewModelWithPlayer().ViewModel;
    }

    private static MainViewModel CreateViewModel(
        IReadOnlyList<Track> tracks,
        FakeAudioPlayerService player,
        FakeFileService fileService)
    {
        return CreateViewModel(tracks, player, fileService, new FakePlayerSettingsRepository());
    }

    private static MainViewModel CreateViewModel(
        IReadOnlyList<Track> tracks,
        FakeAudioPlayerService player,
        FakeFileService fileService,
        FakePlayerSettingsRepository repo)
    {
        return new MainViewModel(
            new FakeTrackRepository(tracks),
            fileService,
            new FakeSaveFileDialogService(),
            player,
            new FakeListeningHistoryRepository(),
            repo,
            addTrackDialogService: null,
            confirmationService: null);
    }

    private static MainViewModel CreateViewModelWithRepo(RecordingTrackRepository repo)
    {
        return new MainViewModel(
            repo,
            new FakeFileService(),
            new FakeSaveFileDialogService(),
            new FakeAudioPlayerService(),
            new FakeListeningHistoryRepository(),
            new FakePlayerSettingsRepository(),
            addTrackDialogService: null,
            confirmationService: null);
    }

    private sealed class RecordingTrackRepository : ITrackRepository
    {
        private readonly List<Track> _tracks;
        public List<Track> UpdateCalls { get; } = new();

        public RecordingTrackRepository(IEnumerable<Track> seed) => _tracks = seed.ToList();

        public IReadOnlyList<Track> GetAll() => _tracks;
        public Track? FindById(int id) => _tracks.FirstOrDefault(t => t.Id == id);
        public Track Add(Track track) { _tracks.Add(track); return track; }
        public void Update(Track track)
        {
            int i = _tracks.FindIndex(t => t.Id == track.Id);
            if (i >= 0) _tracks[i] = track;
            UpdateCalls.Add(track);
        }
        public void Remove(int id) => _tracks.RemoveAll(t => t.Id == id);
    }

    private static MainViewModel CreateViewModelWithSearch(
        IReadOnlyList<Track> tracks,
        ISearchService searchService)
    {
        return new MainViewModel(
            new FakeTrackRepository(tracks),
            new FakeFileService(),
            new FakeSaveFileDialogService(),
            new FakeAudioPlayerService(),
            new FakeListeningHistoryRepository(),
            new FakePlayerSettingsRepository(),
            addTrackDialogService: null,
            confirmationService: null,
            searchService: searchService);
    }

    private sealed class RankedSearchService : ISearchService
    {
        // Возвращает заранее заданный список треков ровно в указанном порядке —
        // имитирует FTS5 + ORDER BY bm25. Нужен тестам на сохранение порядка.
        private readonly IReadOnlyList<Track> _ranked;

        public RankedSearchService(IReadOnlyList<Track> ranked) => _ranked = ranked;

        public IReadOnlyList<Track> Search(string query, int limit = 500) =>
            string.IsNullOrWhiteSpace(query) ? Array.Empty<Track>() : _ranked.Take(limit).ToList();
    }

    private sealed class StubSearchService : ISearchService
    {
        // Простой substring-match по Title/Artist/Album/Genre, чтобы тесты на
        // комбинацию фильтров не зависели от настоящего FTS-движка.
        private readonly IReadOnlyList<Track> _all;

        public StubSearchService(IReadOnlyList<Track> all) => _all = all;

        public IReadOnlyList<Track> Search(string query, int limit = 500)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Array.Empty<Track>();
            }
            return _all
                .Where(t =>
                    (t.Title ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (t.Artist ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (t.Album ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (t.Genre ?? "").Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .ToList();
        }
    }

    private static (MainViewModel ViewModel, FakeAudioPlayerService Player, FakePlayerSettingsRepository Repo) CreateViewModelWithPlayer()
    {
        var tracks = new[]
        {
            new Track { Id = 1, Title = "Rock Song", Artist = "Band", Genre = "Рок", Duration = TimeSpan.FromSeconds(100), FilePath = "rock.mp3" },
            new Track { Id = 2, Title = "Jazz Song", Artist = "Quartet", Genre = "Джаз", Duration = TimeSpan.FromSeconds(120), FilePath = "jazz.mp3" }
        };

        var player = new FakeAudioPlayerService();
        var repo = new FakePlayerSettingsRepository();
        var viewModel = CreateViewModel(tracks, player, new FakeFileService(), repo);

        return (viewModel, player, repo);
    }

    private sealed class FakeTrackRepository : ITrackRepository
    {
        private readonly IReadOnlyList<Track> _tracks;

        public FakeTrackRepository(IReadOnlyList<Track> tracks)
        {
            _tracks = tracks;
        }

        public IReadOnlyList<Track> GetAll() => _tracks;
        public Track? FindById(int id) => _tracks.FirstOrDefault(t => t.Id == id);
        public Track Add(Track track) => throw new NotSupportedException();
        public void Update(Track track) => throw new NotSupportedException();
        public void Remove(int id) => throw new NotSupportedException();
    }

    private sealed class FakeFileService : IFileService
    {
        private readonly HashSet<string> _missingPaths;

        public FakeFileService(params string[] missingPaths)
        {
            _missingPaths = new HashSet<string>(missingPaths, StringComparer.OrdinalIgnoreCase);
        }

        public bool Exists(string path) => !_missingPaths.Contains(path);
        public OperationResult Copy(string sourcePath, string targetPath, bool overwrite) => OperationResult.Success("saved");
        public string GetFileName(string path) => Path.GetFileName(path) ?? "track.mp3";
        public OperationResult Delete(string path) => OperationResult.Success("deleted");
    }

    private sealed class FakeSaveFileDialogService : ISaveFileDialogService
    {
        public string? PickSavePath(string suggestedFileName) => Path.Combine(Path.GetTempPath(), suggestedFileName);
    }

    private sealed class FakePlayerSettingsRepository : IPlayerSettingsRepository
    {
        public Exception? SaveError { get; set; }
        public PlayerSettings Loaded { get; set; } = PlayerSettings.Default;
        public List<PlayerSettings> SavedSnapshots { get; } = new();
        public int? SavedActiveViewIndex { get; private set; }

        public PlayerSettings Load() => Loaded;
        public void Save(PlayerSettings settings)
        {
            if (SaveError is not null) throw SaveError;
            SavedSnapshots.Add(settings);
        }
        public int? LoadActiveViewIndex() => SavedActiveViewIndex;
        public void SaveActiveView(MainViewMode view) => SavedActiveViewIndex = (int)view;
    }

    private sealed class FakeListeningHistoryRepository : IListeningHistoryRepository
    {
        private readonly List<PlaybackEntry> _entries = new();

        public IReadOnlyList<PlaybackEntry> GetRecent(int limit = 50)
            => _entries.OrderByDescending(e => e.PlayedAt).Take(limit).ToList();

        public IReadOnlyList<PlaybackEntry> GetAll()
            => _entries.OrderByDescending(e => e.PlayedAt).ToList();

        public IReadOnlyList<ListeningStats> GetTop(int limit = 50)
            => _entries
                .GroupBy(e => e.Track.Id)
                .Select(g => new ListeningStats(g.First().Track, g.Count(), g.Max(e => e.PlayedAt)))
                .OrderByDescending(s => s.PlayCount)
                .Take(limit)
                .ToList();

        public IReadOnlyList<PlaybackEntry> GetRecentUnique(int limit = 50)
            => _entries
                .GroupBy(e => e.Track.Id)
                .Select(g => g.OrderByDescending(e => e.PlayedAt).First())
                .OrderByDescending(e => e.PlayedAt)
                .Take(limit)
                .ToList();

        public IReadOnlyList<Track> GetNeverPlayed() => Array.Empty<Track>();

        public void Append(PlaybackEntry entry) => _entries.Add(entry);
    }

    private sealed class FakeAudioPlayerService : IAudioPlayerService
    {
        public event EventHandler<string>? MediaOpened;
        public event EventHandler? MediaEnded;
        public event EventHandler<string>? MediaFailed;

        public bool IsPlaying { get; private set; }
        public TimeSpan Position { get; set; }
        public TimeSpan Duration { get; } = TimeSpan.FromSeconds(100);
        public double Volume { get; set; } = 1.0;
        public bool IsMuted { get; set; }
        public string? LastOpenedFilePath { get; private set; }
        public OperationResult OpenResult { get; set; } = OperationResult.Success("opened");
        public OperationResult PlayResult { get; set; } = OperationResult.Success("playing");
        public int OpenCallCount { get; private set; }
        public int PlayCallCount { get; private set; }
        public int PauseCallCount { get; private set; }
        public int StopCallCount { get; private set; }

        public OperationResult Open(string filePath)
        {
            OpenCallCount++;
            LastOpenedFilePath = filePath;
            return OpenResult;
        }

        public OperationResult Play()
        {
            PlayCallCount++;
            if (PlayResult.IsSuccess)
            {
                IsPlaying = true;
            }
            return PlayResult;
        }

        public void Pause()
        {
            PauseCallCount++;
            IsPlaying = false;
        }

        public void Stop()
        {
            StopCallCount++;
            IsPlaying = false;
            Position = TimeSpan.Zero;
        }

        public void Dispose()
        {
        }

        public void RaiseEndedForTest() => MediaEnded?.Invoke(this, EventArgs.Empty);
        public void RaiseOpenedForTest() => RaiseOpenedForTest(LastOpenedFilePath ?? string.Empty);
        public void RaiseOpenedForTest(string filePath) => MediaOpened?.Invoke(this, filePath);
        public void RaiseFailedForTest(string message) => MediaFailed?.Invoke(this, message);
    }
}
