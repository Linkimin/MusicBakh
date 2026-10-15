using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence.Repositories;

/// <summary>
/// Репозиторий треков поверх LibraryDbContext. Получает фабрику контекста, чтобы каждый
/// вызов работал на свежем коротком контексте — это устраняет проблемы с life-time'ом
/// при многопоточности и упрощает тесты.
/// </summary>
public sealed class SqliteTrackRepository : ITrackRepository
{
    private readonly Func<LibraryDbContext> _contextFactory;

    public SqliteTrackRepository(Func<LibraryDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public IReadOnlyList<Track> GetAll()
    {
        using var ctx = _contextFactory();
        return ctx.Tracks
            .OrderBy(t => t.IsBuiltIn ? 0 : 1)
            .ThenBy(t => t.Id)
            .Select(TrackMapper.ToDomain)
            .ToList();
    }

    public Track? FindById(int id)
    {
        using var ctx = _contextFactory();
        var entity = ctx.Tracks.FirstOrDefault(t => t.Id == id);
        return entity is null ? null : TrackMapper.ToDomain(entity);
    }

    public Track Add(Track track)
    {
        // IsBuiltIn=true принимаем сознательно: этот путь использует только seed-loader
        // при первом запуске. У UI нет способа собрать Track с IsBuiltIn=true (форма
        // импорта явно ставит false), поэтому отдельной защиты здесь не требуется.
        using var ctx = _contextFactory();
        var entity = new TrackEntity
        {
            Title = track.Title,
            Artist = track.Artist,
            Album = track.Album,
            Genre = track.Genre,
            DurationTicks = track.Duration.Ticks,
            FilePath = track.FilePath,
            CoverPath = track.CoverPath,
            Rating = track.Rating,
            Reaction = (int)track.Reaction,
            Year = track.Year,
            TrackNumber = track.TrackNumber,
            AlbumArtist = track.AlbumArtist,
            AddedAtUtc = DateTime.UtcNow,
            IsBuiltIn = track.IsBuiltIn
        };
        ctx.Tracks.Add(entity);
        ctx.SaveChanges();

        return TrackMapper.ToDomain(entity);
    }

    public void Update(Track track)
    {
        using var ctx = _contextFactory();
        var entity = ctx.Tracks.FirstOrDefault(t => t.Id == track.Id);
        if (entity is null)
        {
            return;
        }

        entity.Title = track.Title;
        entity.Artist = track.Artist;
        entity.Album = track.Album;
        entity.Genre = track.Genre;
        entity.DurationTicks = track.Duration.Ticks;
        entity.FilePath = track.FilePath;
        entity.CoverPath = track.CoverPath;
        entity.Rating = track.Rating;
        entity.Reaction = (int)track.Reaction;
        entity.Year = track.Year;
        entity.TrackNumber = track.TrackNumber;
        entity.AlbumArtist = track.AlbumArtist;
        entity.IsBuiltIn = track.IsBuiltIn;
        // AddedAtUtc намеренно не трогаем — это «дата добавления в библиотеку», она не
        // должна обнуляться при обновлении полей.

        ctx.SaveChanges();
    }

    public void Remove(int id)
    {
        using var ctx = _contextFactory();
        var entity = ctx.Tracks.FirstOrDefault(t => t.Id == id);
        if (entity is null)
        {
            return;
        }
        if (entity.IsBuiltIn)
        {
            throw new NotSupportedException("Built-in tracks cannot be removed.");
        }
        ctx.Tracks.Remove(entity);
        ctx.SaveChanges();
    }
}
