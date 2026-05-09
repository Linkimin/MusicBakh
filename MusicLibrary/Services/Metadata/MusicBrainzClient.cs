using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace MusicLibrary.Services.Metadata;

/// <summary>
/// Клиент REST API MusicBrainz. Сам сервер требует осмысленный User-Agent
/// и не больше одного запроса в секунду — оба условия выполнены здесь.
/// </summary>
public sealed class MusicBrainzClient : IMusicBrainzClient
{
    private const string BaseUrl = "https://musicbrainz.org/ws/2/";
    private static readonly TimeSpan MinDelayBetweenCalls = TimeSpan.FromMilliseconds(1100);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _lastCall = DateTime.MinValue;

    public MusicBrainzClient(HttpClient httpClient)
    {
        _httpClient = httpClient;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri(BaseUrl);
        }

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MusicLibrary/1.0 (educational, ryabec0@gmail.com)");
        }
    }

    public async Task<MusicBrainzMatch?> SearchAsync(string artist, string title, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        await ThrottleAsync(cancellationToken).ConfigureAwait(false);

        string query = BuildQuery(artist, title);
        string url = $"recording/?query={Uri.EscapeDataString(query)}&fmt=json&limit=1";

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(url, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            return Parse(stream);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // MusicBrainz может падать по таймауту/сети — не валим импорт, просто без онлайн-данных.
            return null;
        }
    }

    private async Task ThrottleAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            TimeSpan elapsed = DateTime.UtcNow - _lastCall;
            if (elapsed < MinDelayBetweenCalls)
            {
                await Task.Delay(MinDelayBetweenCalls - elapsed, cancellationToken).ConfigureAwait(false);
            }
            _lastCall = DateTime.UtcNow;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string BuildQuery(string artist, string title)
    {
        if (string.IsNullOrWhiteSpace(artist))
        {
            return $"recording:\"{Escape(title)}\"";
        }
        return $"recording:\"{Escape(title)}\" AND artist:\"{Escape(artist)}\"";
    }

    private static string Escape(string value)
    {
        return value.Replace("\"", "\\\"").Replace("\\", "\\\\");
    }

    private static MusicBrainzMatch? Parse(Stream stream)
    {
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("recordings", out JsonElement recordings)
            || recordings.ValueKind != JsonValueKind.Array
            || recordings.GetArrayLength() == 0)
        {
            return null;
        }

        JsonElement first = recordings[0];
        string title = first.TryGetProperty("title", out JsonElement titleElement) && titleElement.ValueKind == JsonValueKind.String
            ? titleElement.GetString() ?? string.Empty
            : string.Empty;

        string artist = string.Empty;
        if (first.TryGetProperty("artist-credit", out JsonElement credits) && credits.ValueKind == JsonValueKind.Array && credits.GetArrayLength() > 0)
        {
            JsonElement firstCredit = credits[0];
            if (firstCredit.TryGetProperty("name", out JsonElement nameElement) && nameElement.ValueKind == JsonValueKind.String)
            {
                artist = nameElement.GetString() ?? string.Empty;
            }
            else if (firstCredit.TryGetProperty("artist", out JsonElement artistElement)
                     && artistElement.TryGetProperty("name", out JsonElement artistName))
            {
                artist = artistName.GetString() ?? string.Empty;
            }
        }

        string genre = string.Empty;
        if (first.TryGetProperty("tags", out JsonElement tags) && tags.ValueKind == JsonValueKind.Array)
        {
            JsonElement bestTag = default;
            int bestCount = -1;
            foreach (JsonElement tag in tags.EnumerateArray())
            {
                int count = tag.TryGetProperty("count", out JsonElement countElement) && countElement.TryGetInt32(out int parsed) ? parsed : 0;
                if (count > bestCount)
                {
                    bestCount = count;
                    bestTag = tag;
                }
            }
            if (bestCount >= 0 && bestTag.TryGetProperty("name", out JsonElement tagName))
            {
                genre = tagName.GetString() ?? string.Empty;
            }
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        return new MusicBrainzMatch { Title = title, Artist = artist, Genre = genre };
    }
}
