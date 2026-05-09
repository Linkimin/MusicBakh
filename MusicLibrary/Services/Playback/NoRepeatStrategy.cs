using MusicLibrary.Models;

namespace MusicLibrary.Services.Playback;

/// <summary>
/// Базовый auto-next: следующий трек по индексу, после последнего — null (стоп).
/// </summary>
public sealed class NoRepeatStrategy : IPlaybackQueueStrategy
{
    public static NoRepeatStrategy Instance { get; } = new();

    private NoRepeatStrategy() { }

    public Track? GetNext(Track current, IReadOnlyList<Track> displayedTracks)
    {
        int index = IndexOfById(current, displayedTracks);
        if (index < 0 || index + 1 >= displayedTracks.Count)
        {
            return null;
        }
        return displayedTracks[index + 1];
    }

    private static int IndexOfById(Track track, IReadOnlyList<Track> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Id == track.Id)
            {
                return i;
            }
        }
        return -1;
    }
}
