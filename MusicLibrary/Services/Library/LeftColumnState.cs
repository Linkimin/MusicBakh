using MusicBakh.Core.Domain;

namespace MusicLibrary.Services.Library;

/// <summary>
/// Discriminated union состояний левой колонки. Записи (record-наследники
/// abstract record) позволяют биндингам и TemplateSelector-у различать варианты.
/// </summary>
public abstract record LeftColumnState
{
    public sealed record TracksRoot : LeftColumnState;
    public sealed record AlbumsRoot : LeftColumnState;
    public sealed record ArtistsRoot : LeftColumnState;
    public sealed record AlbumDetail(AlbumAggregate Album) : LeftColumnState;
    public sealed record ArtistDetail(ArtistAggregate Artist) : LeftColumnState;
    public sealed record PlaylistsRoot : LeftColumnState;
    public sealed record PlaylistDetail(PlaylistView Playlist) : LeftColumnState;
}
