using Microsoft.Win32;

namespace MusicLibrary.Services.Files;

/// <summary>
/// Обертка над стандартным диалогом сохранения. Это сохраняет MVVM:
/// ViewModel просит путь, но не создает WPF-диалог напрямую.
/// </summary>
public sealed class SaveFileDialogService : ISaveFileDialogService
{
    public string? PickSavePath(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            Filter = "Аудиофайлы (*.mp3;*.wav)|*.mp3;*.wav|MP3-файлы (*.mp3)|*.mp3|WAV-файлы (*.wav)|*.wav|Все файлы (*.*)|*.*",
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
