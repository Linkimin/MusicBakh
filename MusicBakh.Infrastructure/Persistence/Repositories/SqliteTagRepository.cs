using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence.Repositories;

/// <summary>
/// Репозиторий тегов поверх LibraryDbContext. Attach/Detach намеренно идемпотентны
/// (UI часто шлёт повторные вызовы при drag-drop / повторном клике), уникальность
/// имени обеспечивается на уровне схемы COLLATE NOCASE.
/// </summary>
public sealed class SqliteTagRepository : ITagRepository
{
    private readonly Func<LibraryDbContext> _contextFactory;

    public SqliteTagRepository(Func<LibraryDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public IReadOnlyList<Tag> GetAll()
    {
        using var ctx = _contextFactory();
        return ctx.Tags.OrderBy(t => t.Name).Select(MapToDomain).ToList();
    }

    public Tag? FindById(int id)
    {
        using var ctx = _contextFactory();
        var entity = ctx.Tags.FirstOrDefault(t => t.Id == id);
        return entity is null ? null : MapToDomain(entity);
    }

    public Tag Add(Tag tag)
    {
        using var ctx = _contextFactory();
        var normalized = tag.Name.Trim();

        // Уникальность имени проверяем в коде через StringComparison.OrdinalIgnoreCase,
        // потому что SQLite COLLATE NOCASE — ASCII-only и пропустил бы «Любимое» vs
        // «ЛЮБИМОЕ» как разные. .NET-овский OrdinalIgnoreCase корректно работает на
        // Unicode. SQLite-индекс COLLATE NOCASE остаётся как defence-in-depth для
        // одновременных вставок ASCII-дублей.
        if (ctx.Tags.AsEnumerable().Any(t => string.Equals(t.Name, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Тег с именем «{tag.Name}» уже существует.");
        }

        var entity = new TagEntity { Name = normalized, Color = tag.Color };
        ctx.Tags.Add(entity);
        try
        {
            ctx.SaveChanges();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException sx && sx.SqliteErrorCode == 19)
        {
            // Гонка: проверка прошла, но второй поток успел вставить тот же ASCII-дубль.
            throw new InvalidOperationException($"Тег с именем «{tag.Name}» уже существует.", ex);
        }

        return MapToDomain(entity);
    }

    public void Update(Tag tag)
    {
        using var ctx = _contextFactory();
        var entity = ctx.Tags.FirstOrDefault(t => t.Id == tag.Id);
        if (entity is null)
        {
            return;
        }

        var normalized = tag.Name.Trim();

        // Та же логика, что в Add, но исключаем сам обновляемый тег по Id.
        if (ctx.Tags.AsEnumerable().Any(t => t.Id != tag.Id &&
                                              string.Equals(t.Name, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Тег с именем «{tag.Name}» уже существует.");
        }

        entity.Name = normalized;
        entity.Color = tag.Color;
        try
        {
            ctx.SaveChanges();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException sx && sx.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException($"Тег с именем «{tag.Name}» уже существует.", ex);
        }
    }

    public void Remove(int id)
    {
        using var ctx = _contextFactory();
        var entity = ctx.Tags.FirstOrDefault(t => t.Id == id);
        if (entity is null)
        {
            return;
        }
        ctx.Tags.Remove(entity);
        ctx.SaveChanges();
    }

    public IReadOnlyList<Tag> GetTagsForTrack(int trackId)
    {
        using var ctx = _contextFactory();
        return ctx.TrackTags
            .Where(tt => tt.TrackId == trackId)
            .Select(tt => tt.Tag)
            .OrderBy(t => t.Name)
            .Select(t => MapToDomain(t))
            .ToList();
    }

    public void AttachTag(int trackId, int tagId)
    {
        using var ctx = _contextFactory();
        // INSERT OR IGNORE — идемпотентно: повторный вызов не дублирует и не падает.
        // Заодно защищает от нарушения FK, если переданы несуществующие Id'ы:
        // SqliteException 787 (FOREIGN KEY constraint failed) пробросится.
        ctx.Database.ExecuteSqlInterpolated($@"
            INSERT OR IGNORE INTO TrackTags (TrackId, TagId) VALUES ({trackId}, {tagId})");
    }

    public IReadOnlyDictionary<int, IReadOnlyList<Tag>> GetTagsByTrack()
    {
        using var ctx = _contextFactory();
        return ctx.TrackTags.AsNoTracking()
            .OrderBy(tt => tt.Tag.Name)
            .Select(tt => new { tt.TrackId, tt.Tag.Id, tt.Tag.Name, tt.Tag.Color })
            .ToList()
            .GroupBy(tt => tt.TrackId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Tag>)g.Select(t =>
                new Tag { Id = t.Id, Name = t.Name, Color = t.Color }).ToList());
    }

    public void DetachTag(int trackId, int tagId)
    {
        using var ctx = _contextFactory();
        // DELETE — silent no-op если связи нет.
        ctx.Database.ExecuteSqlInterpolated($@"
            DELETE FROM TrackTags WHERE TrackId = {trackId} AND TagId = {tagId}");
    }

    private static Tag MapToDomain(TagEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Color = e.Color
    };
}
