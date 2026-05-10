using MusicLibrary.Services.Covers;
using System.IO;
using System.Text.RegularExpressions;

namespace MusicLibrary.Services.Metadata;

/// <summary>
/// Каскадный резолвер: ID3 — основа, но грязные сайты-агрегаторы (drivemusic.me, muzofond и пр.)
/// часто пишут свой бренд в теги. Поэтому ID3 проходит через очистку, а если что-то
/// осталось грязным или пустым — данные дополняются через MusicBrainz.
/// </summary>
public sealed class DefaultMetadataResolver : IMetadataResolver
{
    private static readonly Regex BracketSuffixRegex = new(@"\s*[\[\(][^\[\(\]\)]*[\]\)]\s*$", RegexOptions.Compiled);

    private static readonly string[] AggregatorBrandKeywords =
    {
        "drivemusic", "muzofond", "mp3uk", "mp3party", "zaycev", "zaitsev",
        "savemusic", "savefrom", "freemusic", "lightaudio", "hotmix", "allmuzon",
        "mp3.club", "musify", "audiopoisk", "myzcloud", "muzbear", "muzmo"
    };

    private readonly ITagReader _tagReader;
    private readonly IMusicBrainzClient _musicBrainzClient;
    private readonly IItunesCoverClient _itunesClient;
    private readonly IGenreNormalizer _genreNormalizer;

    public DefaultMetadataResolver(
        ITagReader tagReader,
        IMusicBrainzClient musicBrainzClient,
        IItunesCoverClient itunesClient,
        IGenreNormalizer genreNormalizer)
    {
        _tagReader = tagReader;
        _musicBrainzClient = musicBrainzClient;
        _itunesClient = itunesClient;
        _genreNormalizer = genreNormalizer;
    }

    public async Task<ResolvedMetadata> ResolveAsync(string filePath, string? filenameHint, CancellationToken cancellationToken)
    {
        LocalTagInfo tag = _tagReader.Read(filePath);

        string title = StripBrandSuffix(tag.Title);
        string artist = StripBrandSuffix(tag.Artist);
        bool artistWasDirty = !string.Equals(artist, tag.Artist?.Trim(), StringComparison.Ordinal);
        string rawGenre = IsTrashGenre(tag.Genre) ? string.Empty : tag.Genre;

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
        {
            (string hintArtist, string hintTitle) = ParseFilename(filenameHint ?? Path.GetFileNameWithoutExtension(filePath));
            if (string.IsNullOrWhiteSpace(title))
            {
                title = hintTitle;
            }
            if (string.IsNullOrWhiteSpace(artist))
            {
                artist = hintArtist;
            }
        }

        // Если ID3 был замусорен (агрегатор), даже при «полных» полях идём в MusicBrainz
        // за нормальным жанром.
        bool needsOnlineLookup = !string.IsNullOrWhiteSpace(title)
            && (string.IsNullOrWhiteSpace(artist)
                || string.IsNullOrWhiteSpace(rawGenre)
                || artistWasDirty);

        if (needsOnlineLookup)
        {
            MusicBrainzMatch? match = await _musicBrainzClient.SearchAsync(artist, title, cancellationToken).ConfigureAwait(false);
            if (match is not null)
            {
                if (string.IsNullOrWhiteSpace(artist) || artistWasDirty)
                {
                    artist = match.Artist;
                }
                if (string.IsNullOrWhiteSpace(rawGenre))
                {
                    rawGenre = match.Genre;
                }
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = match.Title;
                }
            }
        }

        // MusicBrainz часто возвращает recording без тегов (теги лежат на release-group),
        // поэтому iTunes Search API используется как fallback по жанру.
        if (string.IsNullOrWhiteSpace(rawGenre) && !string.IsNullOrWhiteSpace(title))
        {
            ItunesSearchHit? itunes = await _itunesClient.SearchAsync(artist, title, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(itunes?.PrimaryGenreName))
            {
                rawGenre = itunes!.PrimaryGenreName!;
            }
        }

        return new ResolvedMetadata
        {
            Title = title.Trim(),
            Artist = artist.Trim(),
            Genre = _genreNormalizer.Normalize(rawGenre),
            Duration = tag.Duration,
            CoverFromTag = tag.CoverBytes,
            CoverMimeType = tag.CoverMimeType
        };
    }

    private static string StripBrandSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();

        // Пока в хвосте есть [...] или (...) с подозрительным содержимым — отрезаем.
        // (Реальные суффиксы вида "(feat. X)" или "(Remix)" не трогаем.)
        while (true)
        {
            Match match = BracketSuffixRegex.Match(trimmed);
            if (!match.Success)
            {
                break;
            }
            string inside = match.Value.Trim().TrimStart('[', '(').TrimEnd(']', ')').Trim();
            if (!IsSuspiciousBrand(inside))
            {
                break;
            }
            trimmed = trimmed[..match.Index].TrimEnd();
        }

        return trimmed;
    }

    private static bool IsSuspiciousBrand(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string lowered = token.ToLowerInvariant();
        if (lowered.Contains('.') || lowered.Contains("://") || lowered.StartsWith("www."))
        {
            return true;
        }

        foreach (string brand in AggregatorBrandKeywords)
        {
            if (lowered.Contains(brand))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTrashGenre(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string lowered = value.Trim().ToLowerInvariant();

        if (lowered.Contains("://") || lowered.StartsWith("www."))
        {
            return true;
        }

        string[] suspiciousTlds = { ".fm", ".ru", ".com", ".net", ".org", ".info", ".su", ".by", ".ua", ".kz", ".me" };
        foreach (string tld in suspiciousTlds)
        {
            if (lowered.EndsWith(tld) || lowered.Contains(tld + "/"))
            {
                return true;
            }
        }

        foreach (string brand in AggregatorBrandKeywords)
        {
            if (lowered.Contains(brand))
            {
                return true;
            }
        }

        return false;
    }

    private static (string Artist, string Title) ParseFilename(string filename)
    {
        // Поддерживаем форматы "Artist - Title" и "Artist — Title".
        string[] separators = { " - ", " — " };
        foreach (string separator in separators)
        {
            int index = filename.IndexOf(separator, StringComparison.Ordinal);
            if (index > 0 && index < filename.Length - separator.Length)
            {
                return (
                    StripBrandSuffix(filename[..index]),
                    StripBrandSuffix(filename[(index + separator.Length)..]));
            }
        }

        return (string.Empty, StripBrandSuffix(filename));
    }
}
