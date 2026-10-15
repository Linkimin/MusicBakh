# Архитектура MusicBakh

Документ описывает фактическое устройство приложения по состоянию исходного кода версии 1.0.4 (итерация D эпика «Library 2.0», финальный минор которого выйдет как 1.1.0 после завершения всех патч-итераций).

## Обзор

- **Версия:** 1.0.4.
- **Платформа:** WPF, .NET 10 (`net10.0-windows` для Presentation; `net10.0` для остальных слоёв), C# 14, nullable enabled, ImplicitUsings.
- **Проекты:**
  - `MusicBakh.Core` — доменные сущности и абстракции репозиториев.
  - `MusicBakh.Application` — интерфейсы сервисов уровня use-case (аудио, импорт, файлы, метаданные, обложки).
  - `MusicBakh.Infrastructure` — реализации: SQLite через EF Core, HTTP-клиенты, файловая система, сидер.
  - `MusicLibrary` — WPF-хост: ViewModels, Views, WPF-привязанные реализации (`MediaPlayerAudioService`, `ProceduralCoverGenerator`), DI-бутстрап.
- **Ключевые зависимости:**
  - `Microsoft.EntityFrameworkCore.Sqlite 10.0.9` — ORM и SQLite-провайдер (с 1.0.4; ранее 10.0.8). С 1.0.2 поверх схемы лежит виртуальная FTS5-таблица `TracksFts` (external content) с триггерами синхронизации.
  - `SQLitePCLRaw.bundle_e_sqlite3 3.0.3` — явный пин нативной SQLite (с 1.0.4), закрывает advisory NU1903 / GHSA-2m69-gcr7-jv3q, перекрывая транзитивный 2.1.11.
  - `Microsoft.Extensions.Hosting 10.0.x` — DI-контейнер и host-инфраструктура.
  - `TagLibSharp 2.3.0` — чтение ID3-тегов (с 1.0.2 — включая `tag.Album`).
  - С 1.0.3: новый `ITagRepository` (Core) + `SqliteTagRepository` (Infrastructure) для работы с пользовательскими тегами-ярлыками.
  - `System.Windows.Media.MediaPlayer` (BCL WPF) — воспроизведение аудио.

## Слои и зависимости

```
┌──────────────────────────────────────┐
│  MusicLibrary  (Presentation / WPF)  │
│  ViewModels, Views, App.OnStartup    │
└──────────────┬───────────────────────┘
               │ ссылается на
               ▼
┌──────────────────────────────────────┐
│  MusicBakh.Application               │
│  Интерфейсы сервисов use-case        │
└──────────────┬───────────────────────┘
               │ ссылается на
               ▼
┌──────────────────────────────────────┐
│  MusicBakh.Core                      │
│  Доменные сущности, интерфейсы       │
│  репозиториев                        │
└──────────────────────────────────────┘

┌──────────────────────────────────────┐
│  MusicBakh.Infrastructure            │
│  SQLite-репозитории, HTTP-клиенты,   │
│  файловая система, сидер, миграция   │
└──────────────┬───────────────────────┘
               │ ссылается на
               ▼
    MusicBakh.Application + MusicBakh.Core
```

Presentation ссылается на Core, Application и Infrastructure: App.OnStartup является composition root и регистрирует реализации через DI. Core не зависит от EF/WPF. Общий внутренний TrackMapper находится в Infrastructure; переносить EF-сущности в Core не требуется.

## Каталоги проекта

