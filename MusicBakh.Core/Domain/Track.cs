namespace MusicBakh.Core.Domain;

/// <summary>
/// Описывает музыкальный трек из локальной библиотеки.
/// Модель не зависит от инфраструктуры, чтобы данные можно было использовать в любом слое.
/// </summary>
public sealed class Track
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string Album { get; init; } = string.Empty;
    public string Genre { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public string CoverPath { get; init; } = string.Empty;

    /// <summary>
    /// Оценка пользователя в звёздах, 0..5. 0 означает «не оценено» (UI показывает
    /// все звезды пустыми). Семантика «0 = unrated» важна для фильтра по рейтингу:
    /// «показать с рейтингом ≥ N» при N=0 равно «показать всё».
    /// </summary>
    public int Rating { get; init; }

    /// <summary>
    /// Бинарная реакция «лайк / дизлайк», независимая от Rating. None — не выставлена.
    /// </summary>
    public TrackReaction Reaction { get; init; }

    /// <summary>Год выпуска альбома, опционально (из ID3-тега).</summary>
    public int? Year { get; init; }

    /// <summary>Позиция трека в альбоме (1-based), опционально (из ID3-тега).</summary>
    public int? TrackNumber { get; init; }

    /// <summary>Исполнитель альбома — отличается от Artist для compilations (например, «Various Artists»).</summary>
    public string? AlbumArtist { get; init; }

    /// <summary>
    /// Признак встроенного (seed) трека: такие треки нельзя удалять и менять, они
    /// поставляются вместе с приложением. Пользовательские треки имеют IsBuiltIn=false.
    /// </summary>
    public bool IsBuiltIn { get; init; }

    public string DurationText => PlaybackTime.Format(Duration);
}
