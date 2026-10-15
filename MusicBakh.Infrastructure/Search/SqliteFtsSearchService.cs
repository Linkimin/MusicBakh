using Microsoft.EntityFrameworkCore;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Search;

/// <summary>
/// Реализация ISearchService поверх FTS5 (виртуальная таблица TracksFts, см. миграцию
/// AddTracksFts). Получает фабрику контекста, чтобы каждый запрос работал на свежем
/// коротком контексте — единообразно с остальными SQLite-репозиториями.
/// </summary>
public sealed class SqliteFtsSearchService : ISearchService
{
    private readonly Func<LibraryDbContext> _contextFactory;

    public SqliteFtsSearchService(Func<LibraryDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public IReadOnlyList<Track> Search(string query, int limit = 500)
    {
        string? ftsQuery = FtsQueryBuilder.Build(query);
        if (ftsQuery is null || limit <= 0)
        {
            return Array.Empty<Track>();
        }

        using var ctx = _contextFactory();

        // FromSqlInterpolated параметризует ftsQuery и limit, защищая от SQL-инъекции
        // (хотя FtsQueryBuilder уже отрезает FTS-метасимволы — это второй контур).
        // Сортировка по bm25 — стандартная функция ранжирования FTS5; чем меньше
        // значение, тем выше релевантность.
        var entities = ctx.Tracks
            .FromSqlInterpolated($@"
SELECT t.*
FROM Tracks AS t
JOIN TracksFts AS f ON f.rowid = t.Id
WHERE TracksFts MATCH {ftsQuery}
ORDER BY bm25(TracksFts)
LIMIT {limit}")
            .AsNoTracking()
            .ToList();

        var result = new List<Track>(entities.Count);
        foreach (TrackEntity e in entities)
        {
            result.Add(TrackMapper.ToDomain(e));
        }
        return result;
    }
}
