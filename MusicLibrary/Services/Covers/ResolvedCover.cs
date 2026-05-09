namespace MusicLibrary.Services.Covers;

public sealed class ResolvedCover
{
    public required byte[] Bytes { get; init; }
    public required string Extension { get; init; }
}
