using System.IO;
using MusicBakh.Infrastructure.Migration;
using MusicBakh.Infrastructure.Persistence.Entities;
using MusicBakh.Infrastructure.Persistence.Repositories;
using MusicLibrary.Tests.TestSupport;
using Xunit;

namespace MusicLibrary.Tests.Migration;

public sealed class JsonToSqliteMigrationServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public JsonToSqliteMigrationServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "musicbakh-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            try
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
            catch (IOException)
            {
                // Файл может быть занят, не критично — это временный каталог тестов.
            }
        }
    }

    [Fact]
    public void Run_Does_Nothing_When_Legacy_File_Missing()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);
        var migration = new JsonToSqliteMigrationService(_tempRoot, factory.CreateContext);

        var result = migration.Run();

        Assert.False(result.PerformedMigration);
        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void Run_Migrates_Tracks_And_Renames_Legacy_File()
    {
        var legacyPath = Path.Combine(_tempRoot, "userTracks.json");
        File.WriteAllText(legacyPath, """
            [
              {
                "id": 1,
                "title": "Test Song",
                "artist": "Test Artist",
                "genre": "Rock",
                "durationSeconds": 180,
                "filePath": "C:/Music/test.mp3",
                "coverPath": "C:/Covers/test.png",
                "addedAt": "2026-01-01T12:00:00Z"
              }
            ]
            """);

        using var factory = new InMemorySqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);
        var migration = new JsonToSqliteMigrationService(_tempRoot, factory.CreateContext);

        var result = migration.Run();

        Assert.True(result.PerformedMigration);
        Assert.Equal(1, result.MigratedTracks);
        Assert.False(File.Exists(legacyPath), "userTracks.json должен быть переименован после миграции");
        Assert.True(File.Exists(result.BackupPath!), "должен существовать backup-файл");

        var all = repo.GetAll();
        Assert.Single(all);
        Assert.Equal("Test Song", all[0].Title);
        Assert.False(all[0].IsBuiltIn, "Мигрированные треки — пользовательские, IsBuiltIn=false.");
    }

    [Fact]
    public void Run_Is_NoOp_If_Already_Migrated()
    {
        var legacyPath = Path.Combine(_tempRoot, "userTracks.json");
        File.WriteAllText(legacyPath, "[]");

        using var factory = new InMemorySqliteDbContextFactory();
        var migration = new JsonToSqliteMigrationService(_tempRoot, factory.CreateContext);

        migration.Run();
        var second = migration.Run();

        Assert.False(second.PerformedMigration);
    }

    [Fact]
    public void Run_Handles_Corrupted_Json_Gracefully()
    {
        var legacyPath = Path.Combine(_tempRoot, "userTracks.json");
        File.WriteAllText(legacyPath, "{ not valid json");

        using var factory = new InMemorySqliteDbContextFactory();
        var migration = new JsonToSqliteMigrationService(_tempRoot, factory.CreateContext);

        Assert.Throws<System.Text.Json.JsonException>(() => migration.Run());
        Assert.Equal("{ not valid json", File.ReadAllText(legacyPath));
        Assert.Empty(Directory.GetFiles(_tempRoot, "*.backup-*"));
    }

    [Fact]
    public void Run_DeduplicatesPathsInsideLegacyFile()
    {
        File.WriteAllText(Path.Combine(_tempRoot, "userTracks.json"), """
            [{"title":"One","filePath":"C:/Music/song.mp3"},
             {"title":"Two","filePath":"c:/music/SONG.mp3"}]
            """);
        using var factory = new MigratedSqliteDbContextFactory();
        var result = new JsonToSqliteMigrationService(_tempRoot, factory.CreateContext).Run();
        Assert.Equal(1, result.MigratedTracks);
        Assert.Single(new SqliteTrackRepository(factory.CreateContext).GetAll());
    }

    [Fact]
    public void Run_Is_Idempotent_When_File_Already_Present_In_Db()
    {
        // Воспроизводим сценарий "краш между SaveChanges и File.Move":
        // в БД уже есть трек с FilePath из JSON, а сам JSON ещё не переименован.
        using var factory = new InMemorySqliteDbContextFactory();
        using (var ctx = factory.CreateContext())
        {
            ctx.Tracks.Add(new TrackEntity
            {
                Title = "Pre-migrated",
                Artist = "X",
                FilePath = "C:/Music/test.mp3",
                IsBuiltIn = false
            });
            ctx.SaveChanges();
        }

        var legacyPath = Path.Combine(_tempRoot, "userTracks.json");
        File.WriteAllText(legacyPath, """
            [
              {
                "id": 1,
                "title": "Pre-migrated",
                "artist": "X",
                "genre": "Rock",
                "durationSeconds": 180,
                "filePath": "C:/Music/test.mp3",
                "coverPath": "",
                "addedAt": "2026-01-01T12:00:00Z"
              },
              {
                "id": 2,
                "title": "Fresh",
                "artist": "Y",
                "genre": "Pop",
                "durationSeconds": 120,
                "filePath": "C:/Music/fresh.mp3",
                "coverPath": "",
                "addedAt": "2026-01-01T12:00:00Z"
              }
            ]
            """);

        var migration = new JsonToSqliteMigrationService(_tempRoot, factory.CreateContext);
        var result = migration.Run();

        Assert.True(result.PerformedMigration);
        Assert.Equal(1, result.MigratedTracks); // только новый трек, дубль пропущен по FilePath
        var repo = new SqliteTrackRepository(factory.CreateContext);
        var all = repo.GetAll();
        Assert.Equal(2, all.Count);
        Assert.Single(all, t => t.Title == "Pre-migrated");
        Assert.Single(all, t => t.Title == "Fresh");
    }
}
