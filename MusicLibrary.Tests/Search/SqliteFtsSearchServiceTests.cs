using Microsoft.EntityFrameworkCore;
using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence;
using MusicBakh.Infrastructure.Persistence.Entities;
using MusicBakh.Infrastructure.Persistence.Repositories;
using MusicBakh.Infrastructure.Search;
using MusicLibrary.Tests.TestSupport;
using Xunit;

namespace MusicLibrary.Tests.Search;

/// <summary>
/// Тесты на FTS5-поиск работают на in-memory SQLite, к которому применены реальные
/// миграции (включая AddTracksFts с виртуальной таблицей и триггерами).
/// </summary>
public sealed class SqliteFtsSearchServiceTests
{
    [Theory]
    [InlineData("A:B")]
    [InlineData("A(B)")]
    public void Search_PunctuationPreservesWordBoundaries(string title)
    {
        using var factory = new MigratedSqliteDbContextFactory();
        new SqliteTrackRepository(factory.CreateContext).Add(new Track { Title = title, FilePath = "1.mp3" });
        Assert.Single(new SqliteFtsSearchService(factory.CreateContext).Search(title));
    }

    [Fact]
    public void Empty_Query_Returns_Empty_List_Without_SQL()
    {
        using var factory = new MigratedSqliteDbContextFactory();
        var service = new SqliteFtsSearchService(factory.CreateContext);

        Assert.Empty(service.Search(string.Empty));
        Assert.Empty(service.Search("   "));
        Assert.Empty(service.Search("\""));
    }

    [Fact]
    public void Search_Finds_By_Title_Exact()
    {
        using var factory = new MigratedSqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);
        repo.Add(new Track { Title = "Bohemian Rhapsody", Artist = "Queen", FilePath = "1.mp3" });

        var service = new SqliteFtsSearchService(factory.CreateContext);
        var hits = service.Search("Bohemian");

