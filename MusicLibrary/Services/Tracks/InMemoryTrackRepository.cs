using MusicLibrary.Models;
using System.IO;

namespace MusicLibrary.Services.Tracks;

/// <summary>
/// Учебный источник данных: список треков задается программно, как описано в работе.
/// Файлы при этом лежат в локальной папке приложения, поэтому проект можно запускать без внешних путей.
/// </summary>
public sealed class InMemoryTrackRepository : ITrackRepository
{
    public IReadOnlyList<Track> GetTracks()
    {
        string musicFolder = Path.Combine(AppContext.BaseDirectory, "Music");
        string coversFolder = Path.Combine(AppContext.BaseDirectory, "Covers");

        return new List<Track>
        {
            Create(1, "Я свободен", "Кипелов", "Рок", 204, "Кипелов - Я свободен.mp3", "ya-svoboden.jpg"),
            Create(2, "Hayloft II", "Mother Mother", "Инди", 215, "Mother Mother - Hayloft II.mp3", "hayloft-ii.jpg"),
            Create(3, "VORACITY", "MYTH ROID", "Аниме/OST", 230, "MYTH ROID - VORACITY (ПовелительВладыка ТВ-3Overlord TV-3 OP).mp3", "voracity.jpg")
        };

        Track Create(int id, string title, string artist, string genre, int durationSeconds, string fileName, string coverName)
        {
            return new Track
            {
                Id = id,
                Title = title,
                Artist = artist,
                Genre = genre,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                FilePath = Path.Combine(musicFolder, fileName),
                CoverPath = Path.Combine(coversFolder, coverName)
            };
        }
    }
}
