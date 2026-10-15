using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MusicBakh.Infrastructure.Persistence;

namespace MusicLibrary.Tests.Persistence;

public sealed class DatabaseMaintenanceTests
{
    [Fact]
    public void Upgrade_KeepsReadableBackupOfOldSchemaAndData()
    {
        string root = Path.Combine(Path.GetTempPath(), "musicbakh-backup-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var options = new LibraryDbContextOptions(Path.Combine(root, "library;test.db"));
            using (var ctx = new LibraryDbContext(new DbContextOptionsBuilder<LibraryDbContext>().UseSqlite(options.ConnectionString).Options))
            {
                ctx.GetService<IMigrator>().Migrate("20260519035836_AddLibrarySchema");
                ctx.Database.ExecuteSqlRaw("INSERT INTO Tracks (Title, Artist, Genre, DurationTicks, FilePath, CoverPath, AddedAtUtc, IsBuiltIn) VALUES ('Keep me', 'A', '', 0, 'track.mp3', '', '2026-01-01', 0)");
            }
            string? backup = DatabaseMaintenance.Migrate(options);
            Assert.NotNull(backup);
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backup, Mode = SqliteOpenMode.ReadOnly }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT Title FROM Tracks";
                Assert.Equal("Keep me", command.ExecuteScalar());
                command.CommandText = "SELECT count(*) FROM __EFMigrationsHistory";
                Assert.Equal(1L, command.ExecuteScalar());
            }
            Assert.Null(DatabaseMaintenance.Migrate(options));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }
}
