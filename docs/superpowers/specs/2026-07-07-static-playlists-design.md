# Спек: Статические плейлисты

**Дата:** 2026-07-07
**Статус:** утверждён в брейншторминге, ждёт плана реализации
**Версия:** `v1.0.5` (итерация E эпика «Library 2.0»)

## Контекст

После итераций A–D приложение хранит треки в SQLite (`1.0.1`), умеет FTS5-поиск и историю (`1.0.2`), рейтинги/реакции/теги-ярлыки (`1.0.3`) и показывает библиотеку в трёх видах — Треки/Альбомы/Исполнители — с drill-down в детальные страницы (`1.0.4`). Левая колонка — `ContentControl` с `LeftColumnTemplateSelector`, переключающим DataTemplate по `LeftColumnState`; вкладки в шапке переключают `MainViewMode`. Воспроизведение с `1.0.4` использует отдельную очередь `_playbackQueue` (отвязанную от списка-вида `DisplayedTracks`).

Roadmap [`docs/roadmap-vision.md`](../../roadmap-vision.md) §4.1: `1.0.5` (итерация E) — «Статические плейлисты с drag-and-drop». Умные плейлисты по правилам — это итерация F (`1.0.6`), вне скоупа E. Импорт/экспорт `.m3u8`/`.pls` сознательно отложен на follow-up после E (см. §Сознательные ограничения).

Проект перешёл из учебного в личный — временных ограничений нет, поэтому в скоуп E входят и «полированные» части (мульти-выбор DnD, инлайн-редактирование имени, переиспользуемый render-smoke-харнесс).

## Цель

1. Добавить **четвёртый вид «Плейлисты»** равноправно с Треки/Альбомы/Исполнители — на уже построенной механике вкладок + drill-down (итерация D).
2. Дать полный CRUD статических (ручных) плейлистов: создать, переименовать, удалить, наполнить, переупорядочить, воспроизвести.
3. Ввести **контекстное меню** (первое в приложении) для «Добавить в плейлист» и управления плейлистами.
4. Ввести **drag-and-drop**: добавление треков в плейлист перетаскиванием (в т.ч. мульти-выбор) и переупорядочивание треков внутри плейлиста.
5. Переиспользовать очередь `_playbackQueue` для воспроизведения плейлиста, не трогая вкладку «Треки».
6. Закрыть урок итерации D: завести **переиспользуемый headless render-smoke** тест, инстанцирующий все left-column-шаблоны (включая новые), чтобы XAML-крэши на переключении вида ловились в CI.

## Структура экрана

Шапка (`Grid.Row=0`) расширяется до четырёх табов:

```
🎵 MusicBakh │ [Треки] [Альбомы] [Исполнители] [Плейлисты] │ Теги · Стат. · +
```

Активный таб подсвечен золотом (стиль `MainViewTabsStyles`). Хоткеи: `Ctrl+1`/`Ctrl+2`/`Ctrl+3`/`Ctrl+4`. Старт приложения — всегда на «Треки» (как в `1.0.4`).

Левая колонка по вкладке «Плейлисты» — два состояния (через `LeftColumnState`):
- **PlaylistsRoot** — список плейлистов.
- **PlaylistDetail(Playlist)** — треклист выбранного плейлиста.

## Контент вкладки «Плейлисты»

### Корень (PlaylistsRoot)

**Layout:** вертикальный список строк (`ItemsControl`/`ListBox` со `StackPanel` в `ItemsPanel`), сверху кнопка **«+ Новый плейлист»**.

**Строка плейлиста:**
- Иконка/маркер плейлиста (простой значок ♪ или сгенерированный акцент; обложка-коллаж — вне скоупа E).
- Имя плейлиста (SemiBold, 14).
- Ниже — «N треков · длительность» приглушённо (FontSize 11).
- Cursor=Hand, левый клик = drill-down в детальную страницу.
- Правый клик = контекст-меню «Переименовать» / «Удалить».
- Целевой объект drop при DnD (см. §Drag-and-drop).

**Сортировка строк:** по `CreatedAtUtc DESC` (свежесозданные сверху). Пользовательская сортировка/пиннинг — вне скоупа E.

