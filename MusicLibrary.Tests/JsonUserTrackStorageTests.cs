using MusicLibrary.Models;
using MusicLibrary.Services.Storage;
using System.IO;

namespace MusicLibrary.Tests;

public sealed class JsonUserTrackStorageTests : IDisposable
{
    private readonly string _root;

    public JsonUserTrackStorageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "MusicLibraryTests_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Load_ReturnsEmpty_WhenFileMissing()
    {
        var storage = new JsonUserTrackStorage(_root);

        Assert.Empty(storage.Load());
        Assert.True(Directory.Exists(storage.MusicDirectory));
        Assert.True(Directory.Exists(storage.CoversDirectory));
    }

    [Fact]
    public void SaveLoad_RoundTrip_PreservesAllFields()
    {
        var storage = new JsonUserTrackStorage(_root);
        DateTime now = new(2026, 5, 9, 12, 0, 0, DateTimeKind.Utc);

        var original = new UserTrack
        {
            Id = 17,
            Title = "Test Title",
            Artist = "Test Artist",
            Genre = "Электроника",
            DurationSeconds = 215.5,
            FilePath = @"C:\Music\track.mp3",
            CoverPath = @"C:\Covers\track.png",
            AddedAt = now
        };

        storage.Save(new[] { original });

        IReadOnlyList<UserTrack> loaded = storage.Load();
        Assert.Single(loaded);
        UserTrack restored = loaded[0];
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Title, restored.Title);
        Assert.Equal(original.Artist, restored.Artist);
        Assert.Equal(original.Genre, restored.Genre);
        Assert.Equal(original.DurationSeconds, restored.DurationSeconds);
        Assert.Equal(original.FilePath, restored.FilePath);
        Assert.Equal(original.CoverPath, restored.CoverPath);
    }

    [Fact]
    public void Append_AddsToExistingFile()
    {
        var storage = new JsonUserTrackStorage(_root);
        storage.Save(new[]
        {
            new UserTrack { Id = 1, Title = "First", Artist = "A", Genre = "Рок", DurationSeconds = 100, FilePath = "f1", CoverPath = "c1" }
        });

        storage.Append(new UserTrack { Id = 2, Title = "Second", Artist = "B", Genre = "Поп", DurationSeconds = 200, FilePath = "f2", CoverPath = "c2" });

        IReadOnlyList<UserTrack> all = storage.Load();
        Assert.Equal(2, all.Count);
        Assert.Equal("First", all[0].Title);
        Assert.Equal("Second", all[1].Title);
    }

    [Fact]
    public void Delete_RemovesEntryAndDeletesFiles()
    {
        var storage = new JsonUserTrackStorage(_root);
        string audioPath = Path.Combine(storage.MusicDirectory, "test.mp3");
        string coverPath = Path.Combine(storage.CoversDirectory, "test.png");
        File.WriteAllBytes(audioPath, new byte[] { 1, 2, 3 });
        File.WriteAllBytes(coverPath, new byte[] { 4, 5, 6 });

        storage.Save(new[]
        {
            new UserTrack { Id = 17, Title = "T", Artist = "A", Genre = "Рок", DurationSeconds = 100, FilePath = audioPath, CoverPath = coverPath }
        });

        storage.Delete(17);

        Assert.Empty(storage.Load());
        Assert.False(File.Exists(audioPath));
        Assert.False(File.Exists(coverPath));
    }

    [Fact]
    public void Delete_IsNoOp_WhenIdNotFound()
    {
        var storage = new JsonUserTrackStorage(_root);
        storage.Save(new[]
        {
            new UserTrack { Id = 1, Title = "T", DurationSeconds = 100, FilePath = "f", CoverPath = "c" }
        });

        storage.Delete(999);

        Assert.Single(storage.Load());
    }

    [Fact]
    public void Load_ReturnsEmpty_OnCorruptedFile()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "userTracks.json"), "{ this is not json");

        var storage = new JsonUserTrackStorage(_root);

        Assert.Empty(storage.Load());
    }
}