```
MusicBakh.Core/
├── Domain/
│   ├── Track.cs                        — доменная модель трека (Title/Artist/Album/Genre/Duration/FilePath/CoverPath/IsBuiltIn/Rating/Reaction/Year/TrackNumber/AlbumArtist)
│   ├── MainViewMode.cs                 — enum Tracks/Albums/Artists (с 1.0.4)
│   ├── Tag.cs                          — доменная модель тега (Id, Name, Color?) — с 1.0.3
│   ├── TrackReaction.cs                — enum (None/Liked/Disliked) — с 1.0.3
│   ├── PlaybackEntry.cs                — запись истории воспроизведения
│   ├── PlayerSettings.cs               — громкость, mute, режим повтора
│   ├── RepeatMode.cs                   — enum (NoRepeat/Current/Library)
│   └── OperationResult.cs              — результат операций UI
└── Abstractions/
    ├── ITrackRepository.cs             — репозиторий треков (Get/Find/Add/Update/Remove)
    ├── IListeningHistoryRepository.cs  — хранилище истории + агрегации (GetTop/GetRecentUnique/GetNeverPlayed)
    ├── IPlayerSettingsRepository.cs    — сохранение настроек плеера
    ├── ISearchService.cs               — полнотекстовый поиск по библиотеке (с 1.0.2)
    ├── ITagRepository.cs               — репозиторий тегов (GetAll/GetByTrackId/Add/Update/Delete/AttachToTrack/DetachFromTrack) — с 1.0.3
    ├── ListeningStats.cs               — DTO топа прослушиваний (Track, PlayCount, LastPlayedUtc)
    └── IClock.cs                       — абстракция системного времени

MusicBakh.Application/
├── Abstractions/                       — интерфейсы сервисов use-case
│   ├── IAudioPlayerService.cs
│   ├── ITrackImporter.cs
│   ├── IMetadataResolver.cs
│   ├── ICoverResolver.cs
│   ├── ITagReader.cs
│   ├── IMusicBrainzClient.cs
│   ├── IItunesCoverClient.cs
│   ├── IGenreNormalizer.cs
│   ├── IProceduralCoverGenerator.cs
│   ├── IFileService.cs
│   ├── IOpenFileDialogService.cs
│   ├── ISaveFileDialogService.cs
│   ├── IConfirmationService.cs
│   ├── IPlaybackQueueStrategy.cs
│   └── IMusicStoragePaths.cs
└── Contracts/                          — DTO для пайплайна импорта
    ├── ImportRequest.cs                — LocalFileImportRequest / UrlImportRequest
    ├── ImportResult.cs
    ├── TrackImportCandidate.cs
    ├── ResolvedMetadata.cs
    ├── ResolvedCover.cs
    ├── LocalTagInfo.cs
    ├── MusicBrainzMatch.cs
    └── ItunesSearchHit.cs

MusicBakh.Infrastructure/
├── Persistence/
│   ├── LibraryDbContext.cs             — EF Core DbContext
│   ├── LibraryDbContextOptions.cs      — пути к SQLite-файлу
│   ├── Entities/                       — EF-сущности (TrackEntity, ListeningHistoryEntryEntity, KeyValueEntryEntity, TagEntity, TrackTagEntity)
│   ├── Configurations/                 — IEntityTypeConfiguration для каждой таблицы
│   ├── Repositories/                   — SqliteTrackRepository, SqliteListeningHistoryRepository, SqlitePlayerSettingsRepository, SqliteTagRepository
│   └── Migrations/                     — AddLibrarySchema, AddListeningHistory, AddKeyValueStore, AddTrackAlbum, AddTracksFts, AddTrackRatingAndReaction, AddTagsAndTrackTags
├── Search/
│   ├── FtsQueryBuilder.cs              — санитайзер пользовательской строки → FTS5 MATCH (с 1.0.2)
│   └── SqliteFtsSearchService.cs       — реализация ISearchService поверх TracksFts (с 1.0.2)
├── Migration/
│   └── JsonToSqliteMigrationService.cs — перенос userTracks.json → SQLite
├── Seeding/
│   └── BuiltInTrackSeeder.cs           — наполнение встроенными треками + refresh устаревших путей
├── Import/                             — TrackImporter
├── Metadata/                           — DefaultMetadataResolver, TagLibSharpTagReader, MusicBrainzClient, RussianGenreNormalizer
├── Covers/                             — CompositeCoverResolver, ItunesCoverClient
├── FileSystem/                         — FileService, MusicStoragePaths
├── Playback/                           — стратегии очереди (NoRepeat/RepeatCurrent/RepeatLibrary)
├── Time/                               — SystemClock
└── DependencyInjection/
    └── InfrastructureServiceCollectionExtensions.cs   — AddMusicBakhInfrastructure()

MusicLibrary/
├── App.xaml(.cs)                       — DI-бутстрап, AppUserModelID, OnStartup
├── MainWindow.xaml(.cs)                — обработчики seek-слайдера, NativeWindowAppearance
├── NativeWindowAppearance.cs           — DWM dark caption/border/text color
├── Controls/
│   └── OverflowChipPanel.cs            — кастомный WPF Panel: чипы + пилюля «+ N ещё» (с 1.0.3)
├── ViewModels/
│   ├── ViewModelBase.cs
│   ├── MainViewModel.cs                — главный VM, все ICommand, SearchText (с 1.0.2), рейтинг/реакция/теги (с 1.0.3)
│   ├── TagFilterItem.cs                — VM-обёртка тега в фильтр-toolbar (с 1.0.3)
│   ├── AddTrackViewModel.cs
│   └── StatsViewModel.cs               — топ/недавние/ни разу (с 1.0.2)
├── Views/
│   ├── AddTrackWindow.xaml(.cs)
│   ├── ConfirmationDialogWindow.xaml(.cs)
│   ├── ConfirmationDialogService.cs
│   ├── StatsWindow.xaml(.cs)           — окно статистики, с 1.0.2
│   ├── StatsWindowService.cs           — фабрика открытия окна, с 1.0.2
│   ├── TagsWindow.xaml(.cs)            — окно управления тегами, с 1.0.3
│   └── TagsWindowService.cs            — фабрика открытия окна тегов, с 1.0.3
├── Services/
│   ├── Playback/MediaPlayerAudioService.cs   — WPF MediaPlayer
│   ├── Covers/ProceduralCoverGenerator.cs    — градиентная заглушка обложки
│   ├── Library/LibraryFilter.cs              — pure-function фильтрация библиотеки (с 1.0.3)
│   ├── Library/LibraryFilterCriteria.cs      — record-критерии фильтра (с 1.0.3)
│   ├── Library/LibraryGroupingService.cs     — pure GroupByAlbum/GroupByArtist (с 1.0.4)
│   ├── Library/AlbumAggregate.cs             — computed-агрегат альбома (с 1.0.4)
│   ├── Library/ArtistAggregate.cs            — computed-агрегат исполнителя (с 1.0.4)
│   ├── Library/LeftColumnState.cs            — состояние левой колонки (drill-down) (с 1.0.4)
│   ├── Storage/                              — устаревшие JSON-адаптеры (не используются с 1.0.1)
│   ├── Tracks/CompositeTrackRepository.cs    — объединяет встроенные и пользовательские
│   └── Files/                               — OpenFileDialogService, SaveFileDialogService
├── Selectors/LeftColumnTemplateSelector.cs   — выбор DataTemplate по LeftColumnState (с 1.0.4)
├── Commands/RelayCommand.cs
├── Converters/                         — IValueConverter для XAML (с 1.0.3: RatingToStarsConverter, ReactionToIconConverter, TagsToChipsConverter, MinRatingActiveConverter и др.)
├── DependencyInjection/
│   └── PresentationServiceCollectionExtensions.cs   — AddMusicBakhPresentation()
├── Resources/                          — XAML словари (Colors, Brushes, стили, иконки)
├── Assets/Brand/                       — musicbakh.ico, musicbakh-logo.png
├── Music/                              — три эталонных mp3 (копируются в output)
└── Covers/                             — обложки к эталонным трекам

MusicLibrary.Tests/
├── Persistence/                        — SqliteTrackRepositoryTests, SqliteListeningHistoryRepositoryTests, SqlitePlayerSettingsRepositoryTests
├── Search/                             — FtsQueryBuilderTests, SqliteFtsSearchServiceTests, SqliteFtsSearchServiceBenchmark (с 1.0.2)
├── Migration/                          — JsonToSqliteMigrationServiceTests
├── Seeding/                            — тесты BuiltInTrackSeeder (включая refresh путей)
├── TestSupport/
│   ├── InMemorySqliteDbContextFactory.cs    — EnsureCreated-фикстура для обычных тестов
│   └── MigratedSqliteDbContextFactory.cs    — Migrate()-фикстура для тестов, опирающихся на FTS5 (с 1.0.2)
└── *.cs                                — тесты MainViewModel, сервисов, конвертеров
```

