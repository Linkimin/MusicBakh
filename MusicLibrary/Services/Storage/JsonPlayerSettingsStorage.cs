using MusicLibrary.Models;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicLibrary.Services.Storage;

/// <summary>
/// JSON-хранилище настроек плеера.
/// Файл лежит в %LocalAppData%\MusicBakh\player-settings.json.
/// При повреждении / отсутствии возвращает PlayerSettings.Default.
/// </summary>
public sealed class JsonPlayerSettingsStorage : IPlayerSettingsStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;

    public JsonPlayerSettingsStorage(string filePath)
    {
        _filePath = filePath;
    }

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MusicBakh",
        "player-settings.json");

    public PlayerSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return PlayerSettings.Default;
            }

            string json = File.ReadAllText(_filePath);
            PlayerSettingsDto? dto = JsonSerializer.Deserialize<PlayerSettingsDto>(json, Options);
            if (dto is null)
            {
                return PlayerSettings.Default;
            }

            // Ограничиваем громкость допустимым диапазоном — иначе MediaPlayer бросит ArgumentOutOfRange.
            double volume = Math.Clamp(dto.Volume, 0.0, 1.0);
            return new PlayerSettings(volume, dto.IsMuted, dto.RepeatMode);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // Повреждённый файл / нет прав чтения — стартуем с дефолтов, не падаем.
            return PlayerSettings.Default;
        }
    }

    public void Save(PlayerSettings settings)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var dto = new PlayerSettingsDto(settings.Volume, settings.IsMuted, settings.RepeatMode);
            string json = JsonSerializer.Serialize(dto, Options);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Диск переполнен / нет прав — игнорируем, чтобы не уронить приложение из-за настроек.
        }
    }

    private sealed record PlayerSettingsDto(double Volume, bool IsMuted, RepeatMode RepeatMode);
}
