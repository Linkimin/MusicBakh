using MusicLibrary.Services.Files;
using System.IO;

namespace MusicLibrary.Tests;

public sealed class FileServiceTests
{
    [Fact]
    public void Exists_ReturnsFalse_WhenPathIsEmpty()
    {
        var service = new FileService();

        bool exists = service.Exists(string.Empty);

        Assert.False(exists);
    }

    [Fact]
    public void Copy_ReportsError_WhenSourceFileDoesNotExist()
    {
        var service = new FileService();
        string target = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp3");

        var result = service.Copy("missing-file.mp3", target, overwrite: true);

        Assert.False(result.IsSuccess);
        Assert.Contains("Файл не найден", result.Message);
    }
}
