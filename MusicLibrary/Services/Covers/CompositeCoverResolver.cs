using MusicLibrary.Services.Metadata;

namespace MusicLibrary.Services.Covers;

/// <summary>
/// Каскад источников обложки: ID3 APIC → iTunes Search → процедурная заглушка.
/// Возвращает всегда не null — последний шаг гарантированно создает картинку.
/// </summary>
public sealed class CompositeCoverResolver : ICoverResolver
{
    private readonly IItunesCoverClient _itunesClient;
    private readonly IProceduralCoverGenerator _proceduralGenerator;

    public CompositeCoverResolver(IItunesCoverClient itunesClient, IProceduralCoverGenerator proceduralGenerator)
    {
        _itunesClient = itunesClient;
        _proceduralGenerator = proceduralGenerator;
    }

    public async Task<ResolvedCover> ResolveAsync(ResolvedMetadata metadata, CancellationToken cancellationToken)
    {
        if (metadata.CoverFromTag is { Length: > 0 })
        {
            string extension = ExtensionFromMime(metadata.CoverMimeType);
            return new ResolvedCover { Bytes = metadata.CoverFromTag, Extension = extension };
        }

        ResolvedCover? itunes = await _itunesClient.FindAsync(metadata.Artist, metadata.Title, cancellationToken).ConfigureAwait(false);
        if (itunes is not null)
        {
            return itunes;
        }

        return _proceduralGenerator.Generate(metadata.Artist, metadata.Title);
    }

    private static string ExtensionFromMime(string? mime)
    {
        if (string.IsNullOrWhiteSpace(mime))
        {
            return "jpg";
        }

        return mime.ToLowerInvariant() switch
        {
            "image/png" => "png",
            "image/jpeg" or "image/jpg" => "jpg",
            "image/webp" => "webp",
            _ => "jpg"
        };
    }
}
