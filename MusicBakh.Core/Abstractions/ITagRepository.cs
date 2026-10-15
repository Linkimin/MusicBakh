using MusicBakh.Core.Domain;

namespace MusicBakh.Core.Abstractions;

/// <summary>
/// Хранилище пользовательских тегов и их связей с треками. Attach/Detach идемпотентны
/// (повторный вызов не дублирует строку и не падает на отсутствующей связи).
/// </summary>
public interface ITagRepository
{
    IReadOnlyList<Tag> GetAll();
    Tag? FindById(int id);

    /// <summary>Добавить новый тег. Бросает InvalidOperationException, если имя уже занято (case-insensitive).</summary>
    Tag Add(Tag tag);

    /// <summary>Обновить Name/Color по Id. Если тега нет — silent no-op.</summary>
    void Update(Tag tag);

    /// <summary>Удалить тег. Каскадно убирает связи в TrackTags. Silent no-op если не нашли.</summary>
    void Remove(int id);

    /// <summary>Все теги, привязанные к указанному треку.</summary>
    IReadOnlyList<Tag> GetTagsForTrack(int trackId);

    /// <summary>Снимок всех связей одним запросом; треки без тегов отсутствуют в словаре.</summary>
    IReadOnlyDictionary<int, IReadOnlyList<Tag>> GetTagsByTrack();

    /// <summary>Привязать тег к треку. Идемпотентно: повторный вызов ничего не делает.</summary>
    void AttachTag(int trackId, int tagId);

    /// <summary>Отвязать тег от трека. Идемпотентно: если связи нет, тихо выходит.</summary>
    void DetachTag(int trackId, int tagId);
}
