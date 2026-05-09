using MusicLibrary.Models;

namespace MusicLibrary.Services.Storage;

/// <summary>
/// Хранит пользовательские треки между запусками. Каталог хранилища возвращает каталоги
/// для аудиофайлов и обложек, чтобы импортёр мог в них записывать.
/// </summary>
public interface IUserTrackStorage
{
    string MusicDirectory { get; }
    string CoversDirectory { get; }

    IReadOnlyList<UserTrack> Load();
    void Save(IEnumerable<UserTrack> tracks);
    void Append(UserTrack track);

    /// <summary>
    /// Удаляет запись из JSON и физически удаляет связанные аудиофайл и обложку.
    /// </summary>
    void Delete(int id);
}