## Хранилище данных

| Путь | Содержимое |
|---|---|
| `%LocalAppData%\MusicLibrary\library.db` | SQLite-база: треки, история воспроизведения, настройки плеера |
| `%LocalAppData%\MusicLibrary\Music\` | Импортированные аудиофайлы (mp3/wav) |
| `%LocalAppData%\MusicLibrary\Covers\` | Обложки с GUID в имени (`{uuid}.{ext}`) |
| `%LocalAppData%\MusicLibrary\userTracks.json.backup-<timestamp>` | Резервная копия legacy-файла; появляется только после первого запуска при апгрейде с 1.0.x |

Эталонные 3 трека и их обложки лежат рядом с `.exe` (`{app}\Music\`, `{app}\Covers\`) — они read-only и удалить их через UI нельзя (флаг `IsBuiltIn`).

SQLite-файл создаётся автоматически при первом запуске через `Database.Migrate()`. Каталоги `Music\` и `Covers\` создаются сервисом `MusicStoragePaths` по требованию.

## EF Core миграции

Миграции находятся в [`MusicBakh.Infrastructure/Persistence/Migrations/`](../MusicBakh.Infrastructure/Persistence/Migrations/). `DatabaseMaintenance.Migrate()` проверяет pending migrations. Перед изменением существующей схемы создаётся согласованная копия SQLite через BackupDatabase (включая WAL), затем вызывается Database.Migrate(). Ошибка копирования/миграции останавливает запуск с диагностикой; автоматического удаления/сброса БД нет.

| Миграция | Что добавляет |
|---|---|
| `AddLibrarySchema` | Таблица `Tracks`: Id, Title, Artist, Genre, DurationSeconds, FilePath, CoverPath, IsBuiltIn |
| `AddListeningHistory` | Таблица `ListeningHistory`: Id, TrackId, PlayedAt (UTC) |
| `AddKeyValueStore` | Таблица `KeyValueStore`: Key (PK), Value — используется для хранения настроек плеера |
| `AddTrackAlbum` | Колонка `Tracks.Album TEXT NOT NULL DEFAULT ''` + индекс |
| `AddTracksFts` | Виртуальная таблица `TracksFts USING fts5(Title, Artist, Album, Genre, content='Tracks')` + триггеры `Tracks_ai`/`Tracks_ad`/`Tracks_au AFTER UPDATE OF Title,Artist,Album,Genre` + backfill уже существующих треков |
| `AddTrackRatingAndReaction` | Колонки `Tracks.Rating INTEGER NOT NULL DEFAULT 0` и `Tracks.Reaction INTEGER NOT NULL DEFAULT 0` — с 1.0.3 |
| `AddTagsAndTrackTags` | Таблица `Tags` (Id, Name, Color) + связующая `TrackTags` (TrackId FK + TagId FK, составной PK, каскадное удаление) — с 1.0.3 |
| `AddTrackYearNumberAlbumArtist` | Колонки `Tracks.Year INTEGER NULL`, `Tracks.TrackNumber INTEGER NULL`, `Tracks.AlbumArtist TEXT NULL` + индекс на `Year` — с 1.0.4 |

### Полнотекстовый поиск (FTS5)

`TracksFts` — external-content FTS5 индекс, разделяющий хранилище с `Tracks` (`content='Tracks'`, `content_rowid='Id'`). Сами данные не дублируются; индекс хранит только обратные термы. Токенизатор `unicode61 remove_diacritics 2` снимает латинскую диакритику (но не нормализует кириллические `й/и`, `ё/е` обрабатывается через diacritic-fold).

Синхронизация — тремя SQL-триггерами:
- `Tracks_ai` — INSERT в `Tracks` → INSERT в `TracksFts`.
- `Tracks_ad` — DELETE из `Tracks` → команда `'delete'` в FTS со старыми значениями.
- `Tracks_au AFTER UPDATE OF Title,Artist,Album,Genre` — пара `'delete' + INSERT`. Clause `AFTER UPDATE OF <cols>` экономит работу: апдейты `CoverPath`/`IsBuiltIn` и будущих `LastPlayedAt`/`Rating` индекс не дёргают.

Поиск идёт через `ISearchService` (`MusicBakh.Core.Abstractions`) → `SqliteFtsSearchService` (`MusicBakh.Infrastructure.Search`):
```sql
SELECT t.* FROM Tracks t
JOIN TracksFts f ON f.rowid = t.Id
WHERE TracksFts MATCH @query
ORDER BY bm25(TracksFts), t.Id LIMIT @limit
```

Запросы длиннее 4096 символов отклоняются без SQL. UI запрашивает кандидатов в пределах размера загруженной библиотеки, затем применяет жанр/рейтинг/теги, сохраняя FTS-порядок; прежний LIMIT 500 до фильтрации скрывал совпадения. Пользовательская строка проходит через `FtsQueryBuilder.Build`, который чистит зарезервированные FTS-символы (`" * : ( )`), оборачивает каждое слово в кавычки + `*` (prefix-match), склеивает пробелом (неявный AND), режет на 10 токенов. Пустой/некорректный ввод → null → `ISearchService.Search` возвращает пустой список без SQL-запроса.

