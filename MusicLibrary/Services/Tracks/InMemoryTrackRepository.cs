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
            Create(1, "Satisfaction", "Benny Benassi", "Электроника", 285, "Benny Benassi - Satisfaction.mp3", "satisfaction.png"),
            Create(2, "In My Mind", "Dynoro, Gigi D'Agostino", "Электроника", 183, "Dynoro, Gigi D'Agostino - In My Mind.mp3", "in-my-mind.png"),
            Create(3, "Антидепрессант", "FIZICA", "Инди", 234, "FIZICA - Антидепрессант.mp3", "antidepressant.png"),
            Create(4, "Я свободен", "Кипелов", "Рок", 204, "Кипелов - Я свободен.mp3", "ya-svoboden.png"),
            Create(5, "Судно (Борис Рыжий)", "Molchat Doma", "Постпанк", 141, "Molchat Doma - Судно (борис рижий).mp3", "sudno.png"),
            Create(6, "Hayloft II", "Mother Mother", "Инди", 215, "Mother Mother - Hayloft II.mp3", "hayloft-ii.png"),
            Create(7, "Hysteria", "Muse", "Рок", 227, "Muse - Hysteria.mp3", "hysteria.png"),
            Create(8, "VORACITY", "MYTH ROID", "Аниме/OST", 230, "MYTH ROID - VORACITY (ПовелительВладыка ТВ-3Overlord TV-3 OP).mp3", "voracity.png"),
            Create(9, "Gods", "Onsa Media", "Аниме/OST", 222, "Onsa Media - Gods.mp3", "gods.png"),
            Create(10, "Soap Lagoon (Russian ver.)", "Onsa Media", "Аниме/OST", 223, "Onsa Media - Soap Lagoon (Russian ver.).mp3", "soap-lagoon.png"),
            Create(11, "Meds", "Placebo feat. Alison Mosshart", "Рок", 175, "Placebo feat. Alison Mosshart - Meds.mp3", "meds.png"),
            Create(12, "We Drink Your Blood", "Powerwolf", "Метал", 222, "Powerwolf - We Drink Your Blood.mp3", "we-drink-your-blood.png"),
            Create(13, "HOLLOW HUNGER (Raon cover)", "Raon", "Аниме/OST", 220, "Raon - HOLLOW HUNGER _ Overlord IV OP┃Raon cover.mp3", "hollow-hunger.png"),
            Create(14, "KILLA!", "SHADXWBXRN", "Фонк", 106, "SHADXWBXRN - KILLA!.mp3", "killa.png"),
            Create(15, "KNIGHT", "SHADXWBXRN", "Фонк", 122, "SHADXWBXRN - KNIGHT.mp3", "knight.png"),
            Create(16, "Seven Nation Army", "The White Stripes", "Рок", 231, "The White Stripes - Seven Nation Army.mp3", "seven-nation-army.png")
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
