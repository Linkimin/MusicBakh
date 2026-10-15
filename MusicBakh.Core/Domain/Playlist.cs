namespace MusicBakh.Core.Domain;

/// <summary>Статический плейлист: имя + время создания. Треки — через IPlaylistRepository.</summary>
public sealed record Playlist(int Id, string Name, DateTime CreatedAtUtc);
