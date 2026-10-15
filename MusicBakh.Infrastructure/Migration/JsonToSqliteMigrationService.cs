#pragma warning disable CS0618 // Сервис мигрирует данные из legacy-хранилища — единственное санкционированное использование Obsolete-типов.

using MusicBakh.Infrastructure.Migration.Legacy;
using MusicBakh.Infrastructure.Persistence;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Migration;

/// <summary>
/// Результат однократного запуска миграции. PerformedMigration=true означает, что
/// файл userTracks.json существовал в начале запуска (даже если записей в нём не оказалось).
/// </summary>
public sealed record MigrationResult(bool PerformedMigration, int MigratedTracks, string? BackupPath);

/// <summary>
/// Одноразовая миграция userTracks.json → SQLite при первом запуске после апгрейда с 1.0.0.
/// Атомарность гарантируется одним DbContext с явной транзакцией: либо все записи
/// попадают в БД и файл переименовывается в userTracks.json.backup-&lt;timestamp&gt;,
/// либо ничего не происходит и легаси-файл остаётся на месте для повторной попытки.
/// Идемпотентность дополнительно подкреплена дедупликацией по FilePath: даже если
/// миграция запустилась повторно (например, после краша между SaveChanges и File.Move),
/// уже существующие треки не дублируются.
/// </summary>
public sealed class JsonToSqliteMigrationService
{
    private readonly string _rootDirectory;
    private readonly Func<LibraryDbContext> _contextFactory;

    public JsonToSqliteMigrationService(string rootDirectory, Func<LibraryDbContext> contextFactory)
    {
        _rootDirectory = rootDirectory;
        _contextFactory = contextFactory;
    }

    public MigrationResult Run()
    {
        var legacyPath = Path.Combine(_rootDirectory, "userTracks.json");
        if (!File.Exists(legacyPath))
        {
            return new MigrationResult(PerformedMigration: false, MigratedTracks: 0, BackupPath: null);
        }

        // Миграция не должна выдавать повреждённый JSON за пустую библиотеку.
        // При ошибке исходник остаётся на месте для восстановления и повторной попытки.
        using var stream = File.OpenRead(legacyPath);
        var tracks = System.Text.Json.JsonSerializer.Deserialize<List<UserTrack>>(stream,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase })
            ?? throw new System.Text.Json.JsonException("Библиотека должна содержать массив треков.");
        stream.Close();

        int migrated = 0;
        using (var ctx = _contextFactory())
        {
            using var transaction = ctx.Database.BeginTransaction();

            // Дедупликация по FilePath: если миграция уже частично прошла и оборвалась,
            // повторный запуск не задвоит записи. FilePath уникален для пользовательских
            // треков (каждый импортированный файл лежит в Music\<guid>.{mp3,wav}).
            var existingPaths = ctx.Tracks
                .Where(t => !t.IsBuiltIn)
                .Select(t => t.FilePath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var t in tracks)
            {
                if (t is null || string.IsNullOrWhiteSpace(t.FilePath) || !double.IsFinite(t.DurationSeconds)
                    || t.DurationSeconds < 0)
                {
                    throw new System.Text.Json.JsonException("В библиотеке найдена некорректная запись трека.");
                }
                if (!existingPaths.Add(t.FilePath))
                {
                    continue;
                }

                ctx.Tracks.Add(new TrackEntity
                {
                    Title = t.Title,
                    Artist = t.Artist,
                    // Legacy JSON не содержит Album. Default в TrackEntity и колонке
                    // (NOT NULL DEFAULT '') и так дают пустую строку, но явная установка
                    // защищает от регрессий, если кто-то сделает поле nullable.
                    Album = string.Empty,
                    Genre = t.Genre,
                    DurationTicks = TimeSpan.FromSeconds(t.DurationSeconds).Ticks,
                    FilePath = t.FilePath,
                    CoverPath = t.CoverPath,
                    AddedAtUtc = t.AddedAt.Kind == DateTimeKind.Utc
                        ? t.AddedAt
                        : t.AddedAt.ToUniversalTime(),
                    IsBuiltIn = false
                });
                migrated++;
            }

            ctx.SaveChanges();
            transaction.Commit();
        }

        var backupPath = Path.Combine(
            _rootDirectory,
            $"userTracks.json.backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        File.Move(legacyPath, backupPath);

        return new MigrationResult(PerformedMigration: true, MigratedTracks: migrated, BackupPath: backupPath);
    }
}

#pragma warning restore CS0618