        Assert.Single(hits);
        Assert.Equal("Bohemian Rhapsody", hits[0].Title);
    }

    [Fact]
    public void Search_Finds_By_Prefix_Across_Fields()
    {
        using var factory = new MigratedSqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);
        repo.Add(new Track { Title = "Radio Ga Ga", Artist = "Queen",   Album = "The Works",     FilePath = "1.mp3" });
        repo.Add(new Track { Title = "Hotel",       Artist = "Eagles",  Album = "Hotel California", FilePath = "2.mp3" });

        var service = new SqliteFtsSearchService(factory.CreateContext);

        Assert.Single(service.Search("quee"));
        Assert.Single(service.Search("californ"));
    }

    [Fact]
    public void Search_Multiword_Is_Implicit_AND()
    {
        using var factory = new MigratedSqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);
        repo.Add(new Track { Title = "Radio Ga Ga",       Artist = "Queen",       FilePath = "1.mp3" });
        repo.Add(new Track { Title = "Bohemian Rhapsody", Artist = "Queen",       FilePath = "2.mp3" });
        repo.Add(new Track { Title = "Radio",             Artist = "Lana Del Rey", FilePath = "3.mp3" });

        var service = new SqliteFtsSearchService(factory.CreateContext);
        var hits = service.Search("queen radio");

        Assert.Single(hits);
        Assert.Equal("Radio Ga Ga", hits[0].Title);
    }

    [Fact]
    public void Trigger_Tracks_ai_Indexes_New_Inserts()
    {
        using var factory = new MigratedSqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);
        var service = new SqliteFtsSearchService(factory.CreateContext);

        Assert.Empty(service.Search("muse"));

        repo.Add(new Track { Title = "Knights of Cydonia", Artist = "Muse", FilePath = "1.mp3" });

        Assert.Single(service.Search("muse"));
    }

    [Fact]
    public void Trigger_Tracks_ad_Removes_From_Index()
    {
        using var factory = new MigratedSqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);
        var added = repo.Add(new Track { Title = "Time", Artist = "Pink Floyd", FilePath = "1.mp3" });

        var service = new SqliteFtsSearchService(factory.CreateContext);
        Assert.Single(service.Search("Pink"));

        repo.Remove(added.Id);

        Assert.Empty(service.Search("Pink"));
    }

    [Fact]
    public void Trigger_Tracks_au_Updates_Index_When_Indexed_Field_Changes()
    {
        using var factory = new MigratedSqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);
        var added = repo.Add(new Track { Title = "Track One", Artist = "Old Name", FilePath = "1.mp3" });

        var service = new SqliteFtsSearchService(factory.CreateContext);
        Assert.Single(service.Search("Old"));

        using (var ctx = factory.CreateContext())
        {
            var entity = ctx.Tracks.Single(t => t.Id == added.Id);
            entity.Artist = "New Name";
            ctx.SaveChanges();
        }

        Assert.Empty(service.Search("Old"));
        Assert.Single(service.Search("New"));
    }

    [Fact]
    public void Trigger_Tracks_au_Does_Not_Touch_Index_On_NonIndexed_Field_Change()
    {
        // Проверяет AFTER UPDATE OF Title,Artist,Album,Genre: при изменении CoverPath
        // триггер не должен трогать FTS-индекс. Прямой способ — посчитать сумму
        // size-полей в TracksFts_docsize до и после: при перестроении индекса (delete+insert)
        // конкретные rowid'ы в shadow-таблицах удалятся и появятся снова, что меняет
        // total_changes контекста. Используем sqlite_sequence-аналог: считаем счётчик
        // INSERT'ов в FTS через delta total_changes между двумя операциями.
        using var factory = new MigratedSqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);
        var added = repo.Add(new Track { Title = "Song", Artist = "Band", FilePath = "1.mp3", CoverPath = "old-cover.png" });

        long deltaForCoverUpdate;
        using (var ctx = factory.CreateContext())
        {
            var entity = ctx.Tracks.Single(t => t.Id == added.Id);
            entity.CoverPath = "new-cover.png";
            long before = TotalChanges(ctx);
            ctx.SaveChanges();
            deltaForCoverUpdate = TotalChanges(ctx) - before;
        }

        long deltaForArtistUpdate;
        using (var ctx = factory.CreateContext())
        {
            var entity = ctx.Tracks.Single(t => t.Id == added.Id);
            entity.Artist = "Renamed";
            long before = TotalChanges(ctx);
            ctx.SaveChanges();
            deltaForArtistUpdate = TotalChanges(ctx) - before;
        }

        // CoverPath: только 1 update Tracks. Artist: 1 update Tracks + триггер двумя
        // вставками в TracksFts (delete + insert) → дополнительные изменения в shadow-
        // таблицах. Точные числа зависят от внутренностей FTS5, поэтому сравниваем
        // строго: artist-апдейт должен трогать БОЛЬШЕ строк, чем cover-апдейт.
        Assert.True(deltaForArtistUpdate > deltaForCoverUpdate,
            $"Ожидалось, что обновление индексируемой колонки изменит больше строк, чем обновление CoverPath. " +
            $"cover delta={deltaForCoverUpdate}, artist delta={deltaForArtistUpdate}");

        // И финальный sanity-check: поиск по новому Artist находит, по старому — нет.
        var service = new SqliteFtsSearchService(factory.CreateContext);
        Assert.Single(service.Search("Renamed"));
        Assert.Empty(service.Search("Band"));
    }

    private static long TotalChanges(LibraryDbContext ctx)
    {
        // total_changes() возвращает общее число изменённых строк за время жизни
        // connection — включая то, что натворили триггеры.
        var connection = ctx.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT total_changes();";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    [Fact]
    public void Search_Removes_Latin_Diacritics()
    {
        // unicode61 remove_diacritics=2 нормализует латинскую диакритику:
        // "Café" находится по "cafe", "Mötley" по "motley".
        // Для кириллицы это работает на ё→е (один знак), но НЕ на й→и
        // (это отдельная буква, не диакритика). Это известное ограничение
        // SQLite FTS5; полноценная морфология — задача backlog 1.0.3+.
        using var factory = new MigratedSqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);
        repo.Add(new Track { Title = "Kind of Café",   Artist = "Miles",      FilePath = "1.mp3" });
        repo.Add(new Track { Title = "Dr. Feelgood",   Artist = "Mötley Crüe", FilePath = "2.mp3" });

        var service = new SqliteFtsSearchService(factory.CreateContext);

        Assert.Single(service.Search("cafe"));
        Assert.Single(service.Search("motley"));
    }

    [Fact]
    public void Search_Finds_Cyrillic_By_Exact_Match()
    {
        using var factory = new MigratedSqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);
        repo.Add(new Track { Title = "Звезда по имени Солнце", Artist = "Цой", FilePath = "1.mp3" });

        var service = new SqliteFtsSearchService(factory.CreateContext);

        Assert.Single(service.Search("звезда"));
        Assert.Single(service.Search("цой"));
    }

    [Fact]
    public void Trigger_Tracks_ai_Works_For_BuiltIn_Tracks()
    {
        // Намеренно НЕ тестирует backfill (INSERT INTO TracksFts SELECT FROM Tracks из
        // миграции AddTracksFts): на этой фикстуре миграции уже накачены до того, как
        // в БД появились данные, так что backfill отрабатывает на пустом наборе.
        // Проверка реального backfill требует прогона миграций по-этапно (применить до
        // AddTrackAlbum, вставить данные, применить AddTracksFts, проверить) — это
        // вынесено в ручной smoke-test апгрейда с 1.0.1 на 1.0.2 в Task 14.
        //
        // Здесь же проверяем, что Tracks_ai-триггер индексирует built-in треки так же,
        // как пользовательские, — без отдельных условий по IsBuiltIn.
        using var factory = new MigratedSqliteDbContextFactory();

        using (var ctx = factory.CreateContext())
        {
            ctx.Tracks.AddRange(
                new TrackEntity { Title = "Seed One", Artist = "Vendor", FilePath = "s1.mp3", IsBuiltIn = true },
                new TrackEntity { Title = "Seed Two", Artist = "Vendor", FilePath = "s2.mp3", IsBuiltIn = true });
            ctx.SaveChanges();
        }

        var service = new SqliteFtsSearchService(factory.CreateContext);
        var hits = service.Search("vendor");

        Assert.Equal(2, hits.Count);
    }

    [Fact]
    public void Search_Finds_By_Album_Prefix()
    {
        using var factory = new MigratedSqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);
        repo.Add(new Track { Title = "Time", Artist = "Pink Floyd", Album = "The Dark Side of the Moon", FilePath = "1.mp3" });
        repo.Add(new Track { Title = "Money", Artist = "Pink Floyd", Album = "The Dark Side of the Moon", FilePath = "2.mp3" });
        repo.Add(new Track { Title = "Comfortably Numb", Artist = "Pink Floyd", Album = "The Wall", FilePath = "3.mp3" });

        var service = new SqliteFtsSearchService(factory.CreateContext);

        var darkSide = service.Search("dark");
        Assert.Equal(2, darkSide.Count);

        var wall = service.Search("wall");
        Assert.Single(wall);
        Assert.Equal("Comfortably Numb", wall[0].Title);
    }

    [Fact]
    public void Injection_Like_Query_Does_Not_Throw()
    {
        using var factory = new MigratedSqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);
        repo.Add(new Track { Title = "Safe", Artist = "Band", FilePath = "1.mp3" });

        var service = new SqliteFtsSearchService(factory.CreateContext);

        // Не должно бросать; результат может быть пустым или содержать «safe» — главное,
        // что метакомбинации не ломают FTS-парсер.
        var hits = service.Search("safe\" OR (1=1) --");
        Assert.NotNull(hits);
    }
}
