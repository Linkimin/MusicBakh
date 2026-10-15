namespace MusicBakh.Infrastructure.Persistence.Entities;

internal sealed class PlaylistEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
