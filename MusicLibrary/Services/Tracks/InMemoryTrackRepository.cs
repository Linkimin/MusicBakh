using MusicLibrary.Models;
using System.IO;

namespace MusicLibrary.Services.Tracks;

/// <summary>
/// Учебный источник данных: список треков задается программно, как описано в работе.
/// Отличие только в том, что данные вынесены из окна в отдельный сервис.
/// </summary>
public sealed class InMemoryTrackRepository : ITrackRepository
{
    public IReadOnlyList<Track> GetTracks()
    {
        string musicFolder = Path.Combine(AppContext.BaseDirectory, "Music");
        string coversFolder = Path.Combine(AppContext.BaseDirectory, "Covers");

        return new List<Track>
        {
            new()
            {
                Id = 1,
                Title = "Moonlight Sonata",
                Artist = "Ludwig van Beethoven",
                Genre = "Классика",
                Duration = TimeSpan.FromSeconds(332),
                FilePath = Path.Combine(musicFolder, "moonlight-sonata.mp3"),
                CoverPath = Path.Combine(coversFolder, "classic.jpg")
            },
            new()
            {
                Id = 2,
                Title = "Blue in Green",
                Artist = "Miles Davis",
                Genre = "Джаз",
                Duration = TimeSpan.FromSeconds(337),
                FilePath = Path.Combine(musicFolder, "blue-in-green.mp3"),
                CoverPath = Path.Combine(coversFolder, "jazz.jpg")
            },
            new()
            {
                Id = 3,
                Title = "Bohemian Rhapsody",
                Artist = "Queen",
                Genre = "Рок",
                Duration = TimeSpan.FromSeconds(355),
                FilePath = Path.Combine(musicFolder, "bohemian-rhapsody.mp3"),
                CoverPath = Path.Combine(coversFolder, "rock.jpg")
            },
            new()
            {
                Id = 4,
                Title = "Take Five",
                Artist = "Dave Brubeck",
                Genre = "Джаз",
                Duration = TimeSpan.FromSeconds(324),
                FilePath = Path.Combine(musicFolder, "take-five.mp3"),
                CoverPath = Path.Combine(coversFolder, "jazz.jpg")
            },
            new()
            {
                Id = 5,
                Title = "Clair de Lune",
                Artist = "Claude Debussy",
                Genre = "Классика",
                Duration = TimeSpan.FromSeconds(282),
                FilePath = Path.Combine(musicFolder, "clair-de-lune.mp3"),
                CoverPath = Path.Combine(coversFolder, "classic.jpg")
            },
            new()
            {
                Id = 6,
                Title = "Stairway to Heaven",
                Artist = "Led Zeppelin",
                Genre = "Рок",
                Duration = TimeSpan.FromSeconds(482),
                FilePath = Path.Combine(musicFolder, "stairway-to-heaven.mp3"),
                CoverPath = Path.Combine(coversFolder, "rock.jpg")
            }
        };
    }
}
