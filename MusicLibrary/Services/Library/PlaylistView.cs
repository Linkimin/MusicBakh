using MusicBakh.Core.Domain;

namespace MusicLibrary.Services.Library;

/// <summary>UI-DTO открытого плейлиста: сам плейлист + его треки (в порядке Position).</summary>
public sealed record PlaylistView(Playlist Playlist, IReadOnlyList<Track> Tracks)
{
    public System.TimeSpan TotalDuration
    {
        get { var s = System.TimeSpan.Zero; foreach (var t in Tracks) s += t.Duration; return s; }
    }
}
