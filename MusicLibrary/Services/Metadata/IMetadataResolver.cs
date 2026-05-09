namespace MusicLibrary.Services.Metadata;

/// <summary>
/// Точка входа в каскад источников метаданных. Принимает путь к файлу и подсказку
/// (обычно имя исходного файла), возвращает максимально полно заполненный набор полей.
/// </summary>
public interface IMetadataResolver
{
    Task<ResolvedMetadata> ResolveAsync(string filePath, string? filenameHint, CancellationToken cancellationToken);
}
