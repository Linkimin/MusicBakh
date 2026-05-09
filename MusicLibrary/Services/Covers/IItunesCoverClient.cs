namespace MusicLibrary.Services.Covers;

/// <summary>
/// Клиент iTunes Search API (бесплатный, без ключа). Используется и для обложки,
/// и для жанра (primaryGenreName) когда MusicBrainz не вернул теги.
/// </summary>
public interface IItunesCoverClient
{
    Task<ResolvedCover?> FindAsync(string artist, string title, CancellationToken cancellationToken);
    Task<ItunesSearchHit?> SearchAsync(string artist, string title, CancellationToken cancellationToken);
}
