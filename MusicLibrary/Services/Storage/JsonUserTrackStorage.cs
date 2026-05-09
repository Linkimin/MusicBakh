using MusicLibrary.Models;
using System.IO;
using System.Text.Json;

namespace MusicLibrary.Services.Storage;

/// <summary>
/// Реализация хранилища пользовательских треков на основе JSON-файла в %LocalAppData%/MusicLibrary.
/// Каталоги Music и Covers создаются автоматически при первом обращении, поэтому импортёр
/// может писать в них без дополнительных проверок.
/// </summary>
public sealed class JsonUserTrackStorage : IUserTrackStorage
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _rootDirectory;
    private readonly string _jsonPath;

    public JsonUserTrackStorage()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MusicLibrary"))
    {
    }

    public JsonUserTrackStorage(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
        MusicDirectory = Path.Combine(rootDirectory, "Music");
        CoversDirectory = Path.Combine(rootDirectory, "Covers");
        _jsonPath = Path.Combine(rootDirectory, "userTracks.json");

        Directory.CreateDirectory(MusicDirectory);
        Directory.CreateDirectory(CoversDirectory);
    }

    public string MusicDirectory { get; }
    public string CoversDirectory { get; }

    public IReadOnlyList<UserTrack> Load()
    {
        if (!File.Exists(_jsonPath))
        {
            return Array.Empty<UserTrack>();
        }

        try
        {
            using FileStream stream = File.OpenRead(_jsonPath);
            List<UserTrack>? tracks = JsonSerializer.Deserialize<List<UserTrack>>(stream, SerializerOptions);
            return tracks ?? new List<UserTrack>();
        }
        catch (JsonException)
        {
            // Поврежденный файл не должен ронять приложение: возвращаем пустой список,
            // следующий Save перезапишет файл валидным содержимым.
            return Array.Empty<UserTrack>();
        }
    }

    public void Save(IEnumerable<UserTrack> tracks)
    {
        Directory.CreateDirectory(_rootDirectory);
        using FileStream stream = File.Create(_jsonPath);
        JsonSerializer.Serialize(stream, tracks.ToList(), SerializerOptions);
    }

    public void Append(UserTrack track)
    {
        List<UserTrack> existing = Load().ToList();
        existing.Add(track);
        Save(existing);
    }

    public void Delete(int id)
    {
        List<UserTrack> existing = Load().ToList();
        UserTrack? removed = existing.FirstOrDefault(t => t.Id == id);
        if (removed is null)
        {
            return;
        }

        existing.Remove(removed);
        Save(existing);

        TryDeleteFile(removed.FilePath);
        TryDeleteFile(removed.CoverPath);
    }

    private static void TryDeleteFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Файл может быть занят другим процессом — это не критично, JSON-запись уже удалена.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
