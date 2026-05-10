# Архитектура MusicBakh

Документ описывает фактическое устройство приложения по состоянию исходного кода. 

## Обзор

- **Платформа:** WPF, .NET 10 (`net10.0-windows`), C# 14, nullable enabled, ImplicitUsings.
- **Архитектура:** лёгкий MVVM (View ↔ ViewModel ↔ сервисы), single-project. IoC-контейнер не используется — зависимости собираются вручную в [MainWindow.xaml.cs:20](../MusicLibrary/MainWindow.xaml.cs:20).
- **Внешний NuGet:** только `TagLibSharp 2.3.0`. Остальное — BCL: `System.Windows.Media.MediaPlayer`, `System.Text.Json`, `System.Net.Http`.
- **Точка входа:** [App.xaml.cs](../MusicLibrary/App.xaml.cs) задаёт `AppUserModelID = "MusicBakh.Player"` для корректной группировки в taskbar Windows, далее WPF поднимает `MainWindow` через `StartupUri`.

## Каталоги проекта

```
MusicLibrary/
├── App.xaml(.cs)                       — Application entry, AppUserModelID
├── MainWindow.xaml(.cs)                — DI-композиция, обработчики seek-слайдера
├── NativeWindowAppearance.cs           — DWM dark caption/border/text color
├── ViewModels/
│   ├── ViewModelBase.cs                — INotifyPropertyChanged base
│   ├── MainViewModel.cs                — главный VM, все ICommand
│   └── AddTrackViewModel.cs            — VM окна импорта
├── Views/
│   ├── AddTrackWindow.xaml(.cs)        — модальное окно импорта
│   ├── ConfirmationDialogWindow.xaml(.cs) — кастомный диалог подтверждения
│   └── ConfirmationDialogService.cs    — обёртка над диалогом
├── Models/
│   ├── Track.cs                        — модель трека
│   ├── UserTrack.cs                    — DTO для JSON
│   ├── TrackImportCandidate.cs         — результат импорта до сохранения
│   ├── PlaybackEntry.cs                — запись истории
│   ├── RepeatMode.cs                   — enum (NoRepeat/Current/Library)
│   └── OperationResult.cs              — результат операций UI
├── Services/
│   ├── Playback/                       — IAudioPlayerService + стратегии очереди
│   ├── Storage/                        — JSON-хранилища
│   ├── Import/                         — пайплайн импорта
│   ├── Metadata/                       — TagLib# + MusicBrainz
│   ├── Covers/                         — iTunes + процедурная
│   ├── Files/                          — диалоги и FS-операции
│   └── Tracks/                         — репозитории
├── Commands/RelayCommand.cs            — реализация ICommand
├── Converters/                         — IValueConverter для XAML
├── Resources/                          — XAML словари (Colors, Brushes, стили, иконки)
├── Assets/Brand/                       — musicbakh.ico, musicbakh-logo.png
├── Music/                              — три эталонных mp3 (копируются в output)
└── Covers/                             — обложки к эталонным трекам
```

## Слой ViewModel

[MainViewModel](../MusicLibrary/ViewModels/MainViewModel.cs) — центральный класс на ~970 строк, держит:

- `ObservableCollection<Track> DisplayedTracks` — отфильтрованный список для UI.
- `ObservableCollection<PlaybackEntry> PlaybackHistory` — последние 50 запусков.
- Раздельные `SelectedTrack` (выделение) и `PlayingTrack` (фактически играет). См. п. 7 в scope-deviations.
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

## Сервисы

