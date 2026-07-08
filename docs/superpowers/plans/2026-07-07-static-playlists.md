# Static Playlists Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Реализовать спек [`docs/superpowers/specs/2026-07-07-static-playlists-design.md`](../specs/2026-07-07-static-playlists-design.md) — четвёртый вид «Плейлисты» со статическими (ручными) плейлистами: CRUD, добавление через контекст-меню + drag-and-drop, reorder, воспроизведение.

**Architecture:** Плейлист-логика выносится в отдельный под-VM `PlaylistsViewModel` (первый шаг разгруза `MainViewModel`, см. [ADR-0002](../../adr/0002-extract-playlists-viewmodel.md)): под-VM владеет состоянием + CRUD + членством, `MainViewModel` остаётся владельцем навигации и очереди воспроизведения (playback-команды плейлиста физически в `MainViewModel`, данные берут из под-VM). Синхронизация открытой детали при мутации — через событие `PlaylistsViewModel.PlaylistContentChanged`, на которое `MainViewModel` подписывается один раз. Хранение — таблицы `Playlists` + `PlaylistTracks(Id, PlaylistId, TrackId, Position)` через EF Core, дубликаты разрешены, каскад по FK. UI переиспускает механику вкладок + drill-down итерации D (`MainViewMode`, `LeftColumnState`, `LeftColumnTemplateSelector`, `ContentControl`).

> **Отложено (не в скоупе E):** интерфейс `IPlaybackQueueController`. Он вводится только когда под-VM реально понадобится самому инициировать playback; в E воспроизведение плейлиста живёт в `MainViewModel` (там же очередь и навигация), поэтому шов без потребителя не создаётся (YAGNI — см. ADR-0002, раздел Consequences).

**Tech Stack:** .NET 10, WPF, C# 14, EF Core 10 SQLite, xUnit. Никаких новых NuGet-зависимостей.

**TDD adaptation:** Репозиторий, под-VM, DnD-helper — классический red→green (pure/тестируемая логика). XAML (шаблоны, контекст-меню, DnD-взаимодействие) — цикл «изменение → build → headless render-smoke → визуальная проверка → commit»; для XAML-задач приёмка — чистый build + прохождение render-smoke, визуальный smoke выполняется в Task 10.

## Global Constraints

- **Комментарии в коде и XAML — на русском.** Имена идентификаторов — английские.
- `TreatWarningsAsErrors=true` в `MusicBakh.Core`/`Application`/`Infrastructure`; сборка этих проектов должна быть **без предупреждений**. `MusicLibrary` (WPF) не форсит warnings-as-errors, но целимся в 0.
- EF-миграции: стартовый проект — `MusicBakh.Infrastructure`. После `dotnet ef migrations add` в сгенерированном `.cs` заменить `using Microsoft.EntityFrameworkCore.Migrations;` → добавить `using EFMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;` и базовый класс `: Migration` → `: EFMigration` (иначе коллизия namespace `MusicBakh.Infrastructure.Persistence.Migrations` с типом `Migration`).
- Репозитории получают `Func<LibraryDbContext>` и открывают короткий контекст на каждый вызов (см. `SqliteTrackRepository`).
- Маппинг `TrackEntity → Track` обязан пробрасывать ВСЕ поля, включая `IsBuiltIn`, `Year`, `TrackNumber`, `AlbumArtist`, `Rating`, `Reaction` (контрактный инвариант).
- `{StaticResource}` конвертеров, используемых в DataTemplate merged-словаря, объявлять **локально в этом словаре** (не в `App.xaml` после MergedDictionaries — не резолвится, крэшит на рендере).
- Команды внутри `ContextMenu` достаются через `PlacementTarget.Tag` (`RelativeSource AncestorType=Window` внутри ContextMenu не работает).
- Стартовый вид приложения — всегда «Треки» (persist active_view не читается при старте — решение 1.0.4).
- Базлайн перед началом: `main` на v1.0.4, **253 теста** зелёные, build 0 ошибок.

---

## Task 1: Абстракции — `Playlist`, `IPlaylistRepository`

Чистые доменные типы и интерфейсы. Компилируется, реализаций нет. (`PlaylistView` — UI-DTO в Presentation, создаётся в Task 4.)

**Files:**
- Create: `MusicBakh.Core/Domain/Playlist.cs`
- Modify: `MusicBakh.Core/Domain/MainViewMode.cs`
- Create: `MusicBakh.Core/Abstractions/IPlaylistRepository.cs`

**Interfaces:**
- Produces: `Playlist(int Id, string Name, DateTime CreatedAtUtc)`; `MainViewMode.Playlists = 3`; `IPlaylistRepository` (сигнатуры ниже).

- [ ] **Step 1: `Playlist` domain record**

`MusicBakh.Core/Domain/Playlist.cs`:
```csharp
namespace MusicBakh.Core.Domain;

/// <summary>Статический плейлист: имя + время создания. Треки — через IPlaylistRepository.</summary>
public sealed record Playlist(int Id, string Name, DateTime CreatedAtUtc);
```

- [ ] **Step 2: `MainViewMode.Playlists`**

В `MusicBakh.Core/Domain/MainViewMode.cs` добавить значение после `Artists = 2`:
```csharp
    Artists = 2,
    Playlists = 3
```

- [ ] **Step 3: `IPlaylistRepository`**

`MusicBakh.Core/Abstractions/IPlaylistRepository.cs`:
```csharp
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
```

- [ ] **Step 4: Build**

Run: `dotnet build MusicLibrary.sln --nologo`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 5: Commit**

```bash
git add MusicBakh.Core
git commit -m "feat(core): playlist domain + IPlaylistRepository abstraction"
```

---

## Task 2: EF-сущности, конфигурации, DbSets, миграция `AddPlaylists`

**Files:**
- Create: `MusicBakh.Infrastructure/Persistence/Entities/PlaylistEntity.cs`
- Create: `MusicBakh.Infrastructure/Persistence/Entities/PlaylistTrackEntity.cs`
- Create: `MusicBakh.Infrastructure/Persistence/Configurations/PlaylistEntityConfiguration.cs`
- Create: `MusicBakh.Infrastructure/Persistence/Configurations/PlaylistTrackEntityConfiguration.cs`
- Modify: `MusicBakh.Infrastructure/Persistence/LibraryDbContext.cs`
- Create (генерируется): `MusicBakh.Infrastructure/Persistence/Migrations/<ts>_AddPlaylists.cs`

**Interfaces:**
- Consumes: ничего из Task 1 напрямую (сущности внутренние).
- Produces: `LibraryDbContext.Playlists`, `LibraryDbContext.PlaylistTracks` (internal DbSet); таблицы `Playlists`, `PlaylistTracks`.

- [ ] **Step 1: `PlaylistEntity`**

`MusicBakh.Infrastructure/Persistence/Entities/PlaylistEntity.cs`:
```csharp
namespace MusicBakh.Infrastructure.Persistence.Entities;

internal sealed class PlaylistEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
```

- [ ] **Step 2: `PlaylistTrackEntity`**

`MusicBakh.Infrastructure/Persistence/Entities/PlaylistTrackEntity.cs`:
```csharp
namespace MusicBakh.Infrastructure.Persistence.Entities;

/// <summary>Связь плейлист↔трек. Собственный Id (PK) — дубликаты трека в плейлисте разрешены.</summary>
internal sealed class PlaylistTrackEntity
{
    public int Id { get; set; }
    public int PlaylistId { get; set; }
    public int TrackId { get; set; }
    public int Position { get; set; }   // 0-based ручной порядок

    public PlaylistEntity Playlist { get; set; } = default!;
    public TrackEntity Track { get; set; } = default!;
}
```

- [ ] **Step 3: Конфигурации** (зеркалят стиль `TrackTagEntityConfiguration`)

`PlaylistEntityConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence.Configurations;

internal sealed class PlaylistEntityConfiguration : IEntityTypeConfiguration<PlaylistEntity>
{
    public void Configure(EntityTypeBuilder<PlaylistEntity> b)
    {
        b.ToTable("Playlists");
        b.HasKey(p => p.Id);
        b.Property(p => p.Name).IsRequired().HasMaxLength(200);
        b.HasIndex(p => p.CreatedAtUtc);
    }
}
```

`PlaylistTrackEntityConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence.Configurations;

internal sealed class PlaylistTrackEntityConfiguration : IEntityTypeConfiguration<PlaylistTrackEntity>
{
    public void Configure(EntityTypeBuilder<PlaylistTrackEntity> b)
    {
        b.ToTable("PlaylistTracks");
        b.HasKey(pt => pt.Id);
        b.HasIndex(pt => new { pt.PlaylistId, pt.Position });

        b.HasOne(pt => pt.Playlist)
            .WithMany()
            .HasForeignKey(pt => pt.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(pt => pt.Track)
            .WithMany()
            .HasForeignKey(pt => pt.TrackId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 4: DbSets + OnModelCreating**

В `LibraryDbContext.cs` добавить после строки с `TrackTags`:
```csharp
    internal DbSet<PlaylistEntity> Playlists => Set<PlaylistEntity>();
    internal DbSet<PlaylistTrackEntity> PlaylistTracks => Set<PlaylistTrackEntity>();
```
И в `OnModelCreating` после `TrackTagEntityConfiguration`:
```csharp
        modelBuilder.ApplyConfiguration(new PlaylistEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PlaylistTrackEntityConfiguration());
```

- [ ] **Step 5: Генерировать миграцию**

Run: `dotnet ef migrations add AddPlaylists -p MusicBakh.Infrastructure -s MusicBakh.Infrastructure`
Затем в сгенерированном `<ts>_AddPlaylists.cs` применить alias-фикс (Global Constraints): добавить `using EFMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;` и заменить `: Migration` → `: EFMigration`.
Проверить, что `Up()` создаёт таблицы `Playlists` и `PlaylistTracks` с FK cascade и индексами; лишних колонок нет.

- [ ] **Step 6: Build**

Run: `dotnet build MusicLibrary.sln --nologo`
Expected: 0 warnings, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add MusicBakh.Infrastructure
git commit -m "feat(infra): Playlists + PlaylistTracks EF entities + AddPlaylists migration"
```

