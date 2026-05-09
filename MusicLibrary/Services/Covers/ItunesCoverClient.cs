using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MusicLibrary.Services.Covers;

/// <summary>
/// Поиск метаданных через iTunes Search API: https://itunes.apple.com/search.
/// Бесплатный, без ключа. Помимо обложки 600x600, отдает primaryGenreName,
/// который используется как fallback, когда MusicBrainz не вернул жанр.
/// </summary>
public sealed class ItunesCoverClient : IItunesCoverClient
{
    private const string SearchUrl = "https://itunes.apple.com/search?term={0}&entity=song&limit=1";
    private static readonly Regex ArtworkSizeRegex = new(@"/\d+x\d+bb\.(jpg|png)", RegexOptions.Compiled);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _httpClient;

    public ItunesCoverClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ItunesSearchHit?> SearchAsync(string artist, string title, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        string term = string.IsNullOrWhiteSpace(artist) ? title : $"{artist} {title}";
        string url = string.Format(SearchUrl, Uri.EscapeDataString(term));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            using HttpResponseMessage searchResponse = await _httpClient.GetAsync(url, timeout.Token).ConfigureAwait(false);
            if (!searchResponse.IsSuccessStatusCode)
            {
                return null;
            }

            await using Stream searchStream = await searchResponse.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            return ParseHit(searchStream);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    public async Task<ResolvedCover?> FindAsync(string artist, string title, CancellationToken cancellationToken)
    {
        ItunesSearchHit? hit = await SearchAsync(artist, title, cancellationToken).ConfigureAwait(false);
        if (hit?.ArtworkUrl is null)
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            string upgraded = ArtworkSizeRegex.Replace(hit.ArtworkUrl, "/600x600bb.$1");
            string extension = Path.GetExtension(new Uri(upgraded).LocalPath).TrimStart('.');
            if (string.IsNullOrEmpty(extension))
            {
                extension = "jpg";
            }

            byte[] bytes = await _httpClient.GetByteArrayAsync(upgraded, timeout.Token).ConfigureAwait(false);
            return new ResolvedCover { Bytes = bytes, Extension = extension };
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static ItunesSearchHit? ParseHit(Stream stream)
    {
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("results", out JsonElement results)
            || results.ValueKind != JsonValueKind.Array
            || results.GetArrayLength() == 0)
        {
            return null;
        }

        JsonElement first = results[0];

        string? artworkUrl = null;
        if (first.TryGetProperty("artworkUrl100", out JsonElement element) && element.ValueKind == JsonValueKind.String)
        {
            artworkUrl = element.GetString();
        }
        else if (first.TryGetProperty("artworkUrl60", out element) && element.ValueKind == JsonValueKind.String)
        {
            artworkUrl = element.GetString();
        }

        string? genre = null;
        if (first.TryGetProperty("primaryGenreName", out JsonElement genreElement) && genreElement.ValueKind == JsonValueKind.String)
        {
            genre = genreElement.GetString();
        }

        if (artworkUrl is null && string.IsNullOrWhiteSpace(genre))
        {
            return null;
        }

        return new ItunesSearchHit { ArtworkUrl = artworkUrl, PrimaryGenreName = genre };
    }
}
