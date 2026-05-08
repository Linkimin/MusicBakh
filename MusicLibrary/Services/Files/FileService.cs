using MusicLibrary.Models;
using System.IO;

namespace MusicLibrary.Services.Files;

/// <summary>
/// Инкапсулирует работу с файловой системой. ViewModel получает понятный результат,
/// а не низкоуровневые исключения ввода-вывода.
/// </summary>
public sealed class FileService : IFileService
{
    public bool Exists(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }

    public OperationResult Copy(string sourcePath, string targetPath, bool overwrite)
    {
        if (!Exists(sourcePath))
        {
            return OperationResult.Error($"Файл не найден: {sourcePath}");
        }

        try
        {
            File.Copy(sourcePath, targetPath, overwrite);
            return OperationResult.Success("Файл успешно сохранен.");
        }
        catch (UnauthorizedAccessException)
        {
            return OperationResult.Error("Нет доступа к выбранной папке или файлу.");
        }
        catch (IOException exception)
        {
            return OperationResult.Error($"Ошибка копирования файла: {exception.Message}");
        }
    }

    public string GetFileName(string path)
    {
        return Path.GetFileName(path) ?? "track.mp3";
    }
}
