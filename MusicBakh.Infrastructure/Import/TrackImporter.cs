using MusicBakh.Application.Abstractions;
using MusicBakh.Application.Contracts;
using System.IO;
using System.Net.Http;

namespace MusicBakh.Infrastructure.Import;

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

    private readonly IMusicStoragePaths _paths;
    private readonly IMetadataResolver _metadataResolver;
    private readonly ICoverResolver _coverResolver;
    private readonly HttpClient _httpClient;

    public TrackImporter(
        IMusicStoragePaths paths,
        IMetadataResolver metadataResolver,
        ICoverResolver coverResolver,
        HttpClient httpClient)
    {
        _paths = paths;
        _metadataResolver = metadataResolver;
        _coverResolver = coverResolver;
        _httpClient = httpClient;
    }

    public async Task<ImportResult> ImportAsync(ImportRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var createdFiles = new List<string>();
        bool completed = false;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        cancellationToken = deadline.Token;
        try
        {
            string audioPath;
            string filenameHint;

            switch (request)
            {
                case LocalFileImportRequest local:
                    (audioPath, filenameHint) = await CopyLocalAsync(local.FilePath, createdFiles, cancellationToken).ConfigureAwait(false);
                    progress?.Report(0.5);
                    break;
                case UrlImportRequest url:
                    (audioPath, filenameHint) = await DownloadAsync(url.Url, progress, createdFiles, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    return ImportResult.Error("Неизвестный тип источника.");
            }

            ResolvedMetadata metadata = await _metadataResolver.ResolveAsync(audioPath, filenameHint, cancellationToken).ConfigureAwait(false);
            ResolvedCover cover = await _coverResolver.ResolveAsync(metadata, cancellationToken).ConfigureAwait(false);

            string extension = cover.Extension.ToLowerInvariant();
            if (extension is not ("png" or "jpg" or "jpeg" or "webp"))
                throw new IOException("Неподдерживаемый формат обложки.");
            string coverPath = Path.Combine(_paths.CoversDirectory, $"{Guid.NewGuid():N}.{extension}");
            createdFiles.Add(coverPath);
            await File.WriteAllBytesAsync(coverPath, cover.Bytes, cancellationToken).ConfigureAwait(false);

            progress?.Report(1.0);

            var candidate = new TrackImportCandidate
            {
                AudioFilePath = audioPath,
                CoverFilePath = coverPath,
                Title = string.IsNullOrWhiteSpace(metadata.Title) ? filenameHint : metadata.Title,
                Artist = metadata.Artist,
                Album = metadata.Album,
                Genre = metadata.Genre,
                Duration = metadata.Duration,
                Year = metadata.Year,
                TrackNumber = metadata.TrackNumber,
                AlbumArtist = metadata.AlbumArtist
            };
            cancellationToken.ThrowIfCancellationRequested();
            completed = true;
            return ImportResult.Success(candidate);
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
        finally
        {
            if (!completed)
                foreach (string path in createdFiles) TryDelete(path);
        }
    }

    private async Task<(string AudioPath, string FilenameHint)> CopyLocalAsync(string sourcePath, List<string> createdFiles, CancellationToken cancellationToken)
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

        string targetPath = Path.Combine(_paths.MusicDirectory, $"{Guid.NewGuid():N}{extension}");
        createdFiles.Add(targetPath);
        await using FileStream sourceStream = File.OpenRead(sourcePath);
        await using FileStream targetStream = File.Create(targetPath);
        await sourceStream.CopyToAsync(targetStream, cancellationToken).ConfigureAwait(false);

        return (targetPath, Path.GetFileNameWithoutExtension(sourcePath));
    }

    private async Task<(string AudioPath, string FilenameHint)> DownloadAsync(string url, IProgress<double>? progress, List<string> createdFiles, CancellationToken cancellationToken)
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
        bool supportedType = contentType is "audio/mpeg" or "audio/mp3" or "audio/wav" or "audio/x-wav" or "audio/wave";
        if ((!supportedType && contentType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true)
            || (Array.IndexOf(AllowedExtensions, urlExtension) < 0 && !supportedType)
            || contentType is "text/html" or "application/json")
        {
            throw new IOException("Ссылка не указывает на mp3/wav файл.");
        }

        string extension = Array.IndexOf(AllowedExtensions, urlExtension) >= 0
            ? urlExtension
            : (contentType is "audio/wav" or "audio/x-wav" or "audio/wave" ? ".wav" : ".mp3");

        long? totalBytes = response.Content.Headers.ContentLength;
        if (totalBytes.HasValue && totalBytes.Value > MaxDownloadBytes)
        {
            throw new IOException($"Файл больше {MaxDownloadBytes / (1024 * 1024)} МБ.");
        }

        string targetPath = Path.Combine(_paths.MusicDirectory, $"{Guid.NewGuid():N}{extension}");
        createdFiles.Add(targetPath);
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

    public void Discard(TrackImportCandidate candidate)
    {
        DeleteManaged(candidate.AudioFilePath, _paths.MusicDirectory);
        DeleteManaged(candidate.CoverFilePath, _paths.CoversDirectory);
    }

    private static void DeleteManaged(string path, string directory)
    {
        // Удаляем только UUID-файлы, созданные импортёром в его собственном каталоге.
        if (string.IsNullOrWhiteSpace(path)) return;
        string fullPath = Path.GetFullPath(path);
        if (string.Equals(Path.GetDirectoryName(fullPath), Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            && Guid.TryParseExact(Path.GetFileNameWithoutExtension(fullPath), "N", out _))
            TryDelete(fullPath);
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning("Не удалось очистить незавершённый импорт: {0}", ex.Message);
        }
    }
}
