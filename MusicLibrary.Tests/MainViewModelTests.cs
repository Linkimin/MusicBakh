using MusicLibrary.Models;
using MusicLibrary.Services.Files;
using MusicLibrary.Services.Playback;
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
    public void PlayPauseCommand_AddsHistory_WhenPlaybackStarts()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectedTrack = viewModel.DisplayedTracks.First();

        viewModel.PlayPauseCommand.Execute(null);

        Assert.Single(viewModel.PlaybackHistory);
        Assert.Equal(viewModel.SelectedTrack, viewModel.PlaybackHistory[0].Track);
    }

    [Fact]
    public void PlayPauseCommand_DoesNotDuplicateHistory_WhenResumingAfterPause()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectedTrack = viewModel.DisplayedTracks.First();

        viewModel.PlayPauseCommand.Execute(null);
        viewModel.PlayPauseCommand.Execute(null);
        viewModel.PlayPauseCommand.Execute(null);

        Assert.Single(viewModel.PlaybackHistory);
    }

    [Fact]
    public void MediaFailed_ResetsPlaybackStateAndDuration()
    {
        var (viewModel, player) = CreateViewModelWithPlayer();
        viewModel.SelectedTrack = viewModel.DisplayedTracks.First();

        viewModel.PlayPauseCommand.Execute(null);
        player.RaiseFailedForTest("unsupported format");

        Assert.False(viewModel.IsPlaying);
        Assert.Equal("Воспроизвести", viewModel.PlayPauseText);
        Assert.Equal(TimeSpan.Zero, viewModel.CurrentPosition);
        Assert.Equal(TimeSpan.Zero, viewModel.CurrentDuration);
        Assert.Contains("unsupported format", viewModel.StatusMessage);
    }

    private static MainViewModel CreateViewModel()
    {
        return CreateViewModelWithPlayer().ViewModel;
    }

    private static (MainViewModel ViewModel, FakeAudioPlayerService Player) CreateViewModelWithPlayer()
    {
        var tracks = new[]
        {
            new Track { Id = 1, Title = "Rock Song", Artist = "Band", Genre = "Рок", Duration = TimeSpan.FromSeconds(100), FilePath = "rock.mp3" },
            new Track { Id = 2, Title = "Jazz Song", Artist = "Quartet", Genre = "Джаз", Duration = TimeSpan.FromSeconds(120), FilePath = "jazz.mp3" }
        };

        var player = new FakeAudioPlayerService();
        var viewModel = new MainViewModel(
            new FakeTrackRepository(tracks),
            new FakeFileService(),
            new FakeSaveFileDialogService(),
            player);

        return (viewModel, player);
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
        public bool Exists(string path) => true;
        public OperationResult Copy(string sourcePath, string targetPath, bool overwrite) => OperationResult.Success("saved");
        public string GetFileName(string path) => Path.GetFileName(path) ?? "track.mp3";
    }

    private sealed class FakeSaveFileDialogService : ISaveFileDialogService
    {
        public string? PickSavePath(string suggestedFileName) => Path.Combine(Path.GetTempPath(), suggestedFileName);
    }

    private sealed class FakeAudioPlayerService : IAudioPlayerService
    {
        public event EventHandler? MediaOpened;
        public event EventHandler? MediaEnded;
        public event EventHandler<string>? MediaFailed;

        public bool IsPlaying { get; private set; }
        public TimeSpan Position { get; set; }
        public TimeSpan Duration { get; } = TimeSpan.FromSeconds(100);

        public OperationResult Open(string filePath)
        {
            MediaOpened?.Invoke(this, EventArgs.Empty);
            return OperationResult.Success("opened");
        }

        public OperationResult Play()
        {
            IsPlaying = true;
            return OperationResult.Success("playing");
        }

        public void Pause() => IsPlaying = false;

        public void Stop()
        {
            IsPlaying = false;
            Position = TimeSpan.Zero;
        }

        public void Dispose()
        {
        }

        public void RaiseEndedForTest() => MediaEnded?.Invoke(this, EventArgs.Empty);
        public void RaiseFailedForTest(string message) => MediaFailed?.Invoke(this, message);
    }
}
