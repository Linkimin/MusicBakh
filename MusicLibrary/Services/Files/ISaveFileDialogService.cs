namespace MusicLibrary.Services.Files;

public interface ISaveFileDialogService
{
    string? PickSavePath(string suggestedFileName);
}
