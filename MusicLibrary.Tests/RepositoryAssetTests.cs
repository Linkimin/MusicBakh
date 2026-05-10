using MusicLibrary.Services.Tracks;
using System.IO;

namespace MusicLibrary.Tests;

public sealed class RepositoryAssetTests
{
    [Fact]
    public void Repository_ContainsCuratedReferenceTracks()
    {
        // В релизной сборке оставлены 3 эталонных трека (см. docs/scope-deviations.md, п. 8).
        var repository = new InMemoryTrackRepository();

        var tracks = repository.GetTracks();

        Assert.Equal(3, tracks.Count);
        Assert.All(tracks, track => Assert.EndsWith(".mp3", track.FilePath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Repository_TrackFilesExist()
    {
        var repository = new InMemoryTrackRepository();

        var missingFiles = repository.GetTracks()
            .Where(track => !File.Exists(track.FilePath))
            .Select(track => track.FilePath)
            .ToList();

        Assert.Empty(missingFiles);
    }

    [Fact]
    public void Repository_CoverFilesExist()
    {
        var repository = new InMemoryTrackRepository();

        var missingCovers = repository.GetTracks()
            .Where(track => !File.Exists(track.CoverPath))
            .Select(track => track.CoverPath)
            .ToList();

        Assert.Empty(missingCovers);
    }

    [Fact]
    public void Repository_DurationsAreFactualAndNonZero()
    {
        var repository = new InMemoryTrackRepository();

        var tracks = repository.GetTracks();

        Assert.All(tracks, track => Assert.True(track.Duration.TotalSeconds > 60, $"{track.Title} has suspicious duration."));
        Assert.Contains(tracks, track => track.Title == "Я свободен" && track.Duration == TimeSpan.FromSeconds(204));
        Assert.Contains(tracks, track => track.Title == "Hayloft II" && track.Duration == TimeSpan.FromSeconds(215));
        Assert.Contains(tracks, track => track.Title == "VORACITY" && track.Duration == TimeSpan.FromSeconds(230));
    }
}
