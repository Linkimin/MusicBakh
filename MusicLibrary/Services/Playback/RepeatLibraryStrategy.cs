using MusicLibrary.Models;

namespace MusicLibrary.Services.Playback;

/// <summary>
/// Зацикливание видимого списка: после последнего трека возвращаемся к первому.
/// Если current не найден в списке — отдаём первый элемент (например, после смены фильтра).
/// </summary>
public sealed class RepeatLibraryStrategy : IPlaybackQueueStrategy
{
    public static RepeatLibraryStrategy Instance { get; } = new();

    private RepeatLibraryStrategy() { }

    public Track? GetNext(Track current, IReadOnlyList<Track> displayedTracks)
    {
        if (displayedTracks.Count == 0)
        {
            return null;
        }

        int index = IndexOfById(current, displayedTracks);
        if (index < 0)
        {
            return displayedTracks[0];
        }

        int nextIndex = (index + 1) % displayedTracks.Count;
        return displayedTracks[nextIndex];
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