Admin-команды для ручной починки индекса (не выставлены в UI):
- `INSERT INTO TracksFts(TracksFts) VALUES('rebuild');` — пересборка индекса из `Tracks`.
- `INSERT INTO TracksFts(TracksFts) VALUES('optimize');` — компакция после массовых вставок/удалений.

### Рейтинги и реакции (с 1.0.3)

`Track.Rating` — целое число от 0 до 5 (0 означает «рейтинг не выставлен»). `Track.Reaction` — enum `TrackReaction` (None / Liked / Disliked). Оба поля хранятся в таблице `Tracks` и обновляются через `ITrackRepository.Update(Track)`.

В UI рейтинг и реакция находятся в средней колонке под обложкой выбранного трека. Клик по N-й звезде выставляет рейтинг N; повторный клик по уже активной звезде сбрасывает рейтинг до 0. Лайк и дизлайк взаимоисключают друг друга; повторный клик сбрасывает реакцию в None.

### Теги-ярлыки (с 1.0.3)

`Tag` — доменная сущность с полями `Id` (int), `Name` (string, уникально в рамках библиотеки без учёта регистра), `Color` (string?, опциональный hex-цвет — пока не отображается на чипах). Связь «многие ко многим» между `Track` и `Tag` хранится в таблице `TrackTags` (составной PK + FK с каскадным удалением).

`ITagRepository` (`MusicBakh.Core.Abstractions`) предоставляет методы:

| Метод | Использование |
|---|---|
| `GetAll()` | Полный список тегов — список в TagsWindow и в popup фильтра |
| `GetByTrackId(trackId)` | Теги конкретного трека — секция «Теги» в средней колонке |
| `Add(tag)` | Создание нового тега в TagsWindow |
| `Update(tag)` | Переименование тега в TagsWindow |
| `Delete(id)` | Удаление тега и автоматическое открепление от всех треков (каскад) |
| `AttachToTrack(trackId, tagId)` | Привязка тега к треку |
| `DetachFromTrack(trackId, tagId)` | Открепление тега от трека |

