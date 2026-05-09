namespace MusicLibrary.Services.Storage;

/// <summary>
/// Чтение и запись пользовательских настроек плеера.
/// Load никогда не бросает исключений — при любой ошибке возвращает PlayerSettings.Default.
/// </summary>
public interface IPlayerSettingsStorage
{
    PlayerSettings Load();
    void Save(PlayerSettings settings);
}
