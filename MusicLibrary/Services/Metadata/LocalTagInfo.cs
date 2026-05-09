namespace MusicLibrary.Services.Metadata;

/// <summary>
/// Сводка ID3-тегов аудиофайла. Любое поле может быть пустым, если в файле тег отсутствует.
/// </summary>
public sealed class LocalTagInfo
{
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string Genre { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public byte[]? CoverBytes { get; init; }
    public string? CoverMimeType { get; init; }
}
