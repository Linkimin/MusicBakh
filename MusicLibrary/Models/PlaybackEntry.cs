namespace MusicLibrary.Models;

/// <summary>
/// Запись истории прослушиваний: какой трек был запущен и в какой момент.
/// </summary>
public sealed class PlaybackEntry
{
    public required Track Track { get; init; }
    public DateTime PlayedAt { get; init; }
}
