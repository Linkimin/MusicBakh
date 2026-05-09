using MusicLibrary.Models;
using MusicLibrary.Services.Storage;

namespace MusicLibrary.Services.Tracks;

/// <summary>
/// Объединяет захардкоженные «эталонные» треки и пользовательские из JSON-хранилища.
/// Захардкоженные всегда идут первыми и сохраняют свои Id (1..N). Пользовательские
/// сохраняют свой Id, если он не пересекается; иначе получают новый, начиная с N+1.
/// Это нужно потому, что в первой версии приложения Id не были стабильными при
/// удалении треков, и теоретически в JSON могли остаться старые значения.
/// </summary>
public sealed class CompositeTrackRepository : ITrackRepository
{
    private readonly ITrackRepository _builtIn;
    private readonly IUserTrackStorage _storage;

    public CompositeTrackRepository(ITrackRepository builtIn, IUserTrackStorage storage)
    {
        _builtIn = builtIn;
        _storage = storage;
    }

    public IReadOnlyList<Track> GetTracks()
    {
        IReadOnlyList<Track> builtInTracks = _builtIn.GetTracks();
        int nextId = builtInTracks.Count == 0 ? 1 : builtInTracks.Max(t => t.Id) + 1;

        var taken = new HashSet<int>(builtInTracks.Select(t => t.Id));
        var result = new List<Track>(builtInTracks);

        foreach (UserTrack user in _storage.Load())
        {
            int id = taken.Contains(user.Id) ? nextId++ : user.Id;
            taken.Add(id);

            result.Add(new Track
            {
                Id = id,
                Title = user.Title,
                Artist = user.Artist,
                Genre = user.Genre,
                Duration = TimeSpan.FromSeconds(user.DurationSeconds),
                FilePath = user.FilePath,
                CoverPath = user.CoverPath
            });

            if (id >= nextId)
            {
                nextId = id + 1;
            }
        }

        return result;
    }
}
