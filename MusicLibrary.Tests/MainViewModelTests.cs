using MusicLibrary.Models;
using MusicLibrary.Services.Files;
using MusicLibrary.Services.Playback;
using MusicLibrary.Services.Storage;
using MusicLibrary.Services.Tracks;
using MusicLibrary.ViewModels;
using System.IO;

namespace MusicLibrary.Tests;

public sealed class MainViewModelTests
{
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
        var (viewModel, player, storage) = CreateViewModelWithPlayer();

        viewModel.Volume = 0.42;

        Assert.Equal(0.42, player.Volume);
        Assert.Contains(storage.SavedSnapshots, s => s.Volume == 0.42);
    }

    [Fact]
    public void IsMuted_Setter_UpdatesPlayerAndSaves()
    {
        var (viewModel, player, storage) = CreateViewModelWithPlayer();

        viewModel.IsMuted = true;

        Assert.True(player.IsMuted);
        Assert.Contains(storage.SavedSnapshots, s => s.IsMuted);
    }

    [Fact]
    public void RepeatMode_Setter_Saves()
    {
        var (viewModel, _, storage) = CreateViewModelWithPlayer();

        viewModel.RepeatMode = RepeatMode.Library;

        Assert.Contains(storage.SavedSnapshots, s => s.RepeatMode == RepeatMode.Library);
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

    private static MainViewModel CreateViewModel()
    {
        return CreateViewModelWithPlayer().ViewModel;
    }

    private static MainViewModel CreateViewModel(
        IReadOnlyList<Track> tracks,
        FakeAudioPlayerService player,
        FakeFileService fileService)
    {
        return CreateViewModel(tracks, player, fileService, new FakePlayerSettingsStorage());
    }

    private static MainViewModel CreateViewModel(
        IReadOnlyList<Track> tracks,
        FakeAudioPlayerService player,
        FakeFileService fileService,
        FakePlayerSettingsStorage storage)
    {
        return new MainViewModel(
            new FakeTrackRepository(tracks),
            fileService,
            new FakeSaveFileDialogService(),
            player,
            addTrackDialogService: null,
            userTrackStorage: null,
            confirmationService: null,
            playerSettingsStorage: storage);
    }

    private static (MainViewModel ViewModel, FakeAudioPlayerService Player, FakePlayerSettingsStorage Storage) CreateViewModelWithPlayer()
    {
        var tracks = new[]
        {
            new Track { Id = 1, Title = "Rock Song", Artist = "Band", Genre = "Рок", Duration = TimeSpan.FromSeconds(100), FilePath = "rock.mp3" },
            new Track { Id = 2, Title = "Jazz Song", Artist = "Quartet", Genre = "Джаз", Duration = TimeSpan.FromSeconds(120), FilePath = "jazz.mp3" }
        };

        var player = new FakeAudioPlayerService();
        var storage = new FakePlayerSettingsStorage();
        var viewModel = CreateViewModel(tracks, player, new FakeFileService(), storage);

        return (viewModel, player, storage);
    }

    private sealed class FakeTrackRepository : ITrackRepository
    {
        private readonly IReadOnlyList<Track> _tracks;

        public FakeTrackRepository(IReadOnlyList<Track> tracks)
        {
            _tracks = tracks;
        }

        public IReadOnlyList<Track> GetTracks() => _tracks;
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
    }

    private sealed class FakeSaveFileDialogService : ISaveFileDialogService
    {
        public string? PickSavePath(string suggestedFileName) => Path.Combine(Path.GetTempPath(), suggestedFileName);
    }

    private sealed class FakePlayerSettingsStorage : IPlayerSettingsStorage
    {
        public PlayerSettings Loaded { get; set; } = PlayerSettings.Default;
        public List<PlayerSettings> SavedSnapshots { get; } = new();

        public PlayerSettings Load() => Loaded;
        public void Save(PlayerSettings settings) => SavedSnapshots.Add(settings);
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
