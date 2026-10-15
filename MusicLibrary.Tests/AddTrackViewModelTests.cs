using MusicBakh.Application.Abstractions;
using MusicBakh.Application.Contracts;
using MusicLibrary.ViewModels;

namespace MusicLibrary.Tests;

public sealed class AddTrackViewModelTests
{
    [Fact]
    public void SaveFailure_KeepsCandidateAndAllowsRetryWithoutImportingAgain()
    {
        var importer = new Importer();
        int attempts = 0;
        bool closed = false;
        using var vm = new AddTrackViewModel(new FileDialog(), importer, candidate =>
        {
            if (++attempts == 1) throw new System.IO.IOException("Database unavailable");
            return new MusicBakh.Core.Domain.Track { Id = 42, FilePath = candidate.AudioFilePath };
        }) { LocalFilePath = "song.mp3" };
        vm.CloseRequested += (_, _) => closed = true;
        vm.StartImportCommand.Execute(null);
        vm.ConfirmCommand.Execute(null);
        Assert.False(closed);
        Assert.Null(vm.Result);
        Assert.True(vm.HasCandidate);
        Assert.Empty(importer.Discarded);
        Assert.Contains("Database unavailable", vm.StatusMessage);
        vm.ConfirmCommand.Execute(null);
        Assert.True(closed);
        Assert.Equal(42, vm.SavedTrack?.Id);
        Assert.Equal(2, attempts);
        Assert.Empty(importer.Discarded);
    }

    [Fact]
    public void CancelAfterSaveFailure_DiscardsOwnedCandidate()
    {
        var importer = new Importer();
        using var vm = new AddTrackViewModel(new FileDialog(), importer,
            _ => throw new System.IO.IOException("Database unavailable")) { LocalFilePath = "song.mp3" };
        vm.StartImportCommand.Execute(null);
        vm.ConfirmCommand.Execute(null);
        vm.CancelCommand.Execute(null);
        Assert.Single(importer.Discarded);
        Assert.Null(vm.SavedTrack);
    }

    [Fact]
    public void Cancel_CancelsInFlightImport()
    {
        var importer = new Importer { Wait = true };
        var vm = new AddTrackViewModel(new FileDialog(), importer) { LocalFilePath = "song.mp3" };
        vm.StartImportCommand.Execute(null);
        vm.CancelCommand.Execute(null);
        Assert.True(importer.Token.IsCancellationRequested);
    }

    [Fact]
    public void Cancel_DiscardsUnconfirmedCandidate()
    {
        var importer = new Importer();
        var vm = new AddTrackViewModel(new FileDialog(), importer) { LocalFilePath = "song.mp3" };
        vm.StartImportCommand.Execute(null);
        Assert.True(vm.HasCandidate);
        vm.CancelCommand.Execute(null);
        Assert.Single(importer.Discarded);
        Assert.Null(vm.Result);
    }

    [Fact]
    public void RetryFailure_DoesNotLeavePreviousCandidateConfirmable()
    {
        var importer = new Importer();
        var vm = new AddTrackViewModel(new FileDialog(), importer) { LocalFilePath = "song.mp3" };
        vm.StartImportCommand.Execute(null);
        importer.Fail = true;
        vm.StartImportCommand.Execute(null);
        Assert.False(vm.HasCandidate);
        Assert.False(vm.ConfirmCommand.CanExecute(null));
        Assert.Single(importer.Discarded);
    }

    private sealed class FileDialog : IOpenFileDialogService
    {
        public string? PickAudioFile() => null;
    }

    private sealed class Importer : ITrackImporter
    {
        public bool Wait { get; init; }
        public bool Fail { get; set; }
        public CancellationToken Token { get; private set; }
        public List<TrackImportCandidate> Discarded { get; } = new();
        public void Discard(TrackImportCandidate candidate) => Discarded.Add(candidate);
        public async Task<ImportResult> ImportAsync(ImportRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            if (Wait) await Task.Delay(Timeout.Infinite, cancellationToken);
            return Fail ? ImportResult.Error("Failed") : ImportResult.Success(new TrackImportCandidate { Title = "Song", AudioFilePath = "song.mp3", CoverFilePath = "missing.png" });
        }
    }
}
