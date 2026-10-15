using MusicBakh.Core.Domain;

namespace MusicBakh.Core.Abstractions;

/// <summary>Хранилище статических плейлистов и их треков (ручной порядок через Position).</summary>
public interface IPlaylistRepository
{
    IReadOnlyList<Playlist> GetAll();                            // CreatedAtUtc DESC
    Playlist? GetById(int id);
    Playlist Create(string name);                               // CreatedAtUtc = IClock.UtcNow
    void Rename(int id, string name);
    void Delete(int id);                                        // + каскад PlaylistTracks
    IReadOnlyList<Track> GetTracks(int playlistId);             // Position ASC
    int GetTrackCount(int playlistId);
    void AddTrack(int playlistId, int trackId);                // Position = max+1
    void AddTracks(int playlistId, IReadOnlyList<int> trackIds); // батч, сохраняя порядок
    void RemoveAt(int playlistId, int position);               // удалить + сжать позиции (транзакция)
    void Reorder(int playlistId, int fromPosition, int toPosition); // транзакция
}
