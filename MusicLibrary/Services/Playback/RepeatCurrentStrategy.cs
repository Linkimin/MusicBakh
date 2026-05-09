using MusicLibrary.Models;

namespace MusicLibrary.Services.Playback;

/// <summary>
/// Зацикливание текущего трека: всегда возвращаем тот же объект.
/// </summary>
public sealed class RepeatCurrentStrategy : IPlaybackQueueStrategy
{
    public static RepeatCurrentStrategy Instance { get; } = new();

    private RepeatCurrentStrategy() { }

    public Track? GetNext(Track current, IReadOnlyList<Track> displayedTracks) => current;
}
