using MusicLibrary.Models;
using MusicLibrary.Services.Covers;
using MusicLibrary.Services.Metadata;
using MusicLibrary.Services.Storage;
using System.IO;
using System.Net.Http;

namespace MusicLibrary.Services.Import;

/// <summary>
/// Один pipeline для обоих источников: сначала аудио оказывается на диске
/// (копированием или загрузкой), затем считываются метаданные, затем обложка
/// сохраняется как отдельный файл рядом с аудио. Любые исключения превращаются
/// в ImportResult.Error — окно импорта никогда не должно зависать.
/// </summary>
public sealed class TrackImporter : ITrackImporter
{
    private const long MaxDownloadBytes = 50L * 1024 * 1024;
    private static readonly string[] AllowedExtensions = { ".mp3", ".wav" };

    private readonly IUserTrackStorage _storage;
    private readonly IMetadataResolver _metadataResolver;
    private readonly ICoverResolver _coverResolver;
    private readonly HttpClient _httpClient;

    public TrackImporter(
        IUserTrackStorage storage,
        IMetadataResolver metadataResolver,
        ICoverResolver coverResolver,
        HttpClient httpClient)
    {
        _storage = storage;
        _metadataResolver = metadataResolver;
        _coverResolver = coverResolver;
        _httpClient = httpClient;
    }

    public async Task<ImportResult> ImportAsync(ImportRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        try
        {
            string audioPath;
            string filenameHint;

            switch (request)
            {
                case LocalFileImportRequest local:
                    (audioPath, filenameHint) = await CopyLocalAsync(local.FilePath, cancellationToken).ConfigureAwait(false);
                    progress?.Report(0.5);
                    break;
                case UrlImportRequest url:
                    (audioPath, filenameHint) = await DownloadAsync(url.Url, progress, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    return ImportResult.Error("Неизвестный тип источника.");
            }

            ResolvedMetadata metadata = await _metadataResolver.ResolveAsync(audioPath, filenameHint, cancellationToken).ConfigureAwait(false);
            ResolvedCover cover = await _coverResolver.ResolveAsync(metadata, cancellationToken).ConfigureAwait(false);

            string coverPath = Path.Combine(_storage.CoversDirectory, $"{Guid.NewGuid():N}.{cover.Extension}");
            await File.WriteAllBytesAsync(coverPath, cover.Bytes, cancellationToken).ConfigureAwait(false);

            progress?.Report(1.0);

            return ImportResult.Success(new TrackImportCandidate
            {
                AudioFilePath = audioPath,
                CoverFilePath = coverPath,
                Title = string.IsNullOrWhiteSpace(metadata.Title) ? filenameHint : metadata.Title,
                Artist = metadata.Artist,
                Genre = metadata.Genre,
                Duration = metadata.Duration
            });
        }
        catch (OperationCanceledException)
        {
            return ImportResult.Error("Импорт занял слишком много времени или был отменен.");
        }
        catch (HttpRequestException exception)
        {
            return ImportResult.Error($"Не удалось загрузить файл: {exception.Message}");
        }
        catch (IOException exception)
        {
            return ImportResult.Error($"Ошибка работы с файлом: {exception.Message}");
        }
        catch (UriFormatException)
        {
            return ImportResult.Error("Некорректный URL.");
        }
        catch (Exception exception)
        {
            // Любое нештатное исключение не должно оставлять окно в зависшем состоянии.
            return ImportResult.Error($"Неожиданная ошибка: {exception.Message}");
        }
    }

    private async Task<(string AudioPath, string FilenameHint)> CopyLocalAsync(string sourcePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Исходный файл не найден.", sourcePath);
        }

        string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (Array.IndexOf(AllowedExtensions, extension) < 0)
        {
            throw new IOException("Поддерживаются только файлы .mp3 и .wav.");
        }

        string targetPath = Path.Combine(_storage.MusicDirectory, $"{Guid.NewGuid():N}{extension}");
        await using FileStream sourceStream = File.OpenRead(sourcePath);
        await using FileStream targetStream = File.Create(targetPath);
        await sourceStream.CopyToAsync(targetStream, cancellationToken).ConfigureAwait(false);

        return (targetPath, Path.GetFileNameWithoutExtension(sourcePath));
    }

    private async Task<(string AudioPath, string FilenameHint)> DownloadAsync(string url, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var uri = new Uri(url, UriKind.Absolute);
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new IOException("Поддерживаются только http(s) ссылки.");
        }

        string urlPath = uri.AbsolutePath;
        string urlExtension = Path.GetExtension(urlPath).ToLowerInvariant();

        using HttpResponseMessage response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        string? contentType = response.Content.Headers.ContentType?.MediaType;
        if (Array.IndexOf(AllowedExtensions, urlExtension) < 0
            && (contentType is null || !contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)))
        {
            throw new IOException("Ссылка не указывает на mp3/wav файл.");
        }

        string extension = Array.IndexOf(AllowedExtensions, urlExtension) >= 0
            ? urlExtension
            : (contentType?.Equals("audio/wav", StringComparison.OrdinalIgnoreCase) == true ? ".wav" : ".mp3");

        long? totalBytes = response.Content.Headers.ContentLength;
        if (totalBytes.HasValue && totalBytes.Value > MaxDownloadBytes)
        {
            throw new IOException($"Файл больше {MaxDownloadBytes / (1024 * 1024)} МБ.");
        }

        string targetPath = Path.Combine(_storage.MusicDirectory, $"{Guid.NewGuid():N}{extension}");
        await using FileStream targetStream = File.Create(targetPath);
        await using Stream sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        var buffer = new byte[81920];
        long copied = 0;
        int read;
        while ((read = await sourceStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            copied += read;
            if (copied > MaxDownloadBytes)
            {
                targetStream.Close();
                File.Delete(targetPath);
                throw new IOException($"Файл больше {MaxDownloadBytes / (1024 * 1024)} МБ.");
            }
            await targetStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);

            if (totalBytes.HasValue && totalBytes.Value > 0)
            {
                progress?.Report(Math.Min(0.95, (double)copied / totalBytes.Value * 0.95));
            }
        }

        return (targetPath, Path.GetFileNameWithoutExtension(urlPath));
    }
}