**Empty-state:** «Плейлистов пока нет — создайте первый» (центрированно, серым).

**Создание (инлайн):** клик по «+ Новый плейлист» → в начало списка вставляется строка в режиме редактирования (`TextBox` в фокусе, дефолт «Новый плейлист»). `Enter` — создать (вызов `IPlaylistRepository.Create(name)`), `Esc` — отменить. Пустое имя при `Enter` → «Новый плейлист» или отмена (дефолт: сохраняем «Новый плейлист»).

**Переименование (инлайн):** контекст-меню «Переименовать» на строке → та же строка переходит в режим редактирования с текущим именем. `Enter` — `Rename(id, name)`, `Esc` — отмена.

**Удаление:** контекст-меню «Удалить» → брендированный `ConfirmationDialogService` («Удалить плейлист «X»? Треки в библиотеке останутся.») → `Delete(id)`. Удаляется только плейлист и его связи; сами треки в библиотеке не трогаются.

### Детальная страница (PlaylistDetail)

**Layout** (по образцу AlbumDetail):
```
← Все плейлисты
  Мой микс
  12 треков · 48 мин
  [▶ Воспроизвести]  [🔀 Перемешать]

1.  ▶ Night Witches            4:03
2.    No Bullets Fly            4:31
...
```
- «← Все плейлисты» / `Esc` — назад в PlaylistsRoot.
- Header: имя плейлиста (18, SemiBold), «N треков · длительность» приглушённо.
- Кнопки **«▶ Воспроизвести»** (очередь = треки плейлиста, старт первого) и **«🔀 Перемешать»**.
- Треклист: строки `«№. Название   Длительность»`. Играющий трек подсвечен золотом (тот же `TrackIdentityMatchConverter` + `PlayingTrack` через `AncestorType=Window`, как в AlbumDetail). Двойной клик = воспроизвести с этой позиции (очередь = треки плейлиста).
- Позиция трека — по `PlaylistTracks.Position` (ручной порядок).
- Правый клик по треку → контекст-меню «Убрать из плейлиста» (+ «Добавить в плейлист ▸» для добавления в другой плейлист).
- **DnD reorder:** перетаскивание строки вверх/вниз внутри треклиста меняет `Position`; insertion-линия показывает точку вставки.
- **Empty-state:** «В плейлисте пока нет треков — добавьте их через правый клик или перетаскиванием».

## Доменные изменения

### `MusicBakh.Core.Domain`

Новая сущность:
```csharp
public sealed record Playlist(int Id, string Name, DateTime CreatedAtUtc);
```

`MainViewMode` (enum) получает значение `Playlists` (после `Artists`).

Агрегат треков плейлиста — computed при чтении (аналогично альбомам/исполнителям, не first-class модель с навигационными свойствами в домене). Для UI используется лёгкий DTO `PlaylistView` (имя намеренно НЕ `PlaylistDetail`, чтобы не пересекаться с вариантом `LeftColumnState.PlaylistDetail`):
```csharp
public sealed record PlaylistView(Playlist Playlist, IReadOnlyList<Track> Tracks)
{
    public TimeSpan TotalDuration { get; } // сумма Tracks[].Duration
}
```
`LeftColumnState.PlaylistDetail` оборачивает `PlaylistView`: `PlaylistDetail(PlaylistView Playlist)`.

### `MusicBakh.Core.Abstractions`

```csharp
public interface IPlaylistRepository
{
    IReadOnlyList<Playlist> GetAll();                        // CreatedAtUtc DESC
    Playlist? GetById(int id);
    Playlist Create(string name);                            // CreatedAtUtc = clock.UtcNow
    void Rename(int id, string name);
    void Delete(int id);                                     // + каскад связей
    IReadOnlyList<Track> GetTracks(int playlistId);          // по Position ASC
    void AddTrack(int playlistId, int trackId);              // в конец (Position = max+1)
    void AddTracks(int playlistId, IReadOnlyList<int> trackIds); // батч, сохраняя порядок
    void RemoveAt(int playlistId, int position);             // удалить по позиции + сжать
    void Reorder(int playlistId, int fromPosition, int toPosition);
    int GetTrackCount(int playlistId);
}
```
`Create` использует `IClock` (из итерации A) для `CreatedAtUtc` — тестируемость.