`SqliteTagRepository` реализует уникальность имени тега через in-code сравнение `.OrdinalIgnoreCase` — `COLLATE NOCASE` в SQLite не охватывает кириллицу.

`MainViewModel` держит `_tagsByTrackId: Dictionary<int, ObservableCollection<Tag>>` — кэш связей тег/трек, обновляемый при attach/detach и при смене выбранного трека. Кэш используется для биндингов чипов на карточке трека без лишних запросов к БД при прокрутке.

Окно «Теги» (`TagsWindow`) открывается командой `OpenTagsWindowCommand` (хоткей `Ctrl+G`) — немодальное, аналогично `StatsWindow`.

### Фильтр-toolbar и LibraryFilter (с 1.0.3)

Фильтрация библиотеки вынесена из `MainViewModel` в отдельный pure-function слой:

- `LibraryFilterCriteria` — immutable record с полями: `SearchResults` (результаты FTS), `Genre` (string?), `Tags` (IReadOnlyList<int> — id активных тегов), `MinRating` (int 0..5), `Reaction` (TrackReaction?).
- `LibraryFilter.Apply(allTracks, criteria)` — статический метод, возвращает `IReadOnlyList<Track>`. Не имеет побочных эффектов, полностью тестируем без WPF.

`MainViewModel.ApplyFilters()` теперь строит `LibraryFilterCriteria` и делегирует фильтрацию `LibraryFilter.Apply`. Это первый шаг разгрузки ViewModel: в 1.0.4–1.0.8 планируется аналогично вынести Playback, History и Import.

### Альбомы и исполнители (с 1.0.4)

Библиотека показывается в трёх режимах (`MainViewMode`: Tracks/Albums/Artists), переключаемых вкладками в шапке. Альбомы и исполнители — **computed-агрегаты, не first-class сущности БД** (решение зафиксировано в [ADR-0001](adr/0001-album-artist-computed-aggregates.md)): группировка вычисляется на лету из отфильтрованных треков.

- `LibraryGroupingService.GroupByAlbum/GroupByArtist` — pure-функции (`MusicLibrary/Services/Library/`), вызываются в `ApplyFilters()` сразу после `LibraryFilter.Apply`, из того же отфильтрованного списка. Поэтому все фильтры (поиск/жанр/теги/рейтинг/реакция) действуют и на группы.
- `AlbumAggregate` — ключ группировки `(AlbumArtist ?? Artist, Album)`; так сборники с общим `AlbumArtist` склеиваются в один альбом. `AlbumKey` (identity для drill-down) использует разделитель `U+001F`, чтобы `Artist+Title` не коллизировали. Треки внутри — `TrackNumber ASC NULLS LAST, Title ASC`. `Year` — максимум по трекам.
- `ArtistAggregate` — ключ `Track.Artist` (не `AlbumArtist`), поэтому исполнитель из сборника виден отдельно. Содержит `Albums` (Year DESC NULLS LAST), `LooseTracks` (треки с пустым `Album`, по `IsNullOrEmpty`) и агрегаты счётчиков/длительности.
- `DisplayedAlbums`/`DisplayedArtists` — computed-кэш на `MainViewModel`, пересчитывается на каждый `ApplyFilters()`.

Drill-down реализован без отдельных окон: `LeftColumnState` (abstract record: `TracksRoot`/`AlbumsRoot`/`ArtistsRoot`/`AlbumDetail(Album)`/`ArtistDetail(Artist)`) + back-стек в `MainViewModel`. Левая колонка — `ContentControl`, чей `Content = CurrentLeftColumn`, а шаблон выбирает `LeftColumnTemplateSelector` (`MusicLibrary/Selectors/`) из пяти DataTemplates в `Resources/AlbumsArtistsTemplates.xaml`. Команды в детальных шаблонах биндятся к `MainViewModel` через `RelativeSource AncestorType=Window` (внутри `ContentControl` ambient DataContext — сам объект состояния). Переключение вкладки обнуляет стек; асимметрия «Исполнитель→Альбом→Back → на исполнителя» достигается порядком push в стеке.

Play/Shuffle: `PlayAlbumCommand`/`PlayArtistCommand` заменяют `DisplayedTracks` собранной очередью и стартуют первый трек через существующий `StartOrResumeTrack`; `Shuffle*` — тот же набор в порядке Fisher-Yates. Стиль вкладок — `Resources/MainViewTabsStyles.xaml`.

### Горизонтальный фильтр-toolbar и OverflowChipPanel (с 1.0.3)

Вертикальная фильтр-секция в левой колонке заменена горизонтальным toolbar над списком треков. Toolbar содержит:
- поле поиска;
- ComboBox жанра;
- чипы активных тегов через `OverflowChipPanel`;
- кнопку ⋮, открывающую popup с фильтрами рейтинга и реакции.

