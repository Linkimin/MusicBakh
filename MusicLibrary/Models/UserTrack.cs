namespace MusicLibrary.Models;

/// <summary>
/// DTO для пользовательских треков, сохраняемых в JSON-файл userTracks.json.
/// От доменной модели Track отличается тем, что длительность хранится в секундах
/// (TimeSpan не дружит с разными версиями сериализатора) и есть метка времени AddedAt.
/// </summary>
public sealed class UserTrack
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public double DurationSeconds { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string CoverPath { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }

    public Track ToTrack() => new()
    {
        Id = Id,
        Title = Title,
        Artist = Artist,
        Genre = Genre,
        Duration = TimeSpan.FromSeconds(DurationSeconds),
        FilePath = FilePath,
        CoverPath = CoverPath
    };

    public static UserTrack FromTrack(Track track, DateTime addedAt) => new()
    {
        Id = track.Id,
        Title = track.Title,
        Artist = track.Artist,
        Genre = track.Genre,
        DurationSeconds = track.Duration.TotalSeconds,
        FilePath = track.FilePath,
        CoverPath = track.CoverPath,
        AddedAt = addedAt
    };
}