---

## Task 3: `SqlitePlaylistRepository` (TDD) + тесты + DI

**Files:**
- Create: `MusicBakh.Infrastructure/Persistence/Repositories/SqlitePlaylistRepository.cs`
- Create: `MusicLibrary.Tests/Persistence/SqlitePlaylistRepositoryTests.cs`
- Modify: `MusicBakh.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`

**Interfaces:**
- Consumes: `IPlaylistRepository` (Task 1), `IClock`, `Func<LibraryDbContext>`, `LibraryDbContext.Playlists/PlaylistTracks` (Task 2).
- Produces: `SqlitePlaylistRepository(Func<LibraryDbContext>, IClock)` реализует `IPlaylistRepository`.

- [ ] **Step 1: Failing-тесты** (используют `MigratedSqliteDbContextFactory` — миграции нужны для FK; смотри существующие `SqliteTrackRepositoryTests` на предмет фикстуры)

`MusicLibrary.Tests/Persistence/SqlitePlaylistRepositoryTests.cs`:
```csharp
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence.Entities;
using MusicBakh.Infrastructure.Persistence.Repositories;
using MusicLibrary.Tests.TestSupport;
using Xunit;

namespace MusicLibrary.Tests.Persistence;

public sealed class SqlitePlaylistRepositoryTests
{
    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    }

    private static int SeedTrack(MigratedSqliteDbContextFactory f, string title)
    {
        using var ctx = f.CreateContext();
        var e = new TrackEntity { Title = title, Artist = "A", FilePath = title + ".mp3" };
        ctx.Tracks.Add(e);
        ctx.SaveChanges();
        return e.Id;
    }

    [Fact]
    public void Create_And_GetAll_Orders_By_CreatedAt_Desc()
    {
        using var f = new MigratedSqliteDbContextFactory();
        var clock = new FixedClock();
        var repo = new SqlitePlaylistRepository(f.CreateContext, clock);

        var p1 = repo.Create("First");
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        var p2 = repo.Create("Second");

        var all = repo.GetAll();
        Assert.Equal(new[] { "Second", "First" }, all.Select(p => p.Name).ToArray());
        Assert.True(p1.Id > 0 && p2.Id > 0);
    }

    [Fact]
    public void AddTracks_Preserves_Order_And_Allows_Duplicates()
    {
        using var f = new MigratedSqliteDbContextFactory();
        var repo = new SqlitePlaylistRepository(f.CreateContext, new FixedClock());
        int a = SeedTrack(f, "a"), b = SeedTrack(f, "b");
        var p = repo.Create("P");

        repo.AddTracks(p.Id, new[] { a, b, a });   // a добавлен дважды

        var tracks = repo.GetTracks(p.Id);
        Assert.Equal(new[] { a, b, a }, tracks.Select(t => t.Id).ToArray());
        Assert.Equal(3, repo.GetTrackCount(p.Id));
    }

    [Fact]
    public void RemoveAt_Compacts_Positions()
    {
        using var f = new MigratedSqliteDbContextFactory();
        var repo = new SqlitePlaylistRepository(f.CreateContext, new FixedClock());
        int a = SeedTrack(f, "a"), b = SeedTrack(f, "b"), c = SeedTrack(f, "c");
        var p = repo.Create("P");
        repo.AddTracks(p.Id, new[] { a, b, c });

        repo.RemoveAt(p.Id, 1);   // убираем b

        Assert.Equal(new[] { a, c }, repo.GetTracks(p.Id).Select(t => t.Id).ToArray());
    }

    [Fact]
    public void Reorder_Moves_Track_And_Renumbers()
    {
        using var f = new MigratedSqliteDbContextFactory();
        var repo = new SqlitePlaylistRepository(f.CreateContext, new FixedClock());
        int a = SeedTrack(f, "a"), b = SeedTrack(f, "b"), c = SeedTrack(f, "c");
        var p = repo.Create("P");
        repo.AddTracks(p.Id, new[] { a, b, c });

        repo.Reorder(p.Id, fromPosition: 0, toPosition: 2);   // a в конец

        Assert.Equal(new[] { b, c, a }, repo.GetTracks(p.Id).Select(t => t.Id).ToArray());
    }

    [Fact]
    public void Deleting_Track_From_Library_Cascades_Out_Of_Playlist()
    {
        using var f = new MigratedSqliteDbContextFactory();
        var repo = new SqlitePlaylistRepository(f.CreateContext, new FixedClock());
        int a = SeedTrack(f, "a"), b = SeedTrack(f, "b");
        var p = repo.Create("P");
        repo.AddTracks(p.Id, new[] { a, b });

        using (var ctx = f.CreateContext())
        {
            ctx.Tracks.Remove(ctx.Tracks.First(t => t.Id == a));
            ctx.SaveChanges();
        }

        Assert.Equal(new[] { b }, repo.GetTracks(p.Id).Select(t => t.Id).ToArray());
    }

    [Fact]
    public void GetTracks_Maps_All_Track_Fields()
    {
        using var f = new MigratedSqliteDbContextFactory();
        var repo = new SqlitePlaylistRepository(f.CreateContext, new FixedClock());
        int id;
        using (var ctx = f.CreateContext())
        {
            var e = new TrackEntity { Title = "T", Artist = "Ar", Album = "Al", FilePath = "t.mp3",
                Year = 2014, TrackNumber = 3, AlbumArtist = "AA", Rating = 4, Reaction = 1, IsBuiltIn = true };
            ctx.Tracks.Add(e); ctx.SaveChanges(); id = e.Id;
        }
        var p = repo.Create("P");
        repo.AddTrack(p.Id, id);

        var t = repo.GetTracks(p.Id).Single();
        Assert.Equal(2014, t.Year);
        Assert.Equal(3, t.TrackNumber);
        Assert.Equal("AA", t.AlbumArtist);
        Assert.Equal(4, t.Rating);
        Assert.True(t.IsBuiltIn);
    }
}
```

- [ ] **Step 2: Run — FAIL** (нет типа `SqlitePlaylistRepository`)

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "SqlitePlaylistRepositoryTests" --nologo`
Expected: build error / fail.

- [ ] **Step 3: Реализация**

`MusicBakh.Infrastructure/Persistence/Repositories/SqlitePlaylistRepository.cs`:
```csharp
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence.Repositories;

/// <summary>Плейлисты поверх LibraryDbContext. Порядок треков — через Position; изменения позиций атомарны.</summary>
public sealed class SqlitePlaylistRepository : IPlaylistRepository
{
    private readonly Func<LibraryDbContext> _contextFactory;
    private readonly IClock _clock;

    public SqlitePlaylistRepository(Func<LibraryDbContext> contextFactory, IClock clock)
    {
        _contextFactory = contextFactory;
        _clock = clock;
    }

    public IReadOnlyList<Playlist> GetAll()
    {
        using var ctx = _contextFactory();
        return ctx.Playlists
            .OrderByDescending(p => p.CreatedAtUtc).ThenByDescending(p => p.Id)
            .Select(p => new Playlist(p.Id, p.Name, p.CreatedAtUtc))
            .ToList();
    }

    public Playlist? GetById(int id)
    {
        using var ctx = _contextFactory();
        var e = ctx.Playlists.FirstOrDefault(p => p.Id == id);
        return e is null ? null : new Playlist(e.Id, e.Name, e.CreatedAtUtc);
    }

    public Playlist Create(string name)
    {
        using var ctx = _contextFactory();
        var e = new PlaylistEntity { Name = name, CreatedAtUtc = _clock.UtcNow };
        ctx.Playlists.Add(e);
        ctx.SaveChanges();
        return new Playlist(e.Id, e.Name, e.CreatedAtUtc);
    }

    public void Rename(int id, string name)
    {
        using var ctx = _contextFactory();
        var e = ctx.Playlists.FirstOrDefault(p => p.Id == id);
        if (e is null) return;
        e.Name = name;
        ctx.SaveChanges();
    }

    public void Delete(int id)
    {
        using var ctx = _contextFactory();
        var e = ctx.Playlists.FirstOrDefault(p => p.Id == id);
        if (e is null) return;
        ctx.Playlists.Remove(e);   // PlaylistTracks уходят по cascade
        ctx.SaveChanges();
    }

    public IReadOnlyList<Track> GetTracks(int playlistId)
    {
        using var ctx = _contextFactory();
        return ctx.PlaylistTracks
            .Where(pt => pt.PlaylistId == playlistId)
            .OrderBy(pt => pt.Position)
            .Select(pt => pt.Track)
            .Select(MapToDomain)
            .ToList();
    }

    public int GetTrackCount(int playlistId)
    {
        using var ctx = _contextFactory();
        return ctx.PlaylistTracks.Count(pt => pt.PlaylistId == playlistId);
    }

    public void AddTrack(int playlistId, int trackId) => AddTracks(playlistId, new[] { trackId });

    public void AddTracks(int playlistId, IReadOnlyList<int> trackIds)
    {
        if (trackIds.Count == 0) return;
        using var ctx = _contextFactory();
        int next = ctx.PlaylistTracks.Where(pt => pt.PlaylistId == playlistId)
            .Select(pt => (int?)pt.Position).Max() is int max ? max + 1 : 0;
        foreach (var id in trackIds)
        {
            ctx.PlaylistTracks.Add(new PlaylistTrackEntity { PlaylistId = playlistId, TrackId = id, Position = next++ });
        }
        ctx.SaveChanges();
    }

    public void RemoveAt(int playlistId, int position)
    {
        using var ctx = _contextFactory();
        using var tx = ctx.Database.BeginTransaction();
        var rows = ctx.PlaylistTracks.Where(pt => pt.PlaylistId == playlistId)
            .OrderBy(pt => pt.Position).ToList();
        if (position < 0 || position >= rows.Count) return;
        ctx.PlaylistTracks.Remove(rows[position]);
        for (int i = position + 1; i < rows.Count; i++) rows[i].Position = i - 1;
        ctx.SaveChanges();
        tx.Commit();
    }

    public void Reorder(int playlistId, int fromPosition, int toPosition)
    {
        using var ctx = _contextFactory();
        using var tx = ctx.Database.BeginTransaction();
        var rows = ctx.PlaylistTracks.Where(pt => pt.PlaylistId == playlistId)
            .OrderBy(pt => pt.Position).ToList();
        if (fromPosition < 0 || fromPosition >= rows.Count || toPosition < 0 || toPosition >= rows.Count) return;
        var moved = rows[fromPosition];
        rows.RemoveAt(fromPosition);
        rows.Insert(toPosition, moved);
        for (int i = 0; i < rows.Count; i++) rows[i].Position = i;
        ctx.SaveChanges();
        tx.Commit();
    }

    private static Track MapToDomain(TrackEntity e) => new()
    {
        Id = e.Id, Title = e.Title, Artist = e.Artist, Album = e.Album, Genre = e.Genre,
        Duration = TimeSpan.FromTicks(e.DurationTicks), FilePath = e.FilePath, CoverPath = e.CoverPath,
        Rating = e.Rating, Reaction = (TrackReaction)e.Reaction,
        Year = e.Year, TrackNumber = e.TrackNumber, AlbumArtist = e.AlbumArtist, IsBuiltIn = e.IsBuiltIn
    };
}
```

- [ ] **Step 4: DI-регистрация**

В `InfrastructureServiceCollectionExtensions.cs` после `services.AddSingleton<ITagRepository, SqliteTagRepository>();` добавить:
```csharp
        services.AddSingleton<IPlaylistRepository, SqlitePlaylistRepository>();
```

- [ ] **Step 5: Run — PASS**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "SqlitePlaylistRepositoryTests" --nologo`
Expected: 6 passed.

- [ ] **Step 6: Full suite + build**

Run: `dotnet build MusicLibrary.sln --nologo && dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category!=Benchmark" --nologo`
Expected: 0/0, 259 passed (253 + 6).

- [ ] **Step 7: Commit**

```bash
git add MusicBakh.Infrastructure MusicLibrary.Tests
git commit -m "feat(infra): SqlitePlaylistRepository with position ordering + transactional reorder/remove"
```

---

## Task 4: `PlaylistsViewModel` + `MainViewModel` интеграция (границы, нав, playback)

Устанавливает границу ADR-0002. `PlaylistsViewModel` владеет плейлист-состоянием, CRUD и членством; `MainViewModel` экспонирует под-VM, добавляет вид/навигацию/воспроизведение плейлиста и подписывается на событие `PlaylistContentChanged` для ре-резолва открытой детали.

**Files:**
- Create: `MusicLibrary/ViewModels/PlaylistsViewModel.cs`
- Create: `MusicLibrary/Services/Library/PlaylistView.cs`
- Modify: `MusicLibrary/Services/Library/LeftColumnState.cs`
- Modify: `MusicLibrary/Selectors/LeftColumnTemplateSelector.cs`
- Modify: `MusicLibrary/ViewModels/MainViewModel.cs`
- Modify: `MusicLibrary/DependencyInjection/PresentationServiceCollectionExtensions.cs`
- Create: `MusicLibrary.Tests/ViewModels/PlaylistsViewModelTests.cs`

**Interfaces:**
- Consumes: `IPlaylistRepository`, `IConfirmationService`, `IClock` (Task 1/3).
- Produces: `PlaylistView(Playlist, IReadOnlyList<Track>)` с `TotalDuration`; `LeftColumnState.PlaylistsRoot`, `LeftColumnState.PlaylistDetail(PlaylistView)`; `PlaylistsViewModel` (свойства/команды/событие `PlaylistContentChanged` ниже); `MainViewModel.Playlists` (под-VM), `MainViewModel.OpenPlaylistCommand`, `PlayPlaylistCommand`, `ShufflePlaylistCommand`, `PlayPlaylistTrackCommand`.

- [ ] **Step 1: `PlaylistView` DTO**

`MusicLibrary/Services/Library/PlaylistView.cs`:
```csharp
using MusicBakh.Core.Domain;

namespace MusicLibrary.Services.Library;

/// <summary>UI-DTO открытого плейлиста: сам плейлист + его треки (в порядке Position).</summary>
public sealed record PlaylistView(Playlist Playlist, IReadOnlyList<Track> Tracks)
{
    public System.TimeSpan TotalDuration
    {
        get { var s = System.TimeSpan.Zero; foreach (var t in Tracks) s += t.Duration; return s; }
    }
}
```

- [ ] **Step 2: `LeftColumnState` варианты**

В `LeftColumnState.cs` добавить в тело abstract record:
```csharp
    public sealed record PlaylistsRoot : LeftColumnState;
    public sealed record PlaylistDetail(PlaylistView Playlist) : LeftColumnState;
```
(нужен `using MusicLibrary.Services.Library;` уже есть — тот же namespace).

- [ ] **Step 3: Selector — новые шаблоны**

В `LeftColumnTemplateSelector.cs` добавить свойства и case'ы:
```csharp
    public DataTemplate? PlaylistsTemplate { get; set; }
    public DataTemplate? PlaylistDetailTemplate { get; set; }
```
В `switch`:
```csharp
        LeftColumnState.PlaylistsRoot   => PlaylistsTemplate,
        LeftColumnState.PlaylistDetail  => PlaylistDetailTemplate,
```

- [ ] **Step 4: `PlaylistsViewModel` failing-тесты**

`MusicLibrary.Tests/ViewModels/PlaylistsViewModelTests.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicLibrary.ViewModels;
using Xunit;

namespace MusicLibrary.Tests.ViewModels;

public sealed class PlaylistsViewModelTests
{
    private sealed class FakeClock : IClock { public System.DateTime UtcNow { get; set; } = new(2026,1,1,0,0,0,System.DateTimeKind.Utc); }
    private sealed class YesConfirm : IConfirmationService { public bool Confirm(string t, string m) => true; }

    // Минимальный in-memory IPlaylistRepository для VM-тестов.
    private sealed class FakePlaylistRepo : IPlaylistRepository
    {
        private readonly List<Playlist> _pl = new();
        private readonly Dictionary<int, List<int>> _tracks = new();
        private int _next = 1;
        public IReadOnlyList<Playlist> GetAll() => _pl.OrderByDescending(p => p.Id).ToList();
        public Playlist? GetById(int id) => _pl.FirstOrDefault(p => p.Id == id);
        public Playlist Create(string name) { var p = new Playlist(_next++, name, System.DateTime.UtcNow); _pl.Add(p); _tracks[p.Id] = new(); return p; }
        public void Rename(int id, string name) { var i = _pl.FindIndex(p => p.Id == id); if (i>=0) _pl[i] = _pl[i] with { Name = name }; }
        public void Delete(int id) { _pl.RemoveAll(p => p.Id == id); _tracks.Remove(id); }
        public IReadOnlyList<Track> GetTracks(int id) => _tracks[id].Select(tid => new Track { Id = tid, Title = "t"+tid, FilePath = tid+".mp3" }).ToList();
        public int GetTrackCount(int id) => _tracks[id].Count;
        public void AddTrack(int id, int tid) => _tracks[id].Add(tid);
        public void AddTracks(int id, IReadOnlyList<int> ids) => _tracks[id].AddRange(ids);
        public void RemoveAt(int id, int pos) => _tracks[id].RemoveAt(pos);
        public void Reorder(int id, int from, int to) { var l=_tracks[id]; var x=l[from]; l.RemoveAt(from); l.Insert(to,x); }
    }

    private static PlaylistsViewModel Vm(FakePlaylistRepo repo) => new(repo, new YesConfirm(), new FakeClock());

    [Fact]
    public void CreatePlaylist_Adds_To_List()
    {
        var repo = new FakePlaylistRepo();
        var vm = Vm(repo);
        vm.ConfirmNewPlaylistName("Мой микс");
        Assert.Contains(vm.Playlists, p => p.Name == "Мой микс");
    }

    [Fact]
    public void Empty_Name_Falls_Back_To_Default()
    {
        var repo = new FakePlaylistRepo();
        var vm = Vm(repo);
        vm.ConfirmNewPlaylistName("   ");
        Assert.Contains(vm.Playlists, p => p.Name == "Новый плейлист");
    }

    [Fact]
    public void AddContextTarget_Adds_Captured_Tracks_To_Playlist()
    {
        var repo = new FakePlaylistRepo();
        var vm = Vm(repo);
        var p = repo.Create("P"); vm.RefreshPlaylists();
        vm.SetContextTarget(new[] { new Track { Id = 7, FilePath = "7.mp3" }, new Track { Id = 8, FilePath = "8.mp3" } });
        vm.AddContextTargetToPlaylist(p);
        Assert.Equal(new[] { 7, 8 }, repo.GetTracks(p.Id).Select(t => t.Id).ToArray());
    }
}
```
(Публичный контракт VM, который надо реализовать: `Playlists`, `RefreshPlaylists()`, `ConfirmNewPlaylistName(string)`, `SetContextTarget(IReadOnlyList<Track>)`, `AddContextTargetToPlaylist(Playlist)`, `AddTracksByIds(int, IReadOnlyList<int>)`, `RemoveTrackAt(int,int)`, `Reorder(int,int,int)`, `BuildView(int)`, событие `PlaylistContentChanged`. Команды — обёртки над этими методами.)
> Осторожно с `FakePlaylistRepo`: его `RemoveAt`/`Reorder` бросят на выходе за границы, тогда как реальный `SqlitePlaylistRepository` тихо возвращается (guard). Если добавляешь VM-тест на некорректную позицию — либо повтори guard в фейке, либо тестируй этот кейс на реальном репозитории (Task 3), а не через фейк.

- [ ] **Step 5: Run — FAIL**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "PlaylistsViewModelTests" --nologo`
Expected: build error.

- [ ] **Step 6: Реализовать `PlaylistsViewModel`**

`MusicLibrary/ViewModels/PlaylistsViewModel.cs`:
```csharp
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicLibrary.Commands;
using MusicLibrary.Services.Library;

namespace MusicLibrary.ViewModels;

/// <summary>
/// Под-VM плейлистов (первый шаг разгруза MainViewModel — ADR-0002). Владеет списком
/// плейлистов, инлайн-edit состоянием, CRUD и мутацией членства. Навигацию/воспроизведение
/// (общие для всех вкладок) выполняет MainViewModel.
/// </summary>
public sealed class PlaylistsViewModel : ViewModelBase
{
    private const string DefaultName = "Новый плейлист";

    private readonly IPlaylistRepository _repo;
    private readonly IConfirmationService _confirm;
    private readonly IClock _clock;

    private IReadOnlyList<Track> _contextTarget = System.Array.Empty<Track>();

    public PlaylistsViewModel(IPlaylistRepository repo, IConfirmationService confirm, IClock clock)
    {
        _repo = repo;
        _confirm = confirm;
        _clock = clock;
        Playlists = new ObservableCollection<Playlist>(_repo.GetAll());

        StartNewPlaylistCommand = new RelayCommand(_ => IsCreating = true);
        AddContextTargetToPlaylistCommand = new RelayCommand(p => { if (p is Playlist pl) AddContextTargetToPlaylist(pl); });
        DeletePlaylistCommand = new RelayCommand(p => { if (p is Playlist pl) DeletePlaylist(pl); });
    }

    public ObservableCollection<Playlist> Playlists { get; }

    private bool _isCreating;
    public bool IsCreating { get => _isCreating; set => SetProperty(ref _isCreating, value); }

    public ICommand StartNewPlaylistCommand { get; }
    public ICommand AddContextTargetToPlaylistCommand { get; }
    public ICommand DeletePlaylistCommand { get; }

    public void RefreshPlaylists()
    {
        Playlists.Clear();
        foreach (var p in _repo.GetAll()) Playlists.Add(p);
    }

    public void ConfirmNewPlaylistName(string? name)
    {
        var final = string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim();
        _repo.Create(final);
        IsCreating = false;
        RefreshPlaylists();
    }

    public void RenamePlaylist(Playlist p, string? name)
    {
        var final = string.IsNullOrWhiteSpace(name) ? p.Name : name.Trim();
        _repo.Rename(p.Id, final);
        RefreshPlaylists();
    }

    public void DeletePlaylist(Playlist p)
    {
        if (!_confirm.Confirm("Удалить плейлист", $"Удалить плейлист «{p.Name}»? Треки в библиотеке останутся."))
            return;
        _repo.Delete(p.Id);
        RefreshPlaylists();
    }

    public void SetContextTarget(IReadOnlyList<Track> tracks) => _contextTarget = tracks ?? System.Array.Empty<Track>();

    public void AddContextTargetToPlaylist(Playlist p)
    {
        if (_contextTarget.Count == 0) return;
        _repo.AddTracks(p.Id, _contextTarget.Select(t => t.Id).ToList());
        RefreshPlaylists();
        PlaylistContentChanged?.Invoke(p.Id);
    }

    public void AddTracksByIds(int playlistId, IReadOnlyList<int> ids)
    {
        if (ids.Count == 0) return;
        _repo.AddTracks(playlistId, ids);
        RefreshPlaylists();
        PlaylistContentChanged?.Invoke(playlistId);
    }

    public void RemoveTrackAt(int playlistId, int position)
    {
        _repo.RemoveAt(playlistId, position);
        RefreshPlaylists();
        PlaylistContentChanged?.Invoke(playlistId);
    }

    public void Reorder(int playlistId, int from, int to)
    {
        _repo.Reorder(playlistId, from, to);
        RefreshPlaylists();
        PlaylistContentChanged?.Invoke(playlistId);
    }

    public PlaylistView BuildView(int playlistId)
    {
        var pl = _repo.GetById(playlistId)!;
        return new PlaylistView(pl, _repo.GetTracks(playlistId));
    }

    /// <summary>
    /// Поднимается после любой мутации содержимого плейлиста (add/remove/reorder) с его Id.
    /// MainViewModel подписывается один раз в конструкторе и, если открыта деталь именно этого
    /// плейлиста, пересобирает её. Событие вместо мутабельного poll-флага — надёжнее: нельзя
    /// «забыть» вызвать ре-резолв в новой команде.
    /// </summary>
    public event System.Action<int>? PlaylistContentChanged;
}
```
> `AddTracksByIds` здесь же (нужен для DnD-drop в Task 8) — чтобы вся мутация членства и подъём события жили в под-VM, а не размазывались по `MainViewModel`.

- [ ] **Step 7: `MainViewModel` — под-VM, навигация, воспроизведение**

В `MainViewModel.cs` (класс НЕ реализует `IPlaybackQueueController` — его в E нет; playback-команды плейлиста в самом `MainViewModel` вызывают существующий приватный `ReplaceQueueAndPlay` напрямую, сигнатуру не меняя):
1. В конструктор добавить параметр `PlaylistsViewModel? playlists = null` (в конец списка опциональных); сохранить в поле `_playlists` и экспонировать `public PlaylistsViewModel Playlists => _playlists!;`. Если `null` (старые тесты) — команды плейлистов no-op-guard'ятся. Если не `null` — подписаться на событие (см. п.5).
2. `SwitchViewCommand`: добавить в `switch` ветку `MainViewMode.Playlists => new LeftColumnState.PlaylistsRoot(),`.
3. Новые команды (регистрировать в конструкторе рядом с `OpenAlbumCommand`):
```csharp
OpenPlaylistCommand = new RelayCommand(p =>
{
    if (p is not Playlist pl || _playlists is null) return;
    _navStack.Push(_currentLeftColumn);
    CurrentLeftColumn = new LeftColumnState.PlaylistDetail(_playlists.BuildView(pl.Id));
    OnPropertyChanged(nameof(CanGoBack));
});
PlayPlaylistCommand = new RelayCommand(p =>
{
    if (p is not PlaylistView v || v.Tracks.Count == 0) return;
    ReplaceQueueAndPlay(v.Tracks, shuffle: false);
});
ShufflePlaylistCommand = new RelayCommand(p =>
{
    if (p is not PlaylistView v || v.Tracks.Count == 0) return;
    ReplaceQueueAndPlay(v.Tracks, shuffle: true);
});
PlayPlaylistTrackCommand = new RelayCommand(p =>
{
    if (p is not Track track || _currentLeftColumn is not LeftColumnState.PlaylistDetail d) return;
    ReplaceQueueAndPlay(d.Playlist.Tracks, shuffle: false, selectedTrack: track);
});
```
С полями-свойствами `public ICommand OpenPlaylistCommand { get; }` и т.д.
4. **Ре-резолв открытой детали — через подписку на событие (один раз в конструкторе):**
```csharp
// в конструкторе, если _playlists is not null:
_playlists.PlaylistContentChanged += OnPlaylistContentChanged;
```
```csharp
// обработчик: если открыта деталь именно этого плейлиста — пересобрать её
private void OnPlaylistContentChanged(int playlistId)
{
    if (_currentLeftColumn is LeftColumnState.PlaylistDetail d && d.Playlist.Playlist.Id == playlistId)
        CurrentLeftColumn = new LeftColumnState.PlaylistDetail(_playlists!.BuildView(playlistId));
}
```
В `Dispose()` отписаться: `if (_playlists is not null) _playlists.PlaylistContentChanged -= OnPlaylistContentChanged;`.
Это заменяет ручной poll-флаг: команды add/remove/reorder (Task 7/8) НЕ должны сами звать ре-резолв — под-VM поднимает событие, `MainViewModel` реагирует автоматически.

- [ ] **Step 8: DI**

В `PresentationServiceCollectionExtensions.cs`:
- Зарегистрировать под-VM: `services.AddSingleton<PlaylistsViewModel>();` (singleton — состояние плейлистов живёт всю сессию, как и `MainViewModel`).
- `MainViewModel` резолвится (проверь текущую регистрацию — `AddSingleton<MainViewModel>()` или ручная сборка в `MainWindow`). Прокинуть `PlaylistsViewModel` в конструктор `MainViewModel` (через DI — параметр опциональный, DI подставит зарегистрированный singleton). Если `MainWindow` собирает VM вручную — добавить аргумент там.
- Никакой регистрации `IPlaybackQueueController` — интерфейс в E не вводится (см. заметку в шапке плана / ADR-0002).

- [ ] **Step 9: Run VM-тесты — PASS**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "PlaylistsViewModelTests" --nologo`
Expected: 3 passed.

- [ ] **Step 10: Full suite + build**

Run: `dotnet build MusicLibrary.sln --nologo && dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category!=Benchmark" --nologo`
Expected: 0/0, 262 passed (259 + 3). Существующие `MainViewModelTests`/`MainViewModelNavigationTests` — если конструктор изменился, обновить их вызовы (добавить `null` для нового опционального параметра или передать фейковый под-VM). Не удалять покрытие.

- [ ] **Step 11: Commit**

```bash
git add MusicLibrary MusicLibrary.Tests
git commit -m "feat(playlists): PlaylistsViewModel sub-VM + MainViewModel nav/playback (ADR-0002)"
```

---

## Task 5: DnD базовая механика (спайк) — helper + тесты + изолированный smoke

**Только базовая механика**: drag-инициация, drop-таргет, вычисление позиции reorder — в переиспользуемом helper'е. Без CRUD/контекст-меню/мульти-выбора (те — Task 8).

**Files:**
- Create: `MusicLibrary/Behaviors/DragDropListHelper.cs`
- Create: `MusicLibrary/Behaviors/ReorderMath.cs`
- Create: `MusicLibrary.Tests/Behaviors/ReorderMathTests.cs`

