namespace MusicLibrary.Models;

/// <summary>
/// Промежуточный объект между импортёром и формой подтверждения. К моменту его
/// создания файл и обложка уже физически лежат в каталогах хранилища, метаданные
/// заполнены каскадом ID3 → MusicBrainz → iTunes. Пользователь может ещё поправить
/// текстовые поля перед окончательным сохранением в библиотеку.
/// </summary>
public sealed class TrackImportCandidate
{
    public required string AudioFilePath { get; set; }
    public required string CoverFilePath { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
}
