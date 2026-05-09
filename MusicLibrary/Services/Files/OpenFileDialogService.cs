using Microsoft.Win32;

namespace MusicLibrary.Services.Files;

public sealed class OpenFileDialogService : IOpenFileDialogService
{
    public string? PickAudioFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Аудиофайлы (*.mp3;*.wav)|*.mp3;*.wav|MP3-файлы (*.mp3)|*.mp3|WAV-файлы (*.wav)|*.wav",
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
