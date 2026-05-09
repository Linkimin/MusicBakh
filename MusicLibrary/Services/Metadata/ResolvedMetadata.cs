namespace MusicLibrary.Services.Metadata;

public sealed class ResolvedMetadata
{
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string Genre { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public byte[]? CoverFromTag { get; init; }
    public string? CoverMimeType { get; init; }
}