### EF Core (`Infrastructure`)

Две сущности:
```csharp
internal sealed class PlaylistEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}

internal sealed class PlaylistTrackEntity
{
    public int Id { get; set; }           // собственный PK — дубликаты разрешены
    public int PlaylistId { get; set; }
    public int TrackId { get; set; }
    public int Position { get; set; }     // 0-based ручной порядок

    public PlaylistEntity Playlist { get; set; } = default!;
    public TrackEntity Track { get; set; } = default!;
}
```

**Конфигурация:**
- `Playlists`: PK `Id`, `Name` required maxLength 200, индекс на `CreatedAtUtc`.
- `PlaylistTracks`: PK `Id`; FK `PlaylistId → Playlists` **cascade delete**; FK `TrackId → Tracks` **cascade delete** (удаление трека из библиотеки убирает его из всех плейлистов); индекс `(PlaylistId, Position)`.

**Миграция `AddPlaylists`** — обязательный alias-фикс `EFMigration` (см. `CLAUDE.md`). Additive: две новые таблицы, существующие данные не трогаются.

**`SqlitePlaylistRepository`** поверх `Func<LibraryDbContext>` (как остальные репозитории). Работа с `Position`:
- `AddTrack` — `Position = (max Position в плейлисте) + 1`, или 0 для пустого.
- `RemoveAt` — удалить строку на позиции, декрементировать `Position` всех строк после неё (сжатие «дыры»).
- `Reorder(from, to)` — вынуть строку, сдвинуть промежуток, переприсвоить `Position` подряд. Транзакция.
- `GetTracks` — JOIN `PlaylistTracks` → `Tracks` по `Position ASC`, маппинг в доменный `Track` (пробрасывать ВСЕ поля включая `IsBuiltIn/Year/TrackNumber/AlbumArtist/Rating/Reaction` — контрактный инвариант из прошлых итераций).

**DI:** `IPlaylistRepository → SqlitePlaylistRepository` в `InfrastructureServiceCollectionExtensions`.

## Контекстное меню (первое в приложении)

Вводится `ContextMenu` на строках треков во всех списках (Треки/AlbumDetail/ArtistDetail/PlaylistDetail):

- **«Добавить в плейлист ▸»** — подменю: список существующих плейлистов (динамический, по `IPlaylistRepository.GetAll()`) + разделитель + **«Новый плейлист…»** (создаёт плейлист и сразу добавляет трек).
- В PlaylistDetail дополнительно: **«Убрать из плейлиста»**.

Строки плейлистов в PlaylistsRoot: `ContextMenu` с **«Переименовать»** / **«Удалить»**.

**Реализация:** WPF `ContextMenu` с `MenuItem`. Команды — на `MainViewModel`, источник через `PlacementTarget`/`RelativeSource` (ContextMenu вне визуального дерева окна — стандартная особенность; используем `Tag`/`PlacementTarget.DataContext` или `ContextMenu.DataContext`-проброс). Динамическое подменю плейлистов — `ItemsSource` на коллекцию плейлистов + `MenuItem` через `ItemContainerStyle`/`HierarchicalDataTemplate`, команда `AddSelectedToPlaylistCommand(playlistId)`.

Команды `MainViewModel`: `CreatePlaylistCommand`, `RenamePlaylistCommand`, `DeletePlaylistCommand`, `OpenPlaylistCommand`, `PlayPlaylistCommand`, `ShufflePlaylistCommand`, `AddTrackToPlaylistCommand(playlist, track)`, `RemoveTrackFromPlaylistCommand(position)`, `PlayPlaylistTrackCommand(track)`.

## Drag-and-drop

