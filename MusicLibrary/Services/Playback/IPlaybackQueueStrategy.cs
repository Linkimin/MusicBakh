using MusicLibrary.Models;

namespace MusicLibrary.Services.Playback;

/// <summary>
/// Решает, какой трек запускается после завершения текущего.
/// Реализации не хранят состояние и используются как singleton'ы.
/// </summary>
public interface IPlaybackQueueStrategy
{
    Track? GetNext(Track current, IReadOnlyList<Track> displayedTracks);
}