`OverflowChipPanel` — кастомный `Panel` (`MusicLibrary/Controls/OverflowChipPanel.cs`). В `MeasureOverride` считает суммарную ширину всех дочерних элементов плюс пилюли «+ N ещё». В `ArrangeOverride` располагает столько чипов, сколько помещается, затем добавляет пилюлю с остатком. Логика в code-behind (не в XAML) — потому что ItemsControl не позволяет разместить в одном контейнере и сгенерированные чипы, и статическую пилюлю-overflow как сиблинги.

### История прослушиваний

`IListeningHistoryRepository` (`MusicBakh.Core.Abstractions`) расширен в итерации B:

| Метод | Использование |
|---|---|
| `GetRecent(limit=50)` | Виджет «недавнее» в правой колонке `MainWindow` — последние N событий с дублями |
| `GetAll()` | Полный журнал без лимита (для будущих экспортов, не используется в UI 1.0.2) |
| `GetTop(limit=50)` | Топ-N треков по числу прослушиваний → `ListeningStats(Track, PlayCount, LastPlayedUtc)`. Используется во вкладке «Топ-50» StatsWindow |
| `GetRecentUnique(limit=50)` | Последние N **уникальных** треков по времени последнего прослушивания. Вкладка «Недавние» StatsWindow |
| `GetNeverPlayed()` | Треки библиотеки без записей в истории. Вкладка «Ни разу не играли» |
| `Append(entry)` | Запись нового события прослушивания |

`SqliteListeningHistoryRepository` реализует агрегации через EF Core `GroupBy` + dictionary-lookup по `Tracks` (вместо `.Include` на грубой проекции — иначе EF не транслирует выражение).

## DI-композиция

Точка входа — [`MusicLibrary/App.xaml.cs`](../MusicLibrary/App.xaml.cs), метод `OnStartup`:

1. **Сборка host:** `Host.CreateDefaultBuilder()` с двумя расширениями:
   - `services.AddMusicBakhInfrastructure(LibraryDbContextOptions.Default)` — регистрирует `LibraryDbContext`, все SQLite-репозитории, HTTP-клиенты, файловые сервисы, сидер, `JsonToSqliteMigrationService`.
   - `services.AddMusicBakhPresentation()` — регистрирует ViewModels, `MainWindow`, WPF-сервисы (`MediaPlayerAudioService`, `ProceduralCoverGenerator`, диалоги).
2. **Инициализация БД** (`InitializeDatabase`):
   - `LibraryDbContext.Database.Migrate()` — создаёт SQLite-файл и применяет миграции.
   - `JsonToSqliteMigrationService.Run()` — переносит legacy `userTracks.json` в таблицу `Tracks` (идемпотентно).
   - `BuiltInTrackSeeder.SeedIfEmpty()` — заполняет встроенные треки, если таблица пуста.
3. **Открытие окна:** `_host.Services.GetRequiredService<MainWindow>().Show()`.

## Миграция с 1.0.0

[`JsonToSqliteMigrationService`](../MusicBakh.Infrastructure/Migration/JsonToSqliteMigrationService.cs) обеспечивает бесшовный апгрейд:

1. Проверяет наличие `%LocalAppData%\MusicLibrary\userTracks.json`.
2. Если файл найден — читает список `UserTrack`, вставляет записи в таблицу `Tracks` через `SqliteTrackRepository`.
3. Переименовывает исходный файл в `userTracks.json.backup-yyyyMMdd-HHmmss`.

После переименования повторный запуск приложения не находит JSON-файла и пропускает миграцию — операция идемпотентна. Резервный файл можно удалить вручную в любой момент.

## Слой ViewModel

[`MainViewModel`](../MusicLibrary/ViewModels/MainViewModel.cs) — центральный класс, держит:

- `ObservableCollection<Track> DisplayedTracks` — отфильтрованный список для UI.
- `ObservableCollection<PlaybackEntry> PlaybackHistory` — последние 50 запусков.
- Раздельные `SelectedTrack` (выделение в списке) и `PlayingTrack` (фактически играет). См. п. 7 в scope-deviations.
- `DispatcherTimer` (500 мс) обновляет `CurrentPosition` пока трек играет, кроме момента когда `IsSeeking = true`.

Команды (все `ICommand`):

