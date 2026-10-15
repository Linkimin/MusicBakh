using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence;

/// <summary>
/// Единственный маппинг <see cref="TrackEntity"/> → <see cref="Track"/> для всех путей чтения
/// (треки, история, плейлисты, поиск). Живёт в Infrastructure, чтобы не тянуть EF-сущности в Core.
/// </summary>
internal static class TrackMapper
{
    public static Track ToDomain(TrackEntity e) => new()
    {
        Id = e.Id,
        Title = e.Title,
        Artist = e.Artist,
        Album = e.Album,
        Genre = e.Genre,
        Duration = TimeSpan.FromTicks(e.DurationTicks),
        FilePath = e.FilePath,
        CoverPath = e.CoverPath,
        Rating = e.Rating,
        Reaction = (TrackReaction)e.Reaction,
        Year = e.Year,
        TrackNumber = e.TrackNumber,
        AlbumArtist = e.AlbumArtist,
        // Критично: IsBuiltIn должен дотечь до UI. Иначе, например, history-replay вернёт
        // встроенный трек с IsBuiltIn=false, и кнопка «Удалить» активируется на shipped-файлы.
        IsBuiltIn = e.IsBuiltIn
    };
}
