using Microsoft.EntityFrameworkCore;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence.Repositories;

/// <summary>
/// История прослушиваний поверх LibraryDbContext. Использует IClock, чтобы тесты
/// могли подменять текущее время; в production это SystemClock.
/// </summary>
public sealed class SqliteListeningHistoryRepository : IListeningHistoryRepository
{
    private readonly Func<LibraryDbContext> _contextFactory;
    private readonly IClock _clock;

    public SqliteListeningHistoryRepository(Func<LibraryDbContext> contextFactory, IClock clock)
    {
        _contextFactory = contextFactory;
        _clock = clock;
    }

    public IReadOnlyList<PlaybackEntry> GetRecent(int limit = 50)
    {
        using var ctx = _contextFactory();
        return ctx.ListeningHistory
            .Include(h => h.Track)
            .OrderByDescending(h => h.PlayedAtUtc)
            .Take(limit)
            .Select(h => ToEntry(h))
            .ToList();
    }

    public IReadOnlyList<PlaybackEntry> GetAll()
    {
        using var ctx = _contextFactory();
        return ctx.ListeningHistory
            .Include(h => h.Track)
            .OrderByDescending(h => h.PlayedAtUtc)
            .Select(h => ToEntry(h))
            .ToList();
    }

    public IReadOnlyList<ListeningStats> GetTop(int limit = 50)
    {
        using var ctx = _contextFactory();
        // GroupBy по TrackId, считаем сколько раз сыграли и когда в последний раз;
        // потом join с Tracks, чтобы получить актуальные доменные поля.
        // EF Core 10 транслирует это в один SQL-запрос с GROUP BY.
        var aggregates = ctx.ListeningHistory
            .GroupBy(h => h.TrackId)
            .Select(g => new
            {
                TrackId = g.Key,
                PlayCount = g.Count(),
                LastPlayedUtc = g.Max(h => h.PlayedAtUtc)
            })
            .OrderByDescending(a => a.PlayCount)
            .ThenByDescending(a => a.LastPlayedUtc)
            .Take(limit)
            .ToList();

        if (aggregates.Count == 0)
        {
            return Array.Empty<ListeningStats>();
        }

        var trackIds = aggregates.Select(a => a.TrackId).ToList();
        var tracks = ctx.Tracks
            .Where(t => trackIds.Contains(t.Id))
            .ToDictionary(t => t.Id, TrackMapper.ToDomain);

        var result = new List<ListeningStats>(aggregates.Count);
        foreach (var agg in aggregates)
        {
            if (tracks.TryGetValue(agg.TrackId, out var track))
            {
                result.Add(new ListeningStats(track, agg.PlayCount, agg.LastPlayedUtc));
            }
            // Если в Tracks трека нет (например, удалили после прослушивания, а CASCADE
            // не отработал из-за soft-delete в будущей итерации) — пропускаем тихо.
            // На текущей схеме CASCADE гарантирует, что такого не случается.
        }
        return result;
    }

    public IReadOnlyList<PlaybackEntry> GetRecentUnique(int limit = 50)
    {
        using var ctx = _contextFactory();
        // Уникальные TrackId с временем последнего прослушивания, отсортированные по нему.
        // Используем GroupBy + Max — EF Core 10 транслирует это нативно.
        var recent = ctx.ListeningHistory
            .GroupBy(h => h.TrackId)
            .Select(g => new
            {
                TrackId = g.Key,
                LastPlayedUtc = g.Max(h => h.PlayedAtUtc)
            })
            .OrderByDescending(x => x.LastPlayedUtc)
            .Take(limit)
            .ToList();

        if (recent.Count == 0)
        {
            return Array.Empty<PlaybackEntry>();
        }

        var trackIds = recent.Select(r => r.TrackId).ToList();
        var tracks = ctx.Tracks
            .Where(t => trackIds.Contains(t.Id))
            .ToDictionary(t => t.Id, TrackMapper.ToDomain);

        var result = new List<PlaybackEntry>(recent.Count);
        foreach (var r in recent)
        {
            if (tracks.TryGetValue(r.TrackId, out var track))
            {
                result.Add(new PlaybackEntry { Track = track, PlayedAt = r.LastPlayedUtc });
            }
        }
        return result;
    }

    public IReadOnlyList<Track> GetNeverPlayed()
    {
        using var ctx = _contextFactory();
        // LEFT JOIN не нужен — достаточно сравнить множества Id через NOT EXISTS,
        // что EF транслирует из !ctx.ListeningHistory.Any(...).
        return ctx.Tracks
            .Where(t => !ctx.ListeningHistory.Any(h => h.TrackId == t.Id))
            .OrderBy(t => t.IsBuiltIn ? 0 : 1)
            .ThenBy(t => t.Id)
            .Select(t => TrackMapper.ToDomain(t))
            .ToList();
    }

    public void Append(PlaybackEntry entry)
    {
        using var ctx = _contextFactory();
        ctx.ListeningHistory.Add(new ListeningHistoryEntryEntity
        {
            TrackId = entry.Track.Id,
            PlayedAtUtc = _clock.UtcNow
        });
        ctx.SaveChanges();
    }

    private static PlaybackEntry ToEntry(ListeningHistoryEntryEntity h) => new()
    {
        Track = TrackMapper.ToDomain(h.Track),
        PlayedAt = h.PlayedAtUtc
    };
}
