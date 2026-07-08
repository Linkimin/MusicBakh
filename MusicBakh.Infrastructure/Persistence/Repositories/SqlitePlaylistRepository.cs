using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence.Repositories;

/// <summary>Плейлисты поверх LibraryDbContext. Порядок треков — через Position; изменения позиций атомарны.</summary>
public sealed class SqlitePlaylistRepository : IPlaylistRepository
{
    private readonly Func<LibraryDbContext> _contextFactory;
    private readonly IClock _clock;

    public SqlitePlaylistRepository(Func<LibraryDbContext> contextFactory, IClock clock)
    {
        _contextFactory = contextFactory;
        _clock = clock;
    }

    public IReadOnlyList<Playlist> GetAll()
    {
        using var ctx = _contextFactory();
        return ctx.Playlists
            .OrderByDescending(p => p.CreatedAtUtc).ThenByDescending(p => p.Id)
            .Select(p => new Playlist(p.Id, p.Name, p.CreatedAtUtc))
            .ToList();
    }

    public Playlist? GetById(int id)
    {
        using var ctx = _contextFactory();
        var e = ctx.Playlists.FirstOrDefault(p => p.Id == id);
        return e is null ? null : new Playlist(e.Id, e.Name, e.CreatedAtUtc);
    }

    public Playlist Create(string name)
    {
        using var ctx = _contextFactory();
        var e = new PlaylistEntity { Name = name, CreatedAtUtc = _clock.UtcNow };
        ctx.Playlists.Add(e);
        ctx.SaveChanges();
        return new Playlist(e.Id, e.Name, e.CreatedAtUtc);
    }

    public void Rename(int id, string name)
    {
        using var ctx = _contextFactory();
        var e = ctx.Playlists.FirstOrDefault(p => p.Id == id);
        if (e is null) return;
        e.Name = name;
        ctx.SaveChanges();
    }

    public void Delete(int id)
    {
        using var ctx = _contextFactory();
        var e = ctx.Playlists.FirstOrDefault(p => p.Id == id);
        if (e is null) return;
        ctx.Playlists.Remove(e);   // PlaylistTracks уходят по cascade
        ctx.SaveChanges();
    }

    public IReadOnlyList<Track> GetTracks(int playlistId)
    {
        using var ctx = _contextFactory();
        return ctx.PlaylistTracks
            .Where(pt => pt.PlaylistId == playlistId)
            .OrderBy(pt => pt.Position)
            .Select(pt => pt.Track)
            .Select(MapToDomain)
            .ToList();
    }

    public int GetTrackCount(int playlistId)
    {
        using var ctx = _contextFactory();
        return ctx.PlaylistTracks.Count(pt => pt.PlaylistId == playlistId);
    }

    public void AddTrack(int playlistId, int trackId) => AddTracks(playlistId, new[] { trackId });

    public void AddTracks(int playlistId, IReadOnlyList<int> trackIds)
    {
        if (trackIds.Count == 0) return;
        using var ctx = _contextFactory();
        int next = ctx.PlaylistTracks.Where(pt => pt.PlaylistId == playlistId)
            .Select(pt => (int?)pt.Position).Max() is int max ? max + 1 : 0;
        foreach (var id in trackIds)
        {
            ctx.PlaylistTracks.Add(new PlaylistTrackEntity { PlaylistId = playlistId, TrackId = id, Position = next++ });
        }
        ctx.SaveChanges();
    }

    public void RemoveAt(int playlistId, int position)
    {
        using var ctx = _contextFactory();
        using var tx = ctx.Database.BeginTransaction();
        var rows = ctx.PlaylistTracks.Where(pt => pt.PlaylistId == playlistId)
            .OrderBy(pt => pt.Position).ToList();
        if (position < 0 || position >= rows.Count) return;
        ctx.PlaylistTracks.Remove(rows[position]);
        for (int i = position + 1; i < rows.Count; i++) rows[i].Position = i - 1;
        ctx.SaveChanges();
        tx.Commit();
    }

    public void Reorder(int playlistId, int fromPosition, int toPosition)
    {
        using var ctx = _contextFactory();
        using var tx = ctx.Database.BeginTransaction();
        var rows = ctx.PlaylistTracks.Where(pt => pt.PlaylistId == playlistId)
            .OrderBy(pt => pt.Position).ToList();
        if (fromPosition < 0 || fromPosition >= rows.Count || toPosition < 0 || toPosition >= rows.Count) return;
        var moved = rows[fromPosition];
        rows.RemoveAt(fromPosition);
        rows.Insert(toPosition, moved);
        for (int i = 0; i < rows.Count; i++) rows[i].Position = i;
        ctx.SaveChanges();
        tx.Commit();
    }

    private static Track MapToDomain(TrackEntity e) => new()
    {
        Id = e.Id, Title = e.Title, Artist = e.Artist, Album = e.Album, Genre = e.Genre,
        Duration = TimeSpan.FromTicks(e.DurationTicks), FilePath = e.FilePath, CoverPath = e.CoverPath,
        Rating = e.Rating, Reaction = (TrackReaction)e.Reaction,
        Year = e.Year, TrackNumber = e.TrackNumber, AlbumArtist = e.AlbumArtist, IsBuiltIn = e.IsBuiltIn
    };
}