| Команда | Описание |
|---|---|
| `PlayPauseCommand` | Старт/пауза, активна при `SelectedTrack != null` |
| `StopCommand` | Стоп, активна при `PlayingTrack != null` |
| `SaveTrackCommand` | Экспорт выбранного трека через `SaveFileDialog` |
| `AddTrackCommand` | Открывает окно импорта |
| `DeleteTrackCommand` | Удаление пользовательского трека (с подтверждением) |
| `PlayTrackCommand` | Запуск конкретного трека (double-click) |
| `ReplayHistoryEntryCommand` | Повтор трека из истории |
| `SkipForwardCommand` / `SkipBackwardCommand` | Перемотка ±10 с |
| `PreviousTrackCommand` / `NextTrackCommand` | Переход по `DisplayedTracks` |
| `ToggleMuteCommand` | Mute on/off |
| `CycleRepeatModeCommand` | NoRepeat → Current → Library → … |
| `SeekToCommand` | Перемотка на конкретную позицию |
| `SetRatingCommand` | Выставить/сбросить рейтинг (0..5★) на выбранном треке — с 1.0.3 |
| `SetReactionCommand` | Выставить/сбросить реакцию (Liked/Disliked) — с 1.0.3 |
| `AttachTagCommand` | Привязать тег к выбранному треку — с 1.0.3 |
| `DetachTagCommand` | Открепить тег от выбранного трека — с 1.0.3 |
| `OpenTagsWindowCommand` | Открыть окно «Теги» (Ctrl+G) — с 1.0.3 |
| `SetMinRatingCommand` | Установить фильтр «рейтинг не ниже N» в toolbar — с 1.0.3 |
| `SetReactionFilterCommand` | Установить фильтр по реакции в toolbar — с 1.0.3 |
| `ResetRatingReactionFilterCommand` | Сбросить фильтры рейтинга и реакции — с 1.0.3 |
| `ToggleTagFilterCommand` | Переключить активность тег-чипа в toolbar — с 1.0.3 |

## Пайплайн импорта

```
[Кнопка «+ Добавить трек»]
        │
        ▼
AddTrackWindow ─── выбор источника ─── LocalFileImportRequest | UrlImportRequest
        │
        ▼
TrackImporter.ImportAsync()
        ├── скачать/скопировать файл в %LocalAppData%\MusicLibrary\Music\
        ├── DefaultMetadataResolver.ResolveAsync()
        │       ├── TagLibSharpTagReader.Read()
        │       ├── StripBrandSuffix() против чёрного списка агрегаторов
        │       ├── MusicBrainzClient.SearchAsync() (throttle 1.1 с)
        │       └── RussianGenreNormalizer.Normalize()
        ├── CompositeCoverResolver.ResolveAsync()
        │       ├── APIC из ID3
        │       ├── ItunesCoverClient.FindAsync() (600×600)
        │       └── ProceduralCoverGenerator.Generate() (512×512 градиент)
        ▼
TrackImportCandidate ─── редактирование пользователем ─── SqliteTrackRepository.AddAsync()
```

Все HTTP-вызовы (MusicBrainz и iTunes) защищены per-request таймаутами по 10 секунд через `CancellationTokenSource`.

## Пайплайн воспроизведения

```
SelectedTrack ─── PlayPauseCommand ──▶ MediaPlayerAudioService.Open(filePath)
                                              │
                                              ▼
                                       MediaPlayer (System.Windows.Media)
                                              │
                                       ┌──────┴──────┬─────────────┐
                                       ▼             ▼             ▼
                                  MediaOpened   MediaEnded    MediaFailed
                                       │             │             │
                                       │     IPlaybackQueueStrategy │
                                       │             │             │
                                       ▼             ▼             ▼
                                  CurrentDuration  Auto-next   StatusMessage
                                  устанавливается  по режиму   с ошибкой
                                                   повтора
```

`DispatcherTimer` тикает каждые 500 мс и пишет `MediaPlayer.Position` в `CurrentPosition` ViewModel. Таймер не трогает значение пока пользователь тащит seek-слайдер (`IsSeeking == true`).

## Тестирование

Единственный тестовый проект — `MusicLibrary.Tests` (xUnit). По состоянию версии 1.0.4: **242 теста** в обычном прогоне + **1 бенчмарк** под трейтом `Category=Benchmark`.

- **SQLite-репозитории** (`SqliteTrackRepositoryTests`, `SqliteListeningHistoryRepositoryTests`, `SqlitePlayerSettingsRepositoryTests`) — работают поверх in-memory SQLite через [`InMemorySqliteDbContextFactory`](../MusicLibrary.Tests/TestSupport/InMemorySqliteDbContextFactory.cs) (`EnsureCreated` — обходит raw-SQL миграции).
- **FTS5-поиск** (`SqliteFtsSearchServiceTests`, `FtsQueryBuilderTests`) — `FtsQueryBuilder` юнит-тестируется напрямую; интеграционные тесты `SqliteFtsSearchService` работают поверх [`MigratedSqliteDbContextFactory`](../MusicLibrary.Tests/TestSupport/MigratedSqliteDbContextFactory.cs), которая зовёт `Database.Migrate()` — единственный способ прогнать raw-SQL миграции (виртуальная таблица + триггеры) в тестах.
- **Бенчмарк** (`SqliteFtsSearchServiceBenchmark`, `[Trait("Category", "Benchmark")]`) — сидит 50 000 случайных треков и проверяет средний поиск < 100 мс (DoD роадмапа 1.1.0). Запуск: `dotnet test --filter "Category=Benchmark"`. Обычный `dotnet test --filter "Category!=Benchmark"` его пропускает.
- **Миграция данных** (`JsonToSqliteMigrationServiceTests`) — проверяет перенос записей из JSON в SQLite и переименование файла.
- **Сидер** (`BuiltInTrackSeederTests`) — добавление в пустую БД, идемпотентность, refresh устаревших путей при переезде сборки (с 1.0.2).
- **ViewModel** (`MainViewModelTests`) — покрывает логику `MainViewModel` через test doubles, без поднятия WPF; с 1.0.2 включает тесты `SearchText` + комбинация фильтров; с 1.0.3 — тесты `SetMinRatingCommand`, команд рейтинга/реакции и bm25-порядка.
- **Теги** (`SqliteTagRepositoryTests`) — attach/detach, уникальность имени с учётом кириллицы — с 1.0.3.
- **OverflowChipPanel** (`OverflowChipPanelTests`) — измерение и раскладка чипов с overflow — с 1.0.3.
- **LibraryFilter** (`LibraryFilterTests`) — чистые unit-тесты фильтрации по рейтингу, реакции, тегам и комбинациям — с 1.0.3.
- **Сервисы** — `DefaultMetadataResolverTests`, `GenreNormalizerTests`, `PlaybackQueueStrategyTests`, `CompositeTrackRepositoryTests`, `FileServiceTests` и др.

