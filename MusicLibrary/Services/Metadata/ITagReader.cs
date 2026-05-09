namespace MusicLibrary.Services.Metadata;

/// <summary>
/// Читает ID3-теги (или их аналоги для wav) из локального аудиофайла.
/// Реализация не должна бросать исключения — при битых файлах возвращает пустой LocalTagInfo.
/// </summary>
public interface ITagReader
{
    LocalTagInfo Read(string filePath);
}
