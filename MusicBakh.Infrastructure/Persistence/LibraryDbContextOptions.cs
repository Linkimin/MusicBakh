using System.IO;

namespace MusicBakh.Infrastructure.Persistence;

/// <summary>
/// Параметры подключения к локальной SQLite-базе.
/// Путь по умолчанию — %LocalAppData%\MusicLibrary\library.db.
/// </summary>
public sealed record LibraryDbContextOptions(string DatabasePath)
{
    public static LibraryDbContextOptions Default { get; } = new(
        DatabasePath: Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MusicLibrary",
            "library.db"));

    public string ConnectionString => new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
    {
        DataSource = DatabasePath
    }.ToString();

    /// <summary>
    /// Создаёт каталог, в котором будет лежать SQLite-файл. SQLite сам файл создаст,
    /// но папку — нет: на чистой машине без этого Database.Migrate() упадёт с
    /// "directory not found", если %LocalAppData%\MusicLibrary ещё не существует.
    /// </summary>
    public void EnsureDirectory()
    {
        var directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