## Внешний вид окна

[`NativeWindowAppearance.cs`](../MusicLibrary/NativeWindowAppearance.cs) через DWM API красит нативный caption-bar Windows в фирменный тёмно-фиолетовый (`#16161F`), выставляет светлый текст заголовка и тёмный border. Применяется в `SourceInitialized` главного окна, `AddTrackWindow`, `ConfirmationDialogWindow` и `StatsWindow`.

## Ресурсы и стили

Все XAML-словари лежат в `MusicLibrary/Resources/` и подключаются в [`App.xaml`](../MusicLibrary/App.xaml):

- `Colors.xaml`, `Brushes.xaml` — палитра.
- `ButtonStyles.xaml`, `ComboBoxStyles.xaml`, `TextBoxStyles.xaml`, `TabStyles.xaml`, `SliderStyles.xaml`, `ScrollBarStyles.xaml`, `ListStyles.xaml` — переопределение системных контролов. `TextBoxStyles.xaml` и `TabStyles.xaml` появились в 1.0.2 ради поля поиска и таб-контрола в `StatsWindow`.
- `PlayerIcons.xaml` — векторные иконки play/pause/skip/repeat/volume через `Geometry`.
- `TrackTemplates.xaml` — `DataTemplate` для карточки трека в `ListBox` + три stats-шаблона (`StatsTopItemTemplate`, `StatsRecentItemTemplate`, `StatsNeverPlayedItemTemplate`). С 1.0.3 карточка трека содержит чипы тегов через `OverflowChipPanel`.

## Сборка

См. [release-checklist.md](release-checklist.md). Краткий тех-список: `net10.0-windows`, `WinExe`, `UseWPF=true`, в Release-конфигурации `RuntimeIdentifier=win-x64`, `SelfContained=true`, `PublishSingleFile=true`, `EnableCompressionInSingleFile=true`, `PublishReadyToRun=false`.

## Уточнения после аудита 2026-09-18

- Очередь хранит позицию, а не только Id: повторения трека в плейлисте допустимы. Resume сохраняет снимок; отсутствующий файл не меняет очередь продолжающего играть трека.
- TogglePlaybackCommand управляет PlayingTrack независимо от SelectedTrack. Редактор в средней колонке относится к SelectedTrack; транспорт закреплён снизу. Команды/подсветка строк внутри LeftColumnState-шаблонов берутся с DataContext окна.
- Теги загружаются одной пакетной выборкой GetTagsByTrack; фильтрация использует кэш. После мутаций обновляются отображение и агрегаты; Back разрешает актуальное содержимое детали.
- Импорт владеет созданными копиями до подтверждения, поддерживает отмену и дедлайн 2 минуты; неудачные и отменённые кандидаты очищаются. Исходные файлы не удаляются. Callback сохранения вызывается до закрытия окна: ошибка БД оставляет кандидата для повторного сохранения, отмена очищает его копии. Аварийное завершение процесса может оставить файлы; автоматической очистки неизвестных файлов нет.
- Ожидаемые ошибки SQLite/файлов показываются пользователю. Ошибки истории/настроек не прерывают аудио. Локальный musicbakh.log ротируется при следующем запуске после 2 MiB, остаётся одна previous-копия; данные никуда не отправляются.
- WPF Application ставит OnStartup в очередь даже без Run. Render-тест создаёт App(startServices:false), загружает реальные ресурсы и обрабатывает UI-события без production host/БД.
- Старые JSON/composite-адаптеры не являются активным runtime-хранилищем. Миграционный путь сохранён для старых библиотек.
- Полный список изменений, проверок и оставшихся ограничений: [итог аудита](audit-2026-09-17.md), [журнал работы](hardening-2026-09-18.md).