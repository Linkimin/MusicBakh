using MusicLibrary.Models;

namespace MusicLibrary.Services.Tracks;

public interface ITrackRepository
{
    IReadOnlyList<Track> GetTracks();
}
