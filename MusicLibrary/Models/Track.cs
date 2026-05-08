namespace MusicLibrary.Models;

/// <summary>
/// Описывает музыкальный трек из локальной библиотеки.
/// Модель не зависит от WPF, чтобы данные можно было использовать и в интерфейсе, и в тестах.
/// </summary>
public sealed class Track
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string Genre { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public string CoverPath { get; init; } = string.Empty;

    public string DurationText => Duration.ToString(@"m\:ss");
}
