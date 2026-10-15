using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MusicBakh.Infrastructure.Persistence;

public static class DatabaseMaintenance
{
    /// <summary>Перед обновлением существующей схемы сохраняет согласованную SQLite-копию, включая WAL.</summary>
    public static string? Migrate(LibraryDbContextOptions options)
    {
        options.EnsureDirectory();
        bool existed = File.Exists(options.DatabasePath);
        using var context = new LibraryDbContext(new DbContextOptionsBuilder<LibraryDbContext>()
            .UseSqlite(options.ConnectionString).Options);
        if (!context.Database.GetPendingMigrations().Any()) return null;

        string? backup = null;
        if (existed)
        {
            backup = $"{options.DatabasePath}.backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
            using var source = new SqliteConnection(options.ConnectionString);
            using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = backup, Pooling = false }.ToString());
            source.Open();
            destination.Open();
            source.BackupDatabase(destination);
        }
        context.Database.Migrate();
        return backup;
    }
}