Первый DnD в приложении. Требует аккуратной реализации (урок D — XAML/interaction-баги не ловятся build'ом).

**Сценарии:**
1. **Добавление:** drag трека(ов) из любого списка → drop на строку плейлиста в PlaylistsRoot **или** в треклист открытого PlaylistDetail → `AddTracks(playlistId, ids)` в конец.
2. **Reorder:** drag строки трека **внутри** PlaylistDetail → drop между строк → `Reorder(from, to)`.

**Мульти-выбор:** списки треков переводятся в `SelectionMode=Extended` (Ctrl/Shift). Drag тащит весь выбор; drop добавляет все в порядке выбора.

**Механика (WPF):**
- Инициация: `PreviewMouseLeftButtonDown` фиксирует точку + элемент; `PreviewMouseMove` при превышении `SystemParameters.MinimumHorizontal/VerticalDragDistance` запускает `DragDrop.DoDragDrop` с `DataObject` (список `Track`/track-id).
- Drop-таргеты: `AllowDrop=True`, `DragOver` (эффект Copy/Move + drop-хайлайт целевой строки или insertion-adorner для reorder), `Drop` (выполнить операцию).
- **Adorner'ы:** insertion-линия при reorder (тонкий `AdornerLayer`-визуал), подсветка целевого плейлиста при добавлении.
- **Автоскролл** при drag у краёв длинного списка.
- Инкапсулировать DnD-логику в переиспользуемые attached-behaviours / helper-класс (`MusicLibrary/Behaviors/` или `Controls/`), а не размазывать по code-behind — чтобы reorder и add переиспускали общий код и тестировались изолированно, где возможно.

## Воспроизведение

Переиспускает `_playbackQueue` из `1.0.4`:
- `PlayPlaylistCommand` / `ShufflePlaylistCommand` → `ReplaceQueueAndPlay(playlist.Tracks, shuffle)`. Вкладка «Треки» и `DisplayedTracks` не трогаются.
- Двойной клик по треку в PlaylistDetail → `PlayPlaylistTrackCommand`: очередь = треки плейлиста, старт с кликнутого (по образцу `PlayDetailTrackCommand` для альбома/исполнителя).
- Играющий трек подсвечивается в PlaylistDetail (как в AlbumDetail).

## ViewModel

`MainViewModel`:
```csharp
public IReadOnlyList<Playlist> Playlists { get; private set; }  // computed-кэш, рефреш при CRUD
// команды из §Контекстное меню
```
`Playlists` пересчитывается при create/rename/delete и при первом обращении. Drill-down в PlaylistDetail строит `PlaylistView`(имя + треки через `GetTracks`) и кладёт в `CurrentLeftColumn`. При изменении содержимого открытого плейлиста (add/remove/reorder) — ре-резолв `CurrentLeftColumn` на свежий `PlaylistView` (по образцу фикса stale-drill-down из D, ApplyFilters-паттерн, но триггер — мутация плейлиста).

**Фильтры и плейлисты:** активный фильтр библиотеки (search/genre/rating/…) на содержимое плейлиста НЕ влияет (плейлист — явный ручной набор, не подмножество библиотечного фильтра). Это отличается от альбомов/исполнителей (там фильтр действует). Зафиксировать в scope-deviations.

## XAML-структура

Новый ресурс-словарь `MusicLibrary/Resources/PlaylistsTemplates.xaml` (по образцу `AlbumsArtistsTemplates.xaml`):
- `PlaylistsRootTemplate` — список + «+ Новый плейлист» + инлайн-edit + empty-state.
- `PlaylistRowTemplate` — строка плейлиста (имя, счётчики, контекст-меню, drop-таргет).
- `PlaylistDetailTemplate` — back + header + Play/Shuffle + треклист (контекст-меню, DnD-reorder, подсветка) + empty-state.
- Конвертеры (`CountToVisibilityConverter`, `TrackIdentityMatchConverter` и др.) объявить **локально** в этом словаре (урок D: `{StaticResource}` в merged-словаре не видит ресурсы, объявленные после MergedDictionaries).

`LeftColumnState` (+ варианты `PlaylistsRoot`, `PlaylistDetail`), `LeftColumnTemplateSelector` (+ маппинг), `MainViewMode.Playlists`, 4-й таб в `MainWindow.xaml` + `MainViewTabsStyles`, регистрация словаря в `App.xaml` (после других шаблонных словарей).

## Хоткеи

- `Ctrl+4` — вкладка «Плейлисты».
- `Esc` в PlaylistDetail — назад (тот же приоритет с поиском, что в D).
- `Delete` на выбранном треке в PlaylistDetail — «Убрать из плейлиста» (опционально, если ложится чисто).

## Сознательные ограничения (в `scope-deviations.md` §1.0.5)

- **Import/export `.m3u8`/`.pls`** — отложено на follow-up после E.
- **Умные плейлисты по правилам** — итерация F (`1.0.6`).
- **Обложка плейлиста (коллаж из обложек треков)** — не в E; строка плейлиста использует простой значок.
- **Сортировка/пиннинг плейлистов** — только `CreatedAtUtc DESC`; пользовательский порядок плейлистов вне E.
- **Фильтр библиотеки не применяется** к содержимому плейлиста (в отличие от альбомов/исполнителей) — плейлист это явный ручной набор.
- **Вложенные плейлисты / папки** — нет.
- **«Играть следующим» / очередь-как-редактируемый-объект** — вне E (playback-очередь остаётся снимком).

## Тестирование

- **`SqlitePlaylistRepositoryTests`** (поверх in-memory SQLite): CRUD; `GetTracks` порядок по Position; `AddTrack`/`AddTracks` в конец; дубликаты (один трек дважды → две строки); `RemoveAt` + сжатие позиций; `Reorder` (вверх/вниз, границы); каскад — удаление трека из библиотеки убирает его из плейлистов, удаление плейлиста чистит связи; round-trip всех полей `Track` (включая `IsBuiltIn`/новые поля).
- **ViewModel-тесты**: create/rename/delete обновляют `Playlists`; add/remove/reorder ре-резолвят открытый `PlaylistDetail`; `PlayPlaylistCommand` наполняет `_playbackQueue` не трогая `DisplayedTracks`; двойной клик по треку плейлиста стартует с позиции.
- **DnD**: логику add/reorder вынести в тестируемый helper; unit-тесты на вычисление целевой позиции reorder и на сборку батча из мульти-выбора. Сам WPF-DnD (adorners/эффекты) — визуальный smoke.
- **Переиспользуемый headless render-smoke** (новый, закрывает урок D): STA-тест, который поднимает `Application`, мёржит ресурс-словари приложения, инстанцирует каждый `LeftColumnState`-шаблон (Tracks/Albums/Artists/AlbumDetail/ArtistDetail/**PlaylistsRoot/PlaylistDetail**) с реальным `MainViewModel` на seed-данных, форсит Measure/Arrange и утверждает отсутствие исключений. Ловит `ResourceReferenceKeyNotFoundException` и подобные XAML-крэши в CI.
- **Бенчмарк FTS** не должен просесть.

## Definition of Done

- Вкладка «Плейлисты» (Ctrl+4) переключает левую колонку; старт всегда на «Треки».
- PlaylistsRoot: список, «+ Новый плейлист» с инлайн-вводом, empty-state; переименование/удаление через контекст-меню (удаление — с подтверждением).
- Drill-down в PlaylistDetail: header, Play/Shuffle, треклист с подсветкой играющего, двойной клик играет, «← Назад»/Esc.
- Контекст-меню «Добавить в плейлист ▸» работает из всех списков треков; «Убрать из плейлиста» в PlaylistDetail.
- DnD: добавление треков (в т.ч. мульти-выбор) перетаскиванием на плейлист/в треклист; reorder перетаскиванием внутри плейлиста с insertion-визуалом.
- Дубликаты разрешены; каскад при удалении трека/плейлиста; порядок сохраняется между запусками.
- Воспроизведение плейлиста через `_playbackQueue`, вкладка «Треки» не ломается.
- Апгрейд с 1.0.4: миграция `AddPlaylists` накатывается без потери данных.
- Существующие 253 теста проходят + новые (репозиторий, ViewModel, render-smoke); render-smoke-харнесс переиспользуем в CI.
- Build чистый; ручной smoke пройден.
