# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

MusicBakh — настольный WPF-плеер и музыкальная библиотека для Windows. .NET 10, C# 14, WPF, SQLite через EF Core. Solution: `MusicLibrary.sln`. Выходной exe называется `MusicBakh.exe`, при этом `RootNamespace` главного WPF-проекта остался `MusicLibrary` (исторически — не переименовывать без отдельной задачи, потянет все `using`-и).

Стратегическое видение продукта на 3 года — [`docs/roadmap-vision.md`](docs/roadmap-vision.md). Текущая фаза: эпик «Library 2.0» (минор `1.1.0`), который доставляется патч-итерациями `1.0.1` … `1.0.8`. Для каждой итерации лежит план в [`docs/superpowers/plans/`](docs/superpowers/plans/) — читай актуальный план перед началом работы над новой версией.

## Commands

Все команды выполняются из корня репо.

```powershell
# Сборка
dotnet build MusicLibrary.sln

# Релизная сборка одного WPF-проекта (быстрее)
dotnet build -c Release MusicLibrary/MusicLibrary.csproj

# Запуск приложения
dotnet run --project MusicLibrary

# Полный тест-сьют (без бенчмарка)
dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category!=Benchmark"

# Один конкретный тест / класс
dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "FullyQualifiedName~SqliteFtsSearchServiceTests"

# Бенчмарк (50k треков FTS, ~10 секунд)
dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category=Benchmark"

# EF Core миграции — стартовый проект должен быть Infrastructure (не WPF — у него нет ref на EF.Design)
dotnet ef migrations add <Name> -p MusicBakh.Infrastructure -s MusicBakh.Infrastructure

# Сборка релизного инсталлятора (требует Inno Setup 6 по умолчанию в C:\Program Files (x86)\Inno Setup 6\)
pwsh scripts/build-release.ps1 -Version 1.0.X
```

После `dotnet ef migrations add` правь сгенерированный `.cs`: добавь `using EFMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;` и поменяй `: Migration` на `: EFMigration` — иначе компилятор путает namespace `MusicBakh.Infrastructure.Persistence.Migrations` с типом `Migration`. См. существующие миграции как образец.

## Architecture

Четыре проекта, зависимости направлены строго наружу:

```
MusicLibrary (WPF, net10.0-windows)              ← Presentation
   └─ MusicBakh.Infrastructure (net10.0)         ← SQLite/EF/HTTP/file IO
        └─ MusicBakh.Application (net10.0)       ← интерфейсы сервисов (use-case-абстракции, DTO)
             └─ MusicBakh.Core (net10.0)         ← домен + repository-абстракции
```

**Core знает только Core. Application знает Core. Infrastructure знает Application+Core. Presentation знает всех.** Никаких циклических ссылок; никаких прямых вызовов EF/Sqlite из ViewModels.

### Хранение и поиск

`%LocalAppData%\MusicLibrary\library.db` — SQLite через `LibraryDbContext` (`MusicBakh.Infrastructure/Persistence/`). Схема: `Tracks`, `ListeningHistory`, `KeyValueStore`, виртуальная FTS5-таблица `TracksFts` (external content, токенизатор `unicode61 remove_diacritics 2`) + три SQL-триггера синхронизации (`Tracks_ai`/`Tracks_ad`/`Tracks_au AFTER UPDATE OF Title,Artist,Album,Genre`).

Поиск — через `ISearchService` (`SqliteFtsSearchService`). Свободная строка проходит через `FtsQueryBuilder.Build` — санитайзер FTS-метасимволов; передавать сырой ввод в `MATCH` **нельзя**.

При работе с `Tracks` всегда через `LibraryDbContext` (EF триггеры срабатывают на стандартных INSERT/UPDATE/DELETE). Любая «raw-SQL модификация в обход контекста» уведёт FTS-индекс из синхрона.

### DI

`App.OnStartup` строит `Host.CreateDefaultBuilder()` с двумя расширениями: `AddMusicBakhInfrastructure` (`MusicBakh.Infrastructure/DependencyInjection/`) и `AddMusicBakhPresentation` (`MusicLibrary/DependencyInjection/`). `Microsoft.Extensions.DependencyInjection` (MEDI) **не умеет автоматически генерировать `Func<T>` фабрики** — для каждого `Func<SomeViewModel>` нужна явная регистрация (см. `Func<LibraryDbContext>` в Infrastructure DI и `Func<StatsViewModel>` в Presentation DI как образцы).

### Тесты

`MusicLibrary.Tests` (xUnit) — единственный тест-проект. Две фикстуры для in-memory SQLite:

- `InMemorySqliteDbContextFactory` — `EnsureCreated()`, **обходит raw-SQL миграции**. Подходит для тестов репозиториев, которым нужна только схема таблиц.
- `MigratedSqliteDbContextFactory` — `Database.Migrate()`, прогоняет все миграции включая FTS-триггеры. Использовать для FTS-тестов и всего, что опирается на triggers/views.

Обе держат **один открытый `SqliteConnection`** на всё время жизни фикстуры — это обязательно для in-memory режима, не пытайся переехать на `AddDbContextFactory` с пулом соединений.

Бенчмарк FTS на 50k треков отмечен `[Trait("Category", "Benchmark")]`, регулярный `dotnet test --filter "Category!=Benchmark"` его пропускает.

### Контрактные инварианты (легко проглядеть)

- `ITrackRepository.Remove(int)` для seed-трека (`IsBuiltIn=true`) **обязан** бросать `NotSupportedException`. Контракт прописан в XML-doc, реализация в `SqliteTrackRepository` это уже соблюдает; не сломай.
- `BuiltInTrackSeeder` сравнивает существующие seed-треки по `(Artist, Title)`. Если `FilePath`/`CoverPath` расходятся с текущим `AppContext.BaseDirectory` — он **обновит** их через `ITrackRepository.Update`. Это лечит «file not found» после переезда сборки между `bin/Debug` ↔ `bin/Release` ↔ установленный путь.
- `TrackMapper.ToDomain` (`MusicBakh.Infrastructure/Persistence/`) — единственный маппинг `TrackEntity → Track` для треков, истории, плейлистов и поиска; он обязан переносить `IsBuiltIn`. Иначе history-replay вернёт встроенный трек с `IsBuiltIn=false`, и UI разрешит удалить shipped-файлы. Не заводи локальные копии маппинга в репозиториях.

### Расположение пользовательских данных

```
%LocalAppData%\MusicLibrary\library.db                            — SQLite
%LocalAppData%\MusicLibrary\Music\                                — импортированные mp3/wav
%LocalAppData%\MusicLibrary\Covers\                               — обложки {uuid}.{ext}
%LocalAppData%\MusicLibrary\userTracks.json.backup-<ts>           — бэкап legacy JSON (только после апгрейда с 1.0.0)
%LocalAppData%\Programs\MusicBakh\                                — куда per-user installer ставит exe
```

Эталонные 3 трека и обложки лежат рядом с exe (`{app}\Music\`, `{app}\Covers\`), `IsBuiltIn=true`, в csproj — `CopyToOutputDirectory="PreserveNewest"`.

## Conventions

- **Комментарии в коде и XAML — на русском.** Имена идентификаторов остаются английскими. См. [`memory/feedback_comments_russian.md`](C:\Users\User\.claude\projects\E--MusicLibraryRelize\memory\feedback_comments_russian.md).
- **Релизный цикл:** план итерации в `docs/superpowers/plans/`, после реализации — changelog в `docs/changelog/`, обновляются `docs/architecture.md` и `docs/scope-deviations.md`, версия в `MusicLibrary.csproj`, тег `vX.Y.Z`, GitHub Release с инсталлятором (см. `docs/release-checklist.md`).
- **Каждая задача в плане итерации — отдельный коммит** с ≤ ~200 строк изменённого кода. Style commit-сообщений см. `git log`.
- **Не амендим коммиты** и не пушим force в `main`. PR с feature-ветки (`release/X.Y.Z-iteration-N`) в `main`.

## Documentation map

- [`docs/roadmap-vision.md`](docs/roadmap-vision.md) — стратегия до `v3.5.0`, три фазы (A: local-first, B: backend+companions, C: AI+social).
- [`docs/architecture.md`](docs/architecture.md) — текущее устройство кода (актуальная версия указана в шапке).
- [`docs/superpowers/plans/`](docs/superpowers/plans/) — детальные планы по итерациям.
- [`docs/changelog/`](docs/changelog/) — release notes по каждой опубликованной версии.
- [`docs/scope-deviations.md`](docs/scope-deviations.md) — где и почему фактическая реализация отошла от первоначального учебного скоупа.
- [`docs/release-checklist.md`](docs/release-checklist.md) — чек-лист релиза (smoke-тесты, installer build, GitHub Release).