| Слой | Файлы | Назначение |
|---|---|---|
| **Audio** | [MediaPlayerAudioService.cs](../MusicLibrary/Services/Playback/MediaPlayerAudioService.cs) | Обёртка над `System.Windows.Media.MediaPlayer`. Поднимает события `MediaOpened`, `MediaEnded`, `MediaFailed` |
| **Queue strategy** | [Services/Playback/IPlaybackQueueStrategy.cs](../MusicLibrary/Services/Playback/IPlaybackQueueStrategy.cs) + `NoRepeatStrategy`, `RepeatCurrentStrategy`, `RepeatLibraryStrategy` | Поведение при `MediaEnded` |
| **Storage** | [JsonUserTrackStorage.cs](../MusicLibrary/Services/Storage/JsonUserTrackStorage.cs), [JsonPlayerSettingsStorage.cs](../MusicLibrary/Services/Storage/JsonPlayerSettingsStorage.cs) | JSON-файлы в `%LocalAppData%`, `JsonNamingPolicy.CamelCase`, `WriteIndented` |
| **Import** | [TrackImporter.cs](../MusicLibrary/Services/Import/TrackImporter.cs), `LocalFileImportRequest`, `UrlImportRequest`, `ImportResult` | Копирование/скачивание `.mp3`/`.wav` (до 50 МБ), вызов метадата- и cover-резолверов |
| **Metadata** | [DefaultMetadataResolver.cs](../MusicLibrary/Services/Metadata/DefaultMetadataResolver.cs), `TagLibSharpTagReader`, `MusicBrainzClient`, `RussianGenreNormalizer` | Каскад тег → очистка суффиксов агрегаторов → MusicBrainz |
| **Covers** | [CompositeCoverResolver.cs](../MusicLibrary/Services/Covers/CompositeCoverResolver.cs), `ItunesCoverClient`, `ProceduralCoverGenerator` | Поиск обложки в iTunes (600×600), fallback на процедурный градиент с буквой |
| **Files** | `FileService`, `OpenFileDialogService`, `SaveFileDialogService`, `MessageBoxConfirmationService` (не используется), `ConfirmationDialogService` (используется) | Диалоги Windows и FS-операции |
| **Tracks** | [CompositeTrackRepository.cs](../MusicLibrary/Services/Tracks/CompositeTrackRepository.cs), `InMemoryTrackRepository` | Объединяет 3 эталонных трека из приложения и пользовательские из JSON |

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
TrackImportCandidate ─── редактирование пользователем ─── JsonUserTrackStorage.Append()
```

Все HTTP-вызовы (MusicBrainz и iTunes) защищены per-request таймаутами по 10 секунд через `CancellationTokenSource`. Глобальный `HttpClient.Timeout` оставлен дефолтным, чтобы не мешать скачиванию большого аудиофайла по URL.

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

`DispatcherTimer` тикает каждые 500 мс и пишет `MediaPlayer.Position` в `CurrentPosition` ViewModel — этот же таймер не трогает значение пока пользователь тащит seek-слайдер (`IsSeeking == true`).

## Хранилище

| Путь | Содержимое |
|---|---|
| `%LocalAppData%\MusicLibrary\userTracks.json` | Список `UserTrack` (Id, Title, Artist, Genre, DurationSeconds, FilePath, CoverPath) |
| `%LocalAppData%\MusicLibrary\Music\` | Импортированные mp3/wav |
| `%LocalAppData%\MusicLibrary\Covers\` | Обложки с GUID в имени (`{uuid}.{ext}`) |
| `%LocalAppData%\MusicBakh\player-settings.json` | `{ Volume, IsMuted, RepeatMode }` |

Эталонные 3 трека и их обложки лежат рядом с .exe (`{app}\Music\`, `{app}\Covers\`) — они read-only и удалить их через UI нельзя (соответствующая команда отключена по `FilePath`).

## Внешний вид окна

[NativeWindowAppearance.cs](../MusicLibrary/NativeWindowAppearance.cs) через DWM API красит нативный caption-bar Windows в фирменный тёмно-фиолетовый (`#16161F`) и выставляет светлый текст заголовка. Применяется в `SourceInitialized` главного окна и `ConfirmationDialogWindow`. Без этого Windows рисует caption по системной теме, и тёмное окно с белой шапкой выглядит ломано.

## Ресурсы и стили

Все XAML-словари лежат в `MusicLibrary/Resources/` и подключаются в [App.xaml](../MusicLibrary/App.xaml):

- `Colors.xaml`, `Brushes.xaml` — палитра.
- `ButtonStyles.xaml`, `ComboBoxStyles.xaml`, `SliderStyles.xaml`, `ScrollBarStyles.xaml`, `ListStyles.xaml` — переопределение системных контролов.
- `PlayerIcons.xaml` — векторные иконки play/pause/skip/repeat/volume через `Geometry`.
- `TrackTemplates.xaml` — `DataTemplate` для карточки трека в `ListBox`.

## Сборка

См. [release-checklist.md](release-checklist.md). Краткий тех-список: `net10.0-windows`, `WinExe`, `UseWPF=true`, в Release-конфигурации `RuntimeIdentifier=win-x64`, `SelfContained=true`, `PublishSingleFile=true`, `EnableCompressionInSingleFile=true`, `PublishReadyToRun=false`.