**Interfaces:**
- Produces: `ReorderMath.TargetIndex(double dropY, IReadOnlyList<double> rowTops, IReadOnlyList<double> rowHeights)` → int (позиция вставки); `DragDropListHelper` — attached properties для инициации drag и приёма drop (используется в Task 8).

- [ ] **Step 1: `ReorderMath` failing-тест** (чистая функция — вычисление целевого индекса по Y-координате дропа относительно строк)

`MusicLibrary.Tests/Behaviors/ReorderMathTests.cs`:
```csharp
using System.Collections.Generic;
using MusicLibrary.Behaviors;
using Xunit;

namespace MusicLibrary.Tests.Behaviors;

public sealed class ReorderMathTests
{
    // 3 строки высотой 20, tops = 0,20,40.
    private static readonly IReadOnlyList<double> Tops = new[] { 0.0, 20.0, 40.0 };
    private static readonly IReadOnlyList<double> Heights = new[] { 20.0, 20.0, 20.0 };

    [Theory]
    [InlineData(2, 0)]    // над первой строкой (верхняя половина row0) → вставка перед 0
    [InlineData(12, 1)]   // нижняя половина row0 → вставка перед 1
    [InlineData(48, 2)]   // нижняя половина row2 → вставка в конец (индекс 2, т.к. clamp к Count-1 при reorder)
    public void TargetIndex_Maps_Y_To_Insertion_Position(double y, int expected)
    {
        Assert.Equal(expected, ReorderMath.TargetIndex(y, Tops, Heights));
    }

    // AdjustForRemoval: сырой индекс вставки (относительно ДО удаления source) → toPosition для Reorder,
    // который удаляет source ПЕРЕД вставкой. При движении вниз всё между source и точкой сдвигается на 1.
    [Theory]
    [InlineData(0, 2, 1)]   // A(0) роняем «между B и C» (raw=2) → ожидаем B,A,C → to=1
    [InlineData(0, 1, 0)]   // A(0) роняем на место B (raw=1) → to=0 (никуда не двинулся визуально)
    [InlineData(2, 0, 0)]   // C(2) роняем в начало (raw=0) → движение вверх, поправки нет → to=0
    [InlineData(1, 2, 1)]   // B(1) роняем «между B и C» (raw=2) → to=1 (стоит на месте)
    public void AdjustForRemoval_Compensates_Downward_Shift(int from, int raw, int expected)
    {
        Assert.Equal(expected, ReorderMath.AdjustForRemoval(raw, from));
    }
}
```
> Именно этот off-by-one невидим для build/unit прочих слоёв и проявился бы только визуально при DnD-reorder вниз — поэтому вынесен в чистую функцию с тестом, а не оставлен инлайн-логикой в behaviour (Task 8).

- [ ] **Step 2: Run — FAIL**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "ReorderMathTests" --nologo`
Expected: build error.

- [ ] **Step 3: `ReorderMath`**

`MusicLibrary/Behaviors/ReorderMath.cs`:
```csharp
using System.Collections.Generic;

namespace MusicLibrary.Behaviors;

/// <summary>Чистая геометрия reorder: по Y-координате дропа возвращает индекс вставки в список строк.</summary>
public static class ReorderMath
{
    /// <summary>
    /// rowTops/rowHeights — верхние границы и высоты строк (в координатах контейнера). Возвращает
    /// индекс строки, ПЕРЕД которой ставится перетаскиваемый элемент (верхняя половина строки → перед ней,
    /// нижняя половина → перед следующей). Клампится в [0, Count-1] для случая reorder внутри одного списка.
    /// </summary>
    public static int TargetIndex(double dropY, IReadOnlyList<double> rowTops, IReadOnlyList<double> rowHeights)
    {
        int count = rowTops.Count;
        if (count == 0) return 0;
        for (int i = 0; i < count; i++)
        {
            double mid = rowTops[i] + rowHeights[i] / 2.0;
            if (dropY < mid) return i;
        }
        return count - 1;
    }

    /// <summary>
    /// Преобразует «сырой» индекс вставки (посчитанный <see cref="TargetIndex"/> относительно списка
    /// ДО удаления перетаскиваемого элемента) в <c>toPosition</c> для <c>IPlaylistRepository.Reorder</c>,
    /// который сначала удаляет элемент из <paramref name="fromPosition"/>, а потом вставляет. При движении
    /// вниз (<paramref name="rawTarget"/> &gt; <paramref name="fromPosition"/>) удаление source сдвигает
    /// все последующие на одну позицию — поэтому вычитаем 1. Движение вверх коррекции не требует.
    /// </summary>
    public static int AdjustForRemoval(int rawTarget, int fromPosition)
        => rawTarget > fromPosition ? rawTarget - 1 : rawTarget;
}
```

- [ ] **Step 4: `DragDropListHelper`** (attached-поведение; используется шаблонами в Task 8)

`MusicLibrary/Behaviors/DragDropListHelper.cs`:
```csharp
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MusicLibrary.Behaviors;

/// <summary>
/// Базовая DnD-механика для списков: инициация drag по порогу движения и приём drop.
/// Поведение навешивается в XAML (Task 8); здесь — общая инфраструктура, чтобы add и reorder
/// не дублировали код. Формат DataObject: строковый формат "musicbakh-tracks" со списком int Id.
/// </summary>
public static class DragDropListHelper
{
    public const string TrackIdsFormat = "musicbakh-track-ids";

    private static Point _start;
    private static bool _armed;

    /// <summary>Вызвать из PreviewMouseLeftButtonDown строки-источника: запоминает точку.</summary>
    public static void ArmDrag(MouseButtonEventArgs e, IInputElement reference)
    {
        _start = e.GetPosition(reference);
        _armed = true;
    }

    /// <summary>Вызвать из PreviewMouseMove: если превышен порог — вернуть true (пора начинать DoDragDrop).</summary>
    public static bool ShouldBeginDrag(MouseEventArgs e, IInputElement reference)
    {
        if (!_armed || e.LeftButton != MouseButtonState.Pressed) return false;
        var p = e.GetPosition(reference);
        return Math.Abs(p.X - _start.X) >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(p.Y - _start.Y) >= SystemParameters.MinimumVerticalDragDistance;
    }

    public static void Disarm() => _armed = false;

    /// <summary>Собирает DataObject из id треков.</summary>
    public static DataObject MakeData(int[] trackIds)
    {
        var data = new DataObject();
        data.SetData(TrackIdsFormat, trackIds);
        return data;
    }

    public static bool TryGetTrackIds(IDataObject data, out int[] ids)
    {
        if (data.GetDataPresent(TrackIdsFormat) && data.GetData(TrackIdsFormat) is int[] arr)
        { ids = arr; return true; }
        ids = Array.Empty<int>();
        return false;
    }
}
```

- [ ] **Step 5: Run — PASS**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "ReorderMathTests" --nologo`
Expected: 7 passed (3 `TargetIndex` + 4 `AdjustForRemoval` кейса).

- [ ] **Step 6: Unit-покрытие DataObject round-trip** (чистая часть helper'а)

Добавить в `ReorderMathTests.cs` (или отдельный `DragDropListHelperTests.cs`):
```csharp
[Fact]
public void DataObject_RoundTrips_Track_Ids()
{
    var data = MusicLibrary.Behaviors.DragDropListHelper.MakeData(new[] { 3, 7, 9 });
    Assert.True(MusicLibrary.Behaviors.DragDropListHelper.TryGetTrackIds(data, out var ids));
    Assert.Equal(new[] { 3, 7, 9 }, ids);
}
```
> **Честно про покрытие:** юнит-тестами покрыты только чистые части — `ReorderMath` (геометрия + `AdjustForRemoval`) и `MakeData`/`TryGetTrackIds` (сериализация). Мышиный порог `ArmDrag`/`ShouldBeginDrag` завязан на живой `MouseEventArgs`/`InputDevice`, который headless не сконструировать корректно, поэтому он проверяется **только визуальным smoke** (Task 10), а не автотестом. Это осознанный предел, а не «покрыто».

- [ ] **Step 7: Build + суите**

Run: `dotnet build MusicLibrary.sln --nologo && dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category!=Benchmark" --nologo`
Expected: 0/0, ~270 passed (262 + 8: 7 ReorderMath + 1 round-trip). Точное число — иллюстративно, важно что все зелёные.

- [ ] **Step 8: Commit**

```bash
git add MusicLibrary MusicLibrary.Tests
git commit -m "feat(dnd): reusable drag-init + drop helper + reorder math (spike, tested in isolation)"
```

---

## Task 6: XAML — `PlaylistsTemplates.xaml`, 4-я вкладка, Ctrl+4, регистрация

**Files:**
- Create: `MusicLibrary/Resources/PlaylistsTemplates.xaml`
- Modify: `MusicLibrary/App.xaml`
- Modify: `MusicLibrary/MainWindow.xaml`
- Modify: `MusicLibrary/MainWindow.xaml.cs` (Esc-back уже общий; проверить, что работает для PlaylistDetail)

**Interfaces:**
- Consumes: `MainViewModel.Playlists` (под-VM), `OpenPlaylistCommand`, `PlayPlaylistCommand`, `ShufflePlaylistCommand`, `PlayPlaylistTrackCommand`, `SwitchViewCommand` (Task 4); `CurrentLeftColumn` states `PlaylistsRoot`/`PlaylistDetail` (Task 4); `TrackIdentityMatchConverter`, `CountToVisibilityConverter` (существуют).
- Produces: DataTemplate'ы `PlaylistsRootTemplate`, `PlaylistDetailTemplate`; selector-свойства подключены; вкладка «Плейлисты».

- [ ] **Step 1: `PlaylistsTemplates.xaml`** — зеркалит структуру `AlbumsArtistsTemplates.xaml` (тот же файл — образец; открыть его и следовать паттерну). Ключевые требования:
  - Корень словаря: `xmlns:converters="clr-namespace:MusicLibrary.Converters"` и **локальные** объявления `<converters:CountToVisibilityConverter x:Key="CountToVisibilityConverter"/>` и `<converters:TrackIdentityMatchConverter x:Key="TrackIdentityMatchConverter"/>` (Global Constraint — иначе крэш на рендере).
  - `PlaylistRowTemplate` — строка плейлиста: имя (`{Binding Name}`), «N треков» (если хранишь счётчик — иначе только имя; счётчик можно опустить в MVP-строке или вычислять в под-VM), `Cursor=Hand`, `Tag="{Binding DataContext, RelativeSource={RelativeSource AncestorType=Window}}"`. Левый клик (`MouseBinding LeftClick`) → `OpenPlaylistCommand` через `AncestorType=Window`, `CommandParameter={Binding}` (сам `Playlist`). `ContextMenu` — в Task 7.
  - `PlaylistsRootTemplate` — `ItemsControl ItemsSource="{Binding DataContext.Playlists.Playlists, RelativeSource={RelativeSource AncestorType=Window}}"` со `StackPanel` в `ItemsPanel`; сверху `Button Content="+ Новый плейлист"` → `DataContext.Playlists.StartNewPlaylistCommand`; empty-state «Плейлистов пока нет — создайте первый» через `CountToVisibilityConverter(Invert)` по `Playlists.Playlists.Count`. Инлайн-edit строки создания — см. Step 2.
  - `PlaylistDetailTemplate` — зеркало `AlbumDetailTemplate`: «← Все плейлисты» → `BackCommand`; header (`{Binding Playlist.Playlist.Name}`, `Playlist.Tracks.Count` треков, `Playlist.TotalDuration`); кнопки «▶ Воспроизвести»/«🔀 Перемешать» → `PlayPlaylistCommand`/`ShufflePlaylistCommand` с `CommandParameter="{Binding Playlist}"` (это `PlaylistView`); `ListBox ItemsSource="{Binding Playlist.Tracks}"` с `ItemContainerStyle="{StaticResource TrackListBoxItemStyle}"`, строки `№/Название/Длительность`, подсветка играющего (тот же `DataTrigger` + `MultiBinding TrackIdentityMatchConverter` + `PlayingTrack` через `AncestorType=Window`, как в AlbumDetail), двойной клик → `PlayPlaylistTrackCommand` (`AncestorType=Window`, `CommandParameter={Binding}`); empty-state «В плейлисте пока нет треков — добавьте их через правый клик или перетаскиванием».
    - **ОБЯЗАТЕЛЬНО** на этом `ListBox`: `AlternationCount="{Binding Playlist.Tracks.Count}"` — иначе `AlternationIndex`, по которому Task 7 удаляет строку из плейлиста, будет 0 у всех строк (дефолт `AlternationCount=0`) либо циклиться, и «Убрать из плейлиста» удалит не тот трек при дубликатах. Номер трека в строке тоже удобно рендерить из `(ItemsControl.AlternationIndex)+1`.

  **Полный XAML** сгенерировать по образцу `AlbumsArtistsTemplates.xaml` с указанными привязками. (Приёмка — build + render-smoke Task 9; визуал — Task 10.)

- [ ] **Step 2: Инлайн-создание/переименование**

В `PlaylistsRootTemplate` строка создания при `Playlists.IsCreating=true`: `TextBox` в фокусе, преднаполнен «Новый плейлист», `Enter` → `ConfirmNewPlaylistName(text)`, `Esc` → отмена (`IsCreating=false`). Реализуется через `TextBox` с `KeyDown`-обработчиком в code-behind (или attached-behaviour) — по образцу того, как в проекте уже сделан ввод (проверь `TagsWindow`/поиск на предмет паттерна инлайн-ввода). Переименование — тот же приём по контекст-меню (Task 7).

- [ ] **Step 3: Регистрация словаря в `App.xaml`**

После `<ResourceDictionary Source="Resources/AlbumsArtistsTemplates.xaml" />` (или где регистрируются шаблонные словари) добавить:
```xml
<ResourceDictionary Source="Resources/PlaylistsTemplates.xaml" />
```

- [ ] **Step 4: 4-я вкладка + selector-свойства в `MainWindow.xaml`**

- В ряд табов добавить кнопку «Плейлисты» (зеркало существующих табов, `CommandParameter` → `{x:Static domain:MainViewMode.Playlists}`, подсветка активного по `ActiveView`).
- В `ContentControl` с `LeftColumnTemplateSelector` добавить свойства `PlaylistsTemplate="{StaticResource PlaylistsRootTemplate}"` и `PlaylistDetailTemplate="{StaticResource PlaylistDetailTemplate}"`.
- В `Window.InputBindings` добавить `Ctrl+D4`:
```xml
<KeyBinding Key="D4" Modifiers="Control" Command="{Binding SwitchViewCommand}">
    <KeyBinding.CommandParameter>
        <x:Static Member="domain:MainViewMode.Playlists" />
    </KeyBinding.CommandParameter>
</KeyBinding>
```

- [ ] **Step 5: Build + render Playlists**

Run: `dotnet build MusicLibrary.sln --nologo`
Expected: 0 errors.
Затем headless render-probe (как в итерации D): временно в `App.OnStartup` после `mainWindow.Show()` — `DispatcherUnhandledException`-хендлер в файл + `if (mainWindow.DataContext is MainViewModel vm) { vm.SwitchViewCommand.Execute(MainViewMode.Playlists); if (vm.Playlists.Playlists.Count>0) vm.OpenPlaylistCommand.Execute(vm.Playlists.Playlists[0]); }`. `dotnet run` 15с, проверить отсутствие crash-файла, откатить probe (`git checkout MusicLibrary/App.xaml.cs`). (В Task 9 это станет постоянным тестом.)

- [ ] **Step 6: Commit**

```bash
git add MusicLibrary
git commit -m "feat(ui): Playlists tab + PlaylistsRoot/PlaylistDetail templates + Ctrl+4"
```

---

## Task 7: Контекст-меню (первое в приложении)

**Files:**
- Create: `MusicLibrary/Behaviors/ContextSelection.cs` (чистый выбор треков)
- Create: `MusicLibrary/Behaviors/TrackContextMenuBehavior.cs` (attached-захват контекст-таргета)
- Create: `MusicLibrary.Tests/Behaviors/ContextSelectionTests.cs`
- Modify: `MusicLibrary/Resources/TrackTemplates.xaml` (ContextMenu на карточке трека)
- Modify: `MusicLibrary/Resources/AlbumsArtistsTemplates.xaml` (ContextMenu на строках треков в детали)
- Modify: `MusicLibrary/Resources/PlaylistsTemplates.xaml` (ContextMenu на строке плейлиста + треке плейлиста)
- Modify: `MusicLibrary/ViewModels/MainViewModel.cs` (команды `AddContextToPlaylistCommand`, `RemovePlaylistTrackCommand`)

**Interfaces:**
- Consumes: `PlaylistsViewModel.Playlists`, `SetContextTarget`, `AddContextTargetToPlaylist`, `RemoveTrackAt`, `DeletePlaylistCommand`, `StartNewPlaylistCommand` (Task 4); `MainViewModel.Playlists`; событие `PlaylistContentChanged` (авто ре-резолв — Task 4).
- Produces: `ContextSelection.Resolve` (чистый); `TrackContextMenuBehavior.Attach` (attached); контекст-меню «Добавить в плейлист ▸» на всех трек-строках; «Убрать из плейлиста» в PlaylistDetail; «Переименовать»/«Удалить» на строке плейлиста.

- [ ] **Step 1: Команды-мосты в `MainViewModel`** (add + remove; ре-резолв — автоматически через событие из Task 4)

Add/Remove делегируют под-VM; ре-резолв открытой детали НЕ вызывается вручную — под-VM поднимает `PlaylistContentChanged`, обработчик из Task 4 сам пересобирает деталь.
```csharp
// Добавить зафиксированный контекст-таргет в выбранный плейлист. p — целевой Playlist (из подменю).
AddContextToPlaylistCommand = new RelayCommand(p =>
{
    if (_playlists is null || p is not Playlist pl) return;
    _playlists.AddContextTargetToPlaylist(pl);   // событие → авто ре-резолв
});
// Убрать строку из открытого плейлиста ПО ПОЗИЦИИ (не по Id — иначе при дубликатах удалит не ту).
RemovePlaylistTrackCommand = new RelayCommand(p =>
{
    if (_playlists is null || _currentLeftColumn is not LeftColumnState.PlaylistDetail d) return;
    if (p is not int position) return;   // int из AlternationIndex через IntFromAlternationConverter, см. Step 3
    _playlists.RemoveTrackAt(d.Playlist.Playlist.Id, position);
});
```
Фиксацию «на каком треке открыто меню» делает не команда, а attached-behaviour из Step 2 (он зовёт `_playlists.SetContextTarget(...)` при открытии меню). Поэтому отдельного `SetContextTrackCommand` нет.

- [ ] **Step 2: Захват контекст-таргета — конкретный `TrackContextMenuBehavior` + чистый `ContextSelection`**

Механизм фиксируется здесь однозначно (не «решим в моменте»). Две части:

**(а) Чистый тестируемый выбор** — `MusicLibrary/Behaviors/ContextSelection.cs`:
```csharp
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MusicBakh.Core.Domain;

namespace MusicLibrary.Behaviors;

/// <summary>Определяет набор треков для операции по правому клику: если кликнутая строка входит
/// в мульти-выделение (&gt;1) — берём всё выделение; иначе — только кликнутый трек.</summary>
public static class ContextSelection
{
    public static IReadOnlyList<Track> Resolve(IList? selectedItems, Track clicked)
    {
        if (selectedItems is null) return new[] { clicked };
        var tracks = selectedItems.OfType<Track>().ToList();
        return tracks.Count > 1 && tracks.Contains(clicked) ? tracks : new List<Track> { clicked };
    }
}
```
Тест `ContextSelectionTests` (3 кейса): null-выделение → [clicked]; кликнут вне выделения → [clicked]; кликнут внутри мульти → всё выделение.

**(б) Attached-behaviour, навешивающий захват** — `MusicLibrary/Behaviors/TrackContextMenuBehavior.cs`, attached-свойство `bool Attach`. При `true` на строке (`FrameworkElement`) подписывает `ContextMenuOpening`; в обработчике: берёт VM из `fe.Tag` (строка уже несёт `Tag = MainViewModel` для `PlacementTarget.Tag`), `Track` из `fe.DataContext`, поднимается по дереву до `ItemsControl`/`ListBox` за `SelectedItems`, вызывает `vm.Playlists.SetContextTarget(ContextSelection.Resolve(selected, track))`. Событийная привязка (WPF-glue) не юнит-тестится — покрыта визуальным smoke (Task 10); вся ветвящаяся логика вынесена в `ContextSelection` (а).

**(в) Само меню** (на корне строки — `Border`/`Grid`), `Tag="{Binding DataContext, RelativeSource={RelativeSource AncestorType=Window}}"` + `behaviors:TrackContextMenuBehavior.Attach="True"`:
```xml
<Border.ContextMenu>
    <ContextMenu>
        <MenuItem Header="Добавить в плейлист"
                  ItemsSource="{Binding PlacementTarget.Tag.Playlists.Playlists, RelativeSource={RelativeSource AncestorType=ContextMenu}}">
            <MenuItem.ItemContainerStyle>
                <Style TargetType="MenuItem">
                    <Setter Property="Header" Value="{Binding Name}" />
                    <Setter Property="Command" Value="{Binding PlacementTarget.Tag.AddContextToPlaylistCommand, RelativeSource={RelativeSource AncestorType=ContextMenu}}" />
                    <Setter Property="CommandParameter" Value="{Binding}" />
                </Style>
            </MenuItem.ItemContainerStyle>
        </MenuItem>
        <Separator />
        <MenuItem Header="Новый плейлист…"
                  Command="{Binding PlacementTarget.Tag.Playlists.StartNewPlaylistCommand, RelativeSource={RelativeSource AncestorType=ContextMenu}}" />
    </ContextMenu>
</Border.ContextMenu>
```

- [ ] **Step 3: «Убрать из плейлиста»** — в трек-строке `PlaylistDetailTemplate` добавить `MenuItem Header="Убрать из плейлиста"` → `RemovePlaylistTrackCommand`, `CommandParameter` = позиция строки из `(ItemsControl.AlternationIndex)` (требует `AlternationCount`, выставленный в Task 6). `AlternationIndex` — `int`, `RelayCommand` получит его как boxed `int` (совпадает с `p is int position`). Опирается на гарантию из Task 6, что `AlternationCount="{Binding Playlist.Tracks.Count}"`.

- [ ] **Step 4: ContextMenu на строке плейлиста** (`PlaylistRowTemplate`): «Переименовать» (переводит строку в инлайн-edit) / «Удалить» → `DeletePlaylistCommand`.

- [ ] **Step 5: Build + render-probe** (повторить probe из Task 6 Step 5 — открыть меню программно нельзя headless, поэтому здесь — build + визуальная проверка откладывается на Task 10; главное — 0 ошибок сборки и что привязки `PlacementTarget.Tag` корректны по образцу).

Run: `dotnet build MusicLibrary.sln --nologo`
Expected: 0 errors.

- [ ] **Step 6: Тесты** — `ContextSelectionTests` (3 кейса из Step 2а). Add/remove через контекст-таргет уже покрыты `PlaylistsViewModelTests` (Task 4). Прогнать: `dotnet test ... --filter "ContextSelectionTests"` → 3 passed, затем полный сьют зелёный.

- [ ] **Step 7: Commit**

```bash
git add MusicLibrary MusicLibrary.Tests
git commit -m "feat(playlists): context menu — add to playlist / remove / rename / delete"
```

---

## Task 8: DnD-интеграция в плейлисты + мульти-выбор

Навешивает Task 5 helper на реальные списки: drop трека(ов) на плейлист/в треклист = добавить; drag внутри плейлиста = reorder. Мульти-выбор.

**Files:**
- Modify: `MusicLibrary/Resources/TrackTemplates.xaml`, `AlbumsArtistsTemplates.xaml` (drag-source на трек-строках)
- Modify: `MusicLibrary/Resources/PlaylistsTemplates.xaml` (drop-target на строке плейлиста + reorder в треклисте)
- Modify: `MusicLibrary/Behaviors/DragDropListHelper.cs` (attached properties для XAML)
- Modify: `MusicLibrary/ViewModels/MainViewModel.cs` (команда `DropTracksOnPlaylistCommand`, `ReorderPlaylistCommand`)
- Modify: списки треков → `SelectionMode=Extended`

**Interfaces:**
- Consumes: `ReorderMath`, `DragDropListHelper` (Task 5); `PlaylistsViewModel.AddTracks`/`Reorder`/`RemoveTrackAt`; `MainViewModel.Playlists`.
- Produces: рабочий DnD add + reorder + мульти-выбор.

- [ ] **Step 1: Команды drop/reorder в `MainViewModel`** (ре-резолв — автоматически через `PlaylistContentChanged`, вручную не звать)
```csharp
DropTracksOnPlaylistCommand = new RelayCommand(p =>
{
    // p — кортеж (Playlist target, IReadOnlyList<int> ids); собрать в behaviour при Drop
    if (_playlists is null || p is not (Playlist pl, IReadOnlyList<int> ids)) return;
    _playlists.AddTracksByIds(pl.Id, ids);   // метод уже добавлен в под-VM в Task 4; событие → авто ре-резолв
});
ReorderPlaylistCommand = new RelayCommand(p =>
{
    if (_playlists is null || _currentLeftColumn is not LeftColumnState.PlaylistDetail d) return;
    if (p is not (int from, int to)) return;   // to уже скорректирован через ReorderMath.AdjustForRemoval (Step 2)
    _playlists.Reorder(d.Playlist.Playlist.Id, from, to);
});
```

- [ ] **Step 2: `DragDropListHelper` attached properties** — `IsDragSource` (навешивает PreviewMouseLeftButtonDown/Move → `ArmDrag`/`ShouldBeginDrag` → `DoDragDrop` с id выделенных треков через `MakeData`) и `PlaylistDropTarget`/`ReorderTarget` (AllowDrop + DragOver-хайлайт + Drop → соответствующая команда). В reorder-Drop: посчитать `raw = ReorderMath.TargetIndex(dropY, rowTops, rowHeights)` из геометрии контейнера, затем `to = ReorderMath.AdjustForRemoval(raw, fromPosition)` (компенсация удаления source — Task 5, point #1), и передать `(from, to)` в `ReorderPlaylistCommand`. НЕ считать позицию инлайн — только через эти две чистые функции.

- [ ] **Step 3: Навесить в XAML** — трек-строки: `behaviors:DragDropListHelper.IsDragSource="True"`. Строка плейлиста в корне: drop-target → `DropTracksOnPlaylistCommand`. Треклист в PlaylistDetail: drop-target (добавить) + reorder-target (перестановка). Списки треков: `SelectionMode="Extended"`.

- [ ] **Step 4: unit-тест сборки батча из мульти-выбора** (helper-функция, извлекающая id из `IList selectedItems` → `int[]`), если вынесена в тестируемый метод.

- [ ] **Step 5: Build + render-probe** (0 ошибок; визуальный DnD — Task 10).

- [ ] **Step 6: Commit**

```bash
git add MusicLibrary MusicLibrary.Tests
git commit -m "feat(playlists): drag-and-drop add + reorder + multi-select"
```

---

## Task 9: Переиспользуемый headless render-smoke (CI-сторож)

Постоянный STA-тест, инстанцирующий все `LeftColumnState`-шаблоны с реальным `MainViewModel` на seed-данных — ловит XAML/StaticResource-крэши (урок итерации D).

**Files:**
- Create: `MusicLibrary.Tests/Rendering/LeftColumnRenderSmokeTests.cs`
- Modify: `MusicLibrary.Tests/MusicLibrary.Tests.csproj` (если нужен STA-runner для xUnit — добавить `Xunit.StaFact` пакет, либо реализовать через ручной STA-thread без пакета)

**Interfaces:**
- Consumes: все шаблоны + `MainViewModel` + `LeftColumnState` варианты.
- Produces: тест `AllLeftColumnTemplates_Render_Without_Exception`.

- [ ] **Step 1: STA-инфраструктура.** xUnit по умолчанию не STA. Варианты: (a) пакет `Xunit.StaFact` (`[StaFact]`), (b) ручной `Thread` c `SetApartmentState(ApartmentState.STA)`, гоняющий рендер и прокидывающий исключение. Выбрать (a) если приемлемо добавить dev-зависимость, иначе (b). Реализовать хелпер `RunOnSta(Action)`.

- [ ] **Step 2: Тест** — поднять `Application` (если ещё не создан), смёржить словари `App.xaml` (`Application.LoadComponent` / вручную добавить `ResourceDictionary` с `Source`), собрать `MainViewModel` с фейковыми зависимостями на seed-треках (переиспользовать фейки из существующих VM-тестов), для каждого состояния (`TracksRoot`, `AlbumsRoot`, `ArtistsRoot`, `AlbumDetail`, `ArtistDetail`, `PlaylistsRoot`, `PlaylistDetail`) выставить `CurrentLeftColumn`, поместить в `ContentControl` с `LeftColumnTemplateSelector`, вызвать `Measure(new Size(800,600))` + `Arrange` в визуальном дереве `Window`, `UpdateLayout()`, и `Assert` отсутствие исключения. Ключевая проверка — что `{StaticResource}` резолвятся (иначе `ResourceReferenceKeyNotFoundException`).

  Псевдо-скелет:
```csharp
[StaFact]  // или ручной STA
public void AllLeftColumnTemplates_Render_Without_Exception()
{
    EnsureApplicationWithMergedDictionaries();
    var vm = BuildSeedViewModel();
    foreach (var state in AllStates(vm))
    {
        var host = new ContentControl {
            ContentTemplateSelector = BuildSelector(),
            Content = state
        };
        var w = new Window { Content = host, Width = 800, Height = 600 };
        w.Show();      // форсит рендер
        host.UpdateLayout();
        w.Close();
    }
    // дошли до сюда без исключения = все шаблоны отрендерились
}
```
Детали (LoadComponent словарей, сборка selector'а из шаблонов, seed-VM) расписать при реализации по образцу того, как App.xaml мёржит словари.

- [ ] **Step 3: Run**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "LeftColumnRenderSmokeTests" --nologo`
Expected: 1 passed. (Если падает `ResourceReferenceKeyNotFoundException` — значит есть незакрытый конвертер/ресурс; починить в соответствующем словаре.)

- [ ] **Step 4: Full suite + build**

Run: `dotnet build MusicLibrary.sln --nologo && dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category!=Benchmark" --nologo`
Expected: 0/0, все зелёные.

- [ ] **Step 5: Commit**

```bash
git add MusicLibrary.Tests
git commit -m "test(rendering): reusable headless render-smoke for all left-column templates (D-lesson guard)"
```

---

## Task 10: Ручной smoke + документация

**Files:**
- Create: `docs/changelog/1.0.5.md`
- Modify: `docs/architecture.md`, `docs/scope-deviations.md`, `README.md`, `docs/release-checklist.md`

- [ ] **Step 1: Ручной smoke** — `dotnet run --project MusicLibrary`, пройти чек-лист:
  - [ ] Вкладка «Плейлисты» (Ctrl+4); старт на «Треки».
  - [ ] «+ Новый плейлист» → инлайн-ввод → Enter создаёт; пустое имя → «Новый плейлист».
  - [ ] Правый клик по треку (в Треки/альбоме/исполнителе) → «Добавить в плейлист ▸ …»; трек появляется в плейлисте.
  - [ ] Drill-down в плейлист: header, Play/Shuffle, треклист, подсветка играющего, двойной клик играет, Esc назад.
  - [ ] DnD: перетащить трек(и) (в т.ч. мульти-выбор Ctrl/Shift) на плейлист/в треклист → добавляется; перетащить внутри плейлиста → reorder.
  - [ ] «Убрать из плейлиста»; «Переименовать»/«Удалить» плейлист (удаление с подтверждением).
  - [ ] Дубликат трека добавляется как отдельная строка.
  - [ ] Play плейлиста не ломает вкладку «Треки» (очередь отдельно).
  - [ ] Перезапуск: плейлисты и порядок на месте; старт на «Треки».
  - [ ] Апгрейд с 1.0.4: миграция `AddPlaylists` накатилась, старые данные целы.
  Баги — фиксить инлайн, коммитить.

- [ ] **Step 2: `docs/changelog/1.0.5.md`** — по образцу `1.0.4.md`: 4-я вкладка, CRUD плейлистов, контекст-меню, DnD (add+reorder+мульти), воспроизведение через `_playbackQueue`, `PlaylistsViewModel`/ADR-0002, миграция `AddPlaylists`, render-smoke, тест-счётчик.

- [ ] **Step 3: `docs/architecture.md`** — версия → 1.0.5; каталог (+`PlaylistsViewModel`, `PlaylistView`, `SqlitePlaylistRepository`, `Playlist*Entity`, `PlaylistsTemplates.xaml`, `DragDropListHelper`/`ReorderMath`, `ContextSelection`/`TrackContextMenuBehavior`); таблица миграций +`AddPlaylists`; секция «Плейлисты» (под-VM-граница + событие `PlaylistContentChanged`; playback остаётся в `MainViewModel`, `IPlaybackQueueController` отложен — ссылка ADR-0002); тест-счётчик.

- [ ] **Step 4: `docs/scope-deviations.md`** — §1.0.5: под-VM как первый разгруз (ADR-0002); фильтр библиотеки не влияет на плейлист; import/export и умные плейлисты вне E; без обложки-коллажа/пиннинга; удаление позиции по первому матчу Id при дубликатах (если так реализовано).

- [ ] **Step 5: `README.md`** — фичи: плейлисты (создать/наполнить контекст-меню+DnD/переупорядочить/играть), Ctrl+4.

- [ ] **Step 6: `docs/release-checklist.md`** — smoke-пункты плейлистов + апгрейд-миграция.

- [ ] **Step 7: Commit**

```bash
git add docs README.md
git commit -m "docs: iteration E / v1.0.5 release notes (static playlists)"
```

---

## Task 11: Релиз 1.0.5

- [ ] **Step 1: Финальные build + тесты + бенчмарк**

Run: `dotnet build MusicLibrary.sln --nologo && dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category!=Benchmark" --nologo && dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category=Benchmark" --nologo`
Expected: 0/0; все non-benchmark зелёные; бенчмарк < 100 мс.

- [ ] **Step 2: Bump версии**

В `MusicLibrary/MusicLibrary.csproj`: `<Version>1.0.5</Version>`, `<AssemblyVersion>1.0.5.0</AssemblyVersion>`, `<FileVersion>1.0.5.0</FileVersion>`.
```bash
git add MusicLibrary/MusicLibrary.csproj
git commit -m "chore: bump version to 1.0.5 — iteration E release"
```

- [ ] **Step 3: Ветка → push → PR → CI → merge**

```bash
git push -u origin release/1.0.5-iteration-e
gh pr create --repo Linkimin/MusicBakh --base main --head release/1.0.5-iteration-e --title "v1.0.5 — Library 2.0, итерация E (статические плейлисты)" --body "<summary>"
```
Дождаться CI (build-and-test) SUCCESS, затем `gh pr merge --merge`.

- [ ] **Step 4: Тег + инсталлятор + GitHub Release**

```bash
git checkout main && git pull origin main --ff-only
git tag -a v1.0.5 -m "v1.0.5 — Library 2.0, итерация E: статические плейлисты" && git push origin v1.0.5
pwsh scripts/build-release.ps1 -Version 1.0.5
gh release create v1.0.5 release/MusicBakh-Setup-1.0.5.exe --repo Linkimin/MusicBakh --title "v1.0.5 — статические плейлисты" --notes "<notes>"
```

- [ ] **Step 5: Cleanup** — удалить смёрженную ветку (local + remote).

---

## Self-Review

**Spec coverage:**
- 4-я вкладка/навигация → Task 4 (state+selector) + Task 6 (tab+Ctrl+4). ✅
- Модель + миграция → Task 2. ✅ Репозиторий (CRUD/порядок/reorder/каскад/транзакции) → Task 3. ✅
- `PlaylistsViewModel`/ADR-0002 (граница + событие вместо poll) → Task 4. ✅ `IPlaybackQueueController` — **осознанно отложен** (нет потребителя в E; вводится при первом реальном self-playback под-VM — см. шапку плана + ADR-0002). ✅
- Контекст-меню (add ▸ / remove / rename / delete; захват — `TrackContextMenuBehavior`+`ContextSelection`) → Task 7. ✅
- DnD (спайк изолированно с `AdjustForRemoval` → интеграция → мульти-выбор) → Task 5 + Task 8. ✅
- Инлайн-создание/переименование + пустое имя дефолт → Task 4 (VM) + Task 6 (UI). ✅
- Воспроизведение через `_playbackQueue` → Task 4. ✅
- Подсветка играющего в плейлисте → Task 6. ✅
- Render-smoke харнесс → Task 9. ✅
- Хоткеи Ctrl+4/Esc → Task 6. ✅
- Docs/scope-deviations/release → Task 10/11. ✅

**Resolved review points (2-й проход):**
- **Reorder off-by-one** → чистая `ReorderMath.AdjustForRemoval` + 4 теста (Task 5), используется в Task 8 drop. ✅
- **AlternationCount** → явное `AlternationCount="{Binding Playlist.Tracks.Count}"` требование в Task 6, на него опирается Task 7 Step 3. ✅
- **Захват контекст-таргета** → конкретный `TrackContextMenuBehavior` + чистый `ContextSelection.Resolve` (юнит-тест), не «решим в моменте». ✅
- **`IPlaybackQueueController` без потребителя** → убран из скоупа E; ADR-0002 обновлён. ✅
- **`MutatedPlaylistId` poll** → событие `PlaylistContentChanged`, подписка один раз в конструкторе + отписка в Dispose; команды не зовут ре-резолв вручную. ✅
- **Мелкое** — drag-порог `ArmDrag/ShouldBeginDrag` честно помечен visual-only (Task 5 Step 6); `FakePlaylistRepo` guard-расхождение отмечено (Task 4 Step 4).

**Placeholder scan:** Task 6/8/9 XAML/render-harness описаны структурно с точными привязками + образцом (`AlbumsArtistsTemplates.xaml`, `App.xaml` merge), а не дословным полным XAML — сознательно (полный XAML раздул бы план на тысячи строк). Task 7 захват-механизм и Task 5 reorder-математика — доведены до конкретного кода (были главные «решим в моменте»-места). Риск остаточных XAML-мест снят render-smoke (Task 9) + визуальным smoke (Task 10).

**Type consistency:** `PlaylistView(Playlist, IReadOnlyList<Track>)` и `LeftColumnState.PlaylistDetail(PlaylistView)` согласованы (Task 4). `IPlaylistRepository` сигнатуры (Task 1) = реализация (Task 3) = под-VM (Task 4). `PlaylistContentChanged` — `Action<int>` (Task 4 под-VM) = подписка `OnPlaylistContentChanged(int)` (Task 4 MainViewModel). `AddTracksByIds(int, IReadOnlyList<int>)` — под-VM (Task 4) = вызов из `DropTracksOnPlaylistCommand` (Task 8). `MainViewMode.Playlists=3` (Task 1) ← selector/switch (Task 4) ← tab/Ctrl+4 (Task 6).
