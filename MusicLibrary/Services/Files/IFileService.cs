using MusicLibrary.Models;

namespace MusicLibrary.Services.Files;

public interface IFileService
{
    bool Exists(string path);
    OperationResult Copy(string sourcePath, string targetPath, bool overwrite);
    string GetFileName(string path);
}
