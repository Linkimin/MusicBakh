namespace MusicBakh.Infrastructure.Persistence.Entities;

/// <summary>Связь плейлист↔трек. Собственный Id (PK) — дубликаты трека в плейлисте разрешены.</summary>
internal sealed class PlaylistTrackEntity
{
    public int Id { get; set; }
    public int PlaylistId { get; set; }
    public int TrackId { get; set; }
    public int Position { get; set; }   // 0-based ручной порядок

    public PlaylistEntity Playlist { get; set; } = default!;
    public TrackEntity Track { get; set; } = default!;
}
