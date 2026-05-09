# Interactive Playback Controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the second MusicBakh UX package: active seek slider, ±10s skip, prev/next track navigation, volume + mute, repeat-mode toggle (Off/Current/Library), hotkeys, and persistence of player settings between launches.

**Architecture:** `IAudioPlayerService` gains `Volume` and `IsMuted` properties (proxies onto `MediaPlayer`). A new `IPlaybackQueueStrategy` interface with three stateless singleton implementations decides what plays after `MediaEnded`, dispatched by `MainViewModel` according to `RepeatMode`. A new `IPlayerSettingsStorage` writes a small JSON file in `%LocalAppData%\MusicBakh\` so volume, mute and repeat-mode survive restarts. The seek slider is anti-feedback-protected with an `IsSeeking` flag toggled by `Thumb.DragStarted` and `PreviewMouseUp` on the slider.

**Tech Stack:** .NET 10, C# 14, WPF, XAML `Window.InputBindings`, XAML `Slider` with `Thumb` events, `System.Text.Json` with `JsonStringEnumConverter`, xUnit, existing `RelayCommand`, existing `IAudioPlayerService`.

**Spec:** `docs/superpowers/specs/2026-05-10-interactive-playback-controls-design.md`

---

## File Map

```text
MusicLibrary/Models/RepeatMode.cs                                 (create)
MusicLibrary/Services/Playback/IPlaybackQueueStrategy.cs          (create)
MusicLibrary/Services/Playback/NoRepeatStrategy.cs                (create)
MusicLibrary/Services/Playback/RepeatCurrentStrategy.cs           (create)
MusicLibrary/Services/Playback/RepeatLibraryStrategy.cs           (create)
MusicLibrary/Services/Storage/PlayerSettings.cs                   (create)
MusicLibrary/Services/Storage/IPlayerSettingsStorage.cs           (create)
MusicLibrary/Services/Storage/JsonPlayerSettingsStorage.cs        (create)
MusicLibrary/Resources/PlayerIcons.xaml                           (create)
MusicLibrary.Tests/PlaybackQueueStrategyTests.cs                  (create)
MusicLibrary.Tests/JsonPlayerSettingsStorageTests.cs              (create)

MusicLibrary/Services/Playback/IAudioPlayerService.cs             (modify)
MusicLibrary/Services/Playback/MediaPlayerAudioService.cs         (modify)
MusicLibrary/ViewModels/MainViewModel.cs                          (modify)
MusicLibrary/MainWindow.xaml                                      (modify)
MusicLibrary/MainWindow.xaml.cs                                   (modify)
MusicLibrary/App.xaml                                             (modify)
MusicLibrary/App.xaml.cs                                          (modify)
MusicLibrary.Tests/MainViewModelTests.cs                          (modify)
MusicLibrary/work_diff.md                                         (modify)
```

Responsibilities:

- `RepeatMode.cs` — enum `{Off, Current, Library}` shared between ViewModel, storage, and strategies.
- `IPlaybackQueueStrategy.cs` and three strategies — pure functions deciding the next track after `MediaEnded`. Stateless singletons.
- `PlayerSettings.cs` — record with `Volume`, `IsMuted`, `RepeatMode` plus `Default`.
- `IPlayerSettingsStorage.cs` / `JsonPlayerSettingsStorage.cs` — JSON read/write at `%LocalAppData%\MusicBakh\player-settings.json`. `Load()` never throws.
- `IAudioPlayerService.cs` / `MediaPlayerAudioService.cs` — extend with `Volume` (clamped 0..1) and `IsMuted`, both proxying to `System.Windows.Media.MediaPlayer`.
- `PlayerIcons.xaml` — Path geometries for transport, volume, mute, and repeat icons.
- `MainViewModel.cs` — new commands and state, strategy-based `MediaEnded` dispatch, `IsSeeking` anti-feedback flag, persistence on every property change.
- `MainWindow.xaml` / `MainWindow.xaml.cs` — new player bar in the center column, two slider event handlers, and `Window.InputBindings` for hotkeys.
- `App.xaml.cs` — composition root: load settings, apply to player, inject storage into ViewModel.
- `MainViewModelTests.cs` — extend the fake player and add tests for the new commands and dispatch logic.
- `work_diff.md` — append §11 documenting the package.

All comments inside C# and XAML files are written in Russian to match the existing codebase. Identifier names stay English.

---

## Task 1: RepeatMode Enum

**Files:**
- Create: `MusicLibrary/Models/RepeatMode.cs`

- [ ] **Step 1: Create the enum file**

Create `MusicLibrary/Models/RepeatMode.cs`:

```csharp
namespace MusicLibrary.Models;

/// <summary>
/// Режим повтора при завершении трека.
/// Off    — после текущего трека воспроизведение останавливается.
/// Current — текущий трек запускается заново.
/// Library — играет следующий трек из видимого списка, в конце возвращается к первому.
/// </summary>
public enum RepeatMode
{
    Off,
    Current,
    Library
}
```

- [ ] **Step 2: Build to verify the file compiles**

Run:

```powershell
dotnet build .\MusicLibrary\MusicLibrary.csproj
```

Expected:

```text
Сборка успешно завершена.
    Предупреждений: 0
    Ошибок: 0
```

- [ ] **Step 3: Commit**

```powershell
git add .\MusicLibrary\Models\RepeatMode.cs
git commit -m "feat: add RepeatMode enum"
```

---

## Task 2: Playback Queue Strategies (TDD)

**Files:**
- Create: `MusicLibrary.Tests/PlaybackQueueStrategyTests.cs`
- Create: `MusicLibrary/Services/Playback/IPlaybackQueueStrategy.cs`
- Create: `MusicLibrary/Services/Playback/NoRepeatStrategy.cs`
- Create: `MusicLibrary/Services/Playback/RepeatCurrentStrategy.cs`
- Create: `MusicLibrary/Services/Playback/RepeatLibraryStrategy.cs`

- [ ] **Step 1: Write the failing strategy tests**

Create `MusicLibrary.Tests/PlaybackQueueStrategyTests.cs`:

```csharp
using MusicLibrary.Models;
using MusicLibrary.Services.Playback;

namespace MusicLibrary.Tests;

public sealed class PlaybackQueueStrategyTests
{
    private static readonly Track A = new() { Id = 1, Title = "A", Artist = "X", Genre = "Рок", FilePath = "a.mp3" };
    private static readonly Track B = new() { Id = 2, Title = "B", Artist = "X", Genre = "Рок", FilePath = "b.mp3" };
    private static readonly Track C = new() { Id = 3, Title = "C", Artist = "X", Genre = "Рок", FilePath = "c.mp3" };
    private static readonly Track Outside = new() { Id = 99, Title = "Z", Artist = "X", Genre = "Джаз", FilePath = "z.mp3" };

    private static readonly IReadOnlyList<Track> Three = new[] { A, B, C };
    private static readonly IReadOnlyList<Track> One = new[] { A };
    private static readonly IReadOnlyList<Track> Empty = Array.Empty<Track>();

    [Fact]
    public void NoRepeat_AtMiddle_ReturnsNextByIndex()
    {
        Assert.Equal(B, NoRepeatStrategy.Instance.GetNext(A, Three));
    }

    [Fact]
    public void NoRepeat_AtLast_ReturnsNull()
    {
        Assert.Null(NoRepeatStrategy.Instance.GetNext(C, Three));
    }

    [Fact]
    public void NoRepeat_EmptyList_ReturnsNull()
    {
        Assert.Null(NoRepeatStrategy.Instance.GetNext(A, Empty));
    }

    [Fact]
    public void NoRepeat_CurrentNotInList_ReturnsNull()
    {
        Assert.Null(NoRepeatStrategy.Instance.GetNext(Outside, Three));
    }

    [Fact]
    public void RepeatCurrent_AlwaysReturnsCurrent_EvenIfNotInList()
    {
        Assert.Equal(Outside, RepeatCurrentStrategy.Instance.GetNext(Outside, Three));
        Assert.Equal(B, RepeatCurrentStrategy.Instance.GetNext(B, Empty));
    }

    [Fact]
    public void RepeatLibrary_AtMiddle_ReturnsNextByIndex()
    {
        Assert.Equal(B, RepeatLibraryStrategy.Instance.GetNext(A, Three));
    }

    [Fact]
    public void RepeatLibrary_AtLast_WrapsToFirst()
    {
        Assert.Equal(A, RepeatLibraryStrategy.Instance.GetNext(C, Three));
    }

    [Fact]
    public void RepeatLibrary_SingleTrackList_ReturnsSameTrack()
    {
        Assert.Equal(A, RepeatLibraryStrategy.Instance.GetNext(A, One));
    }

    [Fact]
    public void RepeatLibrary_EmptyList_ReturnsNull()
    {
        Assert.Null(RepeatLibraryStrategy.Instance.GetNext(A, Empty));
    }

    [Fact]
    public void RepeatLibrary_CurrentNotInList_ReturnsFirst()
    {
        Assert.Equal(A, RepeatLibraryStrategy.Instance.GetNext(Outside, Three));
    }
}
```

- [ ] **Step 2: Run tests and verify compile failure**

Run:

```powershell
dotnet test .\MusicLibrary.Tests\MusicLibrary.Tests.csproj --filter PlaybackQueueStrategyTests
```

Expected:

```text
Failed!
CS0246: The type or namespace name 'IPlaybackQueueStrategy' / 'NoRepeatStrategy' / 'RepeatCurrentStrategy' / 'RepeatLibraryStrategy' could not be found
```

- [ ] **Step 3: Create the interface**

Create `MusicLibrary/Services/Playback/IPlaybackQueueStrategy.cs`:

```csharp
using MusicLibrary.Models;

namespace MusicLibrary.Services.Playback;

/// <summary>
/// Решает, какой трек запускается после завершения текущего.
/// Реализации не хранят состояние и используются как singleton'ы.
/// </summary>
public interface IPlaybackQueueStrategy
{
    Track? GetNext(Track current, IReadOnlyList<Track> displayedTracks);
}
```

- [ ] **Step 4: Implement NoRepeatStrategy**

Create `MusicLibrary/Services/Playback/NoRepeatStrategy.cs`:

```csharp
using MusicLibrary.Models;

namespace MusicLibrary.Services.Playback;

/// <summary>
/// Базовый auto-next: следующий трек по индексу, после последнего — null (стоп).
/// </summary>
public sealed class NoRepeatStrategy : IPlaybackQueueStrategy
{
    public static NoRepeatStrategy Instance { get; } = new();

    private NoRepeatStrategy() { }

    public Track? GetNext(Track current, IReadOnlyList<Track> displayedTracks)
    {
        int index = IndexOfById(current, displayedTracks);
        if (index < 0 || index + 1 >= displayedTracks.Count)
        {
            return null;
        }
        return displayedTracks[index + 1];
    }

    private static int IndexOfById(Track track, IReadOnlyList<Track> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Id == track.Id)
            {
                return i;
            }
        }
        return -1;
    }
}
```

- [ ] **Step 5: Implement RepeatCurrentStrategy**

Create `MusicLibrary/Services/Playback/RepeatCurrentStrategy.cs`:

```csharp
using MusicLibrary.Models;

namespace MusicLibrary.Services.Playback;

/// <summary>
/// Зацикливание текущего трека: всегда возвращаем тот же объект.
/// </summary>
public sealed class RepeatCurrentStrategy : IPlaybackQueueStrategy
{
    public static RepeatCurrentStrategy Instance { get; } = new();

    private RepeatCurrentStrategy() { }

    public Track? GetNext(Track current, IReadOnlyList<Track> displayedTracks) => current;
}
```

- [ ] **Step 6: Implement RepeatLibraryStrategy**

Create `MusicLibrary/Services/Playback/RepeatLibraryStrategy.cs`:

```csharp
using MusicLibrary.Models;

namespace MusicLibrary.Services.Playback;

/// <summary>
/// Зацикливание видимого списка: после последнего трека возвращаемся к первому.
/// Если current не найден в списке — отдаём первый элемент (например, после смены фильтра).
/// </summary>
public sealed class RepeatLibraryStrategy : IPlaybackQueueStrategy
{
    public static RepeatLibraryStrategy Instance { get; } = new();

    private RepeatLibraryStrategy() { }

    public Track? GetNext(Track current, IReadOnlyList<Track> displayedTracks)
    {
        if (displayedTracks.Count == 0)
        {
            return null;
        }

        int index = IndexOfById(current, displayedTracks);
        if (index < 0)
        {
            return displayedTracks[0];
        }

        int nextIndex = (index + 1) % displayedTracks.Count;
        return displayedTracks[nextIndex];
    }

    private static int IndexOfById(Track track, IReadOnlyList<Track> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Id == track.Id)
            {
                return i;
            }
        }
        return -1;
    }
}
```

- [ ] **Step 7: Run strategy tests and verify they pass**

Run:

```powershell
dotnet test .\MusicLibrary.Tests\MusicLibrary.Tests.csproj --filter PlaybackQueueStrategyTests
```

Expected:

```text
Пройден!  - не пройдено: 0, пройдено: 10
```

- [ ] **Step 8: Commit**

```powershell
git add .\MusicLibrary\Services\Playback\IPlaybackQueueStrategy.cs `
        .\MusicLibrary\Services\Playback\NoRepeatStrategy.cs `
        .\MusicLibrary\Services\Playback\RepeatCurrentStrategy.cs `
        .\MusicLibrary\Services\Playback\RepeatLibraryStrategy.cs `
        .\MusicLibrary.Tests\PlaybackQueueStrategyTests.cs
git commit -m "feat: add playback queue strategies"
```

---

## Task 3: Player Settings Storage (TDD)

**Files:**
- Create: `MusicLibrary/Services/Storage/PlayerSettings.cs`
- Create: `MusicLibrary/Services/Storage/IPlayerSettingsStorage.cs`
- Create: `MusicLibrary/Services/Storage/JsonPlayerSettingsStorage.cs`
- Create: `MusicLibrary.Tests/JsonPlayerSettingsStorageTests.cs`

- [ ] **Step 1: Create the settings record**

Create `MusicLibrary/Services/Storage/PlayerSettings.cs`:

```csharp
using MusicLibrary.Models;

namespace MusicLibrary.Services.Storage;

/// <summary>
/// Снимок настроек плеера, сохраняемый между запусками приложения.
/// </summary>
public sealed record PlayerSettings(double Volume, bool IsMuted, RepeatMode RepeatMode)
{
    public static PlayerSettings Default { get; } =
        new(Volume: 1.0, IsMuted: false, RepeatMode: RepeatMode.Off);
}
```

- [ ] **Step 2: Create the interface**

Create `MusicLibrary/Services/Storage/IPlayerSettingsStorage.cs`:

```csharp
namespace MusicLibrary.Services.Storage;

/// <summary>
/// Чтение и запись пользовательских настроек плеера.
/// Load никогда не бросает исключений — при любой ошибке возвращает PlayerSettings.Default.
/// </summary>
public interface IPlayerSettingsStorage
{
    PlayerSettings Load();
    void Save(PlayerSettings settings);
}
```

- [ ] **Step 3: Write the failing storage tests**

Create `MusicLibrary.Tests/JsonPlayerSettingsStorageTests.cs`:

```csharp
using MusicLibrary.Models;
using MusicLibrary.Services.Storage;
using System.IO;

namespace MusicLibrary.Tests;

public sealed class JsonPlayerSettingsStorageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;
    private readonly JsonPlayerSettingsStorage _storage;

    public JsonPlayerSettingsStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MusicBakhTests-" + Guid.NewGuid().ToString("N"));
        _settingsPath = Path.Combine(_tempDir, "player-settings.json");
        _storage = new JsonPlayerSettingsStorage(_settingsPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_FileMissing_ReturnsDefault()
    {
        PlayerSettings settings = _storage.Load();

        Assert.Equal(PlayerSettings.Default, settings);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsAllFields()
    {
        var original = new PlayerSettings(Volume: 0.42, IsMuted: true, RepeatMode: RepeatMode.Library);

        _storage.Save(original);
        PlayerSettings loaded = _storage.Load();

        Assert.Equal(original, loaded);
    }

    [Fact]
    public void Load_CorruptedJson_ReturnsDefault()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_settingsPath, "{not valid json");

        PlayerSettings settings = _storage.Load();

        Assert.Equal(PlayerSettings.Default, settings);
    }

    [Fact]
    public void Load_VolumeOutsideRange_ClampedToZeroOne()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_settingsPath, """{"volume":2.5,"isMuted":false,"repeatMode":"Off"}""");

        PlayerSettings settings = _storage.Load();

        Assert.Equal(1.0, settings.Volume);

        File.WriteAllText(_settingsPath, """{"volume":-0.3,"isMuted":false,"repeatMode":"Off"}""");

        settings = _storage.Load();

        Assert.Equal(0.0, settings.Volume);
    }

    [Fact]
    public void Load_UnknownRepeatMode_ReturnsDefault()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_settingsPath, """{"volume":0.5,"isMuted":false,"repeatMode":"Shuffle"}""");

        PlayerSettings settings = _storage.Load();

        Assert.Equal(PlayerSettings.Default, settings);
    }

    [Fact]
    public void Save_CreatesDirectoryIfMissing()
    {
        Assert.False(Directory.Exists(_tempDir));

        _storage.Save(new PlayerSettings(Volume: 0.7, IsMuted: false, RepeatMode: RepeatMode.Current));

        Assert.True(File.Exists(_settingsPath));
    }
}
```

- [ ] **Step 4: Run tests and verify compile failure**

Run:

```powershell
dotnet test .\MusicLibrary.Tests\MusicLibrary.Tests.csproj --filter JsonPlayerSettingsStorageTests
```

Expected:

```text
Failed!
CS0246: The type or namespace name 'JsonPlayerSettingsStorage' could not be found
```

- [ ] **Step 5: Implement the JSON storage**

Create `MusicLibrary/Services/Storage/JsonPlayerSettingsStorage.cs`:

```csharp
using MusicLibrary.Models;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicLibrary.Services.Storage;

/// <summary>
/// JSON-хранилище настроек плеера.
/// Файл лежит в %LocalAppData%\MusicBakh\player-settings.json.
/// При повреждении / отсутствии возвращает PlayerSettings.Default.
/// </summary>
public sealed class JsonPlayerSettingsStorage : IPlayerSettingsStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;

    public JsonPlayerSettingsStorage(string filePath)
    {
        _filePath = filePath;
    }

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MusicBakh",
        "player-settings.json");

    public PlayerSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return PlayerSettings.Default;
            }

            string json = File.ReadAllText(_filePath);
            PlayerSettingsDto? dto = JsonSerializer.Deserialize<PlayerSettingsDto>(json, Options);
            if (dto is null)
            {
                return PlayerSettings.Default;
            }

            // Ограничиваем громкость допустимым диапазоном — иначе MediaPlayer бросит ArgumentOutOfRange.
            double volume = Math.Clamp(dto.Volume, 0.0, 1.0);
            return new PlayerSettings(volume, dto.IsMuted, dto.RepeatMode);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // Повреждённый файл / нет прав чтения — стартуем с дефолтов, не падаем.
            return PlayerSettings.Default;
        }
    }

    public void Save(PlayerSettings settings)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var dto = new PlayerSettingsDto(settings.Volume, settings.IsMuted, settings.RepeatMode);
            string json = JsonSerializer.Serialize(dto, Options);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Диск переполнен / нет прав — игнорируем, чтобы не уронить приложение из-за настроек.
        }
    }

    private sealed record PlayerSettingsDto(double Volume, bool IsMuted, RepeatMode RepeatMode);
}
```

- [ ] **Step 6: Run tests and verify they pass**

Run:

```powershell
dotnet test .\MusicLibrary.Tests\MusicLibrary.Tests.csproj --filter JsonPlayerSettingsStorageTests
```

Expected:

```text
Пройден!  - не пройдено: 0, пройдено: 6
```

- [ ] **Step 7: Commit**

```powershell
git add .\MusicLibrary\Services\Storage\PlayerSettings.cs `
        .\MusicLibrary\Services\Storage\IPlayerSettingsStorage.cs `
        .\MusicLibrary\Services\Storage\JsonPlayerSettingsStorage.cs `
        .\MusicLibrary.Tests\JsonPlayerSettingsStorageTests.cs
git commit -m "feat: add JSON player settings storage"
```

---

## Task 4: Extend IAudioPlayerService With Volume And Mute

**Files:**
- Modify: `MusicLibrary/Services/Playback/IAudioPlayerService.cs`
- Modify: `MusicLibrary/Services/Playback/MediaPlayerAudioService.cs`
- Modify: `MusicLibrary.Tests/MainViewModelTests.cs` (extend the fake)

- [ ] **Step 1: Extend the interface**

Modify `MusicLibrary/Services/Playback/IAudioPlayerService.cs`. Replace the file contents with:

```csharp
using MusicLibrary.Models;

namespace MusicLibrary.Services.Playback;

public interface IAudioPlayerService : IDisposable
{
    event EventHandler<string>? MediaOpened;
    event EventHandler? MediaEnded;
    event EventHandler<string>? MediaFailed;

    bool IsPlaying { get; }
    TimeSpan Position { get; set; }
    TimeSpan Duration { get; }

    // Громкость 0..1, как у System.Windows.Media.MediaPlayer.
    double Volume { get; set; }
    bool IsMuted { get; set; }

    OperationResult Open(string filePath);
    OperationResult Play();
    void Pause();
    void Stop();
}
```

- [ ] **Step 2: Implement Volume and IsMuted in MediaPlayerAudioService**

Modify `MusicLibrary/Services/Playback/MediaPlayerAudioService.cs`. Add these property implementations after the existing `Duration` property (around line 90, before `Open`):

```csharp
public double Volume
{
    get => _player.Volume;
    // Защитный clamp на случай, если откуда-то прилетит volume > 1 или < 0.
    set => _player.Volume = Math.Clamp(value, 0.0, 1.0);
}

public bool IsMuted
{
    get => _player.IsMuted;
    set => _player.IsMuted = value;
}
```

- [ ] **Step 3: Extend the test fake**

In `MusicLibrary.Tests/MainViewModelTests.cs`, find the `FakeAudioPlayerService` class. Add these auto-properties anywhere among the existing public properties (e.g. after `Duration`):

```csharp
public double Volume { get; set; } = 1.0;
public bool IsMuted { get; set; }
```

- [ ] **Step 4: Build and run all existing tests to verify nothing broke**

Run:

```powershell
dotnet test .\MusicLibrary.sln
```

Expected:

```text
Пройден!  - не пройдено: 0
```

(All 79 prior tests + 6 new storage tests + 10 new strategy tests still pass.)

- [ ] **Step 5: Commit**

```powershell
git add .\MusicLibrary\Services\Playback\IAudioPlayerService.cs `
        .\MusicLibrary\Services\Playback\MediaPlayerAudioService.cs `
        .\MusicLibrary.Tests\MainViewModelTests.cs
git commit -m "feat: add Volume and IsMuted to audio player service"
```

---

## Task 5: ViewModel — Volume / IsMuted / RepeatMode With Persistence (TDD)

**Files:**
- Modify: `MusicLibrary/ViewModels/MainViewModel.cs`
- Modify: `MusicLibrary.Tests/MainViewModelTests.cs`

- [ ] **Step 1: Add a fake settings storage and helper to the test file**

In `MusicLibrary.Tests/MainViewModelTests.cs`, add this nested class after `FakeAudioPlayerService`:

```csharp
private sealed class FakePlayerSettingsStorage : IPlayerSettingsStorage
{
    public PlayerSettings Loaded { get; set; } = PlayerSettings.Default;
    public List<PlayerSettings> SavedSnapshots { get; } = new();

    public PlayerSettings Load() => Loaded;
    public void Save(PlayerSettings settings) => SavedSnapshots.Add(settings);
}
```

Add the `using MusicLibrary.Services.Storage;` and `using MusicLibrary.Models;` directives if they are not already present at the top.

Update `CreateViewModel(IReadOnlyList<Track>, FakeAudioPlayerService, FakeFileService)` to a new overload that accepts an optional storage; replace the existing private static method with these two:

```csharp
private static MainViewModel CreateViewModel(
    IReadOnlyList<Track> tracks,
    FakeAudioPlayerService player,
    FakeFileService fileService)
{
    return CreateViewModel(tracks, player, fileService, new FakePlayerSettingsStorage());
}

private static MainViewModel CreateViewModel(
    IReadOnlyList<Track> tracks,
    FakeAudioPlayerService player,
    FakeFileService fileService,
    FakePlayerSettingsStorage storage)
{
    return new MainViewModel(
        new FakeTrackRepository(tracks),
        fileService,
        new FakeSaveFileDialogService(),
        player,
        addTrackDialogService: null,
        userTrackStorage: null,
        confirmationService: null,
        playerSettingsStorage: storage);
}
```

Also update `CreateViewModelWithPlayer` to expose the storage:

```csharp
private static (MainViewModel ViewModel, FakeAudioPlayerService Player, FakePlayerSettingsStorage Storage) CreateViewModelWithPlayer()
{
    var tracks = new[]
    {
        new Track { Id = 1, Title = "Rock Song", Artist = "Band", Genre = "Рок", Duration = TimeSpan.FromSeconds(100), FilePath = "rock.mp3" },
        new Track { Id = 2, Title = "Jazz Song", Artist = "Quartet", Genre = "Джаз", Duration = TimeSpan.FromSeconds(120), FilePath = "jazz.mp3" }
    };

    var player = new FakeAudioPlayerService();
    var storage = new FakePlayerSettingsStorage();
    var viewModel = CreateViewModel(tracks, player, new FakeFileService(), storage);

    return (viewModel, player, storage);
}
```

Then update every existing test that destructures `var (viewModel, player) = CreateViewModelWithPlayer();` to use `var (viewModel, player, _) = CreateViewModelWithPlayer();`. Use a Find-and-Replace across the file: replace `var (viewModel, player) = CreateViewModelWithPlayer();` → `var (viewModel, player, _) = CreateViewModelWithPlayer();` and `var (viewModel, _) = CreateViewModelWithPlayer();` → `var (viewModel, _, _) = CreateViewModelWithPlayer();`.

- [ ] **Step 2: Add the failing tests**

Add these tests to `MainViewModelTests.cs`:

```csharp
[Fact]
public void Volume_Setter_UpdatesPlayerAndSaves()
{
    var (viewModel, player, storage) = CreateViewModelWithPlayer();

    viewModel.Volume = 0.42;

    Assert.Equal(0.42, player.Volume);
    Assert.Contains(storage.SavedSnapshots, s => s.Volume == 0.42);
}

[Fact]
public void IsMuted_Setter_UpdatesPlayerAndSaves()
{
    var (viewModel, player, storage) = CreateViewModelWithPlayer();

    viewModel.IsMuted = true;

    Assert.True(player.IsMuted);
    Assert.Contains(storage.SavedSnapshots, s => s.IsMuted);
}

[Fact]
public void RepeatMode_Setter_Saves()
{
    var (viewModel, _, storage) = CreateViewModelWithPlayer();

    viewModel.RepeatMode = RepeatMode.Library;

    Assert.Contains(storage.SavedSnapshots, s => s.RepeatMode == RepeatMode.Library);
}
```

- [ ] **Step 3: Run tests and verify compile failure**

Run:

```powershell
dotnet test .\MusicLibrary.Tests\MusicLibrary.Tests.csproj --filter "Volume_Setter|IsMuted_Setter|RepeatMode_Setter"
```

Expected: compile errors saying `MainViewModel` does not have `Volume`, `IsMuted`, `RepeatMode`, or a constructor with `playerSettingsStorage`.

- [ ] **Step 4: Update the ViewModel constructor and add the state properties**

Modify `MusicLibrary/ViewModels/MainViewModel.cs`:

Add this `using` near the top:

```csharp
using MusicLibrary.Services.Storage;
```

Add these private fields next to the other backing fields (around line 36):

```csharp
private readonly IPlayerSettingsStorage? _playerSettingsStorage;
private RepeatMode _repeatMode;
private double _volume = 1.0;
private bool _isMuted;
private bool _isSeeking;
```

Update the constructor parameter list. Find the closing line `IConfirmationService? confirmationService = null)` and replace it with two lines so a new optional 8th parameter is added:

```csharp
    IConfirmationService? confirmationService = null,
    IPlayerSettingsStorage? playerSettingsStorage = null)
```

Inside the constructor body, find the existing line `_confirmationService = confirmationService;` and add one new line directly after it:

```csharp
_playerSettingsStorage = playerSettingsStorage;
```

The rest of the constructor body stays exactly as it was.

Add these public properties anywhere among the other observable properties (e.g. after `IsPlaying`):

```csharp
public RepeatMode RepeatMode
{
    get => _repeatMode;
    set
    {
        if (SetProperty(ref _repeatMode, value))
        {
            PersistSettings();
        }
    }
}

public double Volume
{
    get => _volume;
    set
    {
        // Жёсткий clamp перед записью в плеер — UI слайдеру удобнее не знать про границы.
        double clamped = Math.Clamp(value, 0.0, 1.0);
        if (SetProperty(ref _volume, clamped))
        {
            _audioPlayerService.Volume = clamped;
            PersistSettings();
        }
    }
}

public bool IsMuted
{
    get => _isMuted;
    set
    {
        if (SetProperty(ref _isMuted, value))
        {
            _audioPlayerService.IsMuted = value;
            PersistSettings();
        }
    }
}

public bool IsSeeking
{
    get => _isSeeking;
    set => SetProperty(ref _isSeeking, value);
}
```

Add the `PersistSettings` helper at the bottom of the class, just above `Dispose`:

```csharp
private void PersistSettings()
{
    _playerSettingsStorage?.Save(new PlayerSettings(_volume, _isMuted, _repeatMode));
}
```

- [ ] **Step 5: Run the new tests and existing tests**

Run:

```powershell
dotnet test .\MusicLibrary.sln
```

Expected:

```text
Пройден!  - не пройдено: 0
```

- [ ] **Step 6: Commit**

```powershell
git add .\MusicLibrary\ViewModels\MainViewModel.cs `
        .\MusicLibrary.Tests\MainViewModelTests.cs
git commit -m "feat: add volume, mute, repeat-mode state with persistence"
```

---

## Task 6: ViewModel — Skip ±10s, Prev / Next, Toggle Mute, Cycle Repeat (TDD)

**Files:**
- Modify: `MusicLibrary/ViewModels/MainViewModel.cs`
- Modify: `MusicLibrary.Tests/MainViewModelTests.cs`

- [ ] **Step 1: Add the failing command tests**

Add these tests to `MainViewModelTests.cs`:

```csharp
[Fact]
public void SkipForward_AtEnd_ClampsToDuration()
{
    var (viewModel, player, _) = CreateViewModelWithPlayer();
    player.Position = TimeSpan.FromSeconds(95);

    viewModel.SkipForwardCommand.Execute(null);

    // Duration фейка = 100s, шаг = 10, значит должно стать 100, не 105.
    Assert.Equal(TimeSpan.FromSeconds(100), player.Position);
}

[Fact]
public void SkipBackward_AtStart_ClampsToZero()
{
    var (viewModel, player, _) = CreateViewModelWithPlayer();
    player.Position = TimeSpan.FromSeconds(3);

    viewModel.SkipBackwardCommand.Execute(null);

    Assert.Equal(TimeSpan.Zero, player.Position);
}

[Fact]
public void PreviousTrack_AtFirst_CommandDisabled()
{
    var (viewModel, _, _) = CreateViewModelWithPlayer();
    viewModel.SelectedTrack = viewModel.DisplayedTracks[0];
    viewModel.PlayPauseCommand.Execute(null);

    Assert.False(viewModel.PreviousTrackCommand.CanExecute(null));
}

[Fact]
public void NextTrack_AtLast_CommandDisabled()
{
    var (viewModel, _, _) = CreateViewModelWithPlayer();
    viewModel.SelectedTrack = viewModel.DisplayedTracks[1];
    viewModel.PlayPauseCommand.Execute(null);

    Assert.False(viewModel.NextTrackCommand.CanExecute(null));
}

[Fact]
public void NextTrack_AtFirst_AdvancesAndOpensSecondTrack()
{
    var (viewModel, player, _) = CreateViewModelWithPlayer();
    Track first = viewModel.DisplayedTracks[0];
    Track second = viewModel.DisplayedTracks[1];

    viewModel.SelectedTrack = first;
    viewModel.PlayPauseCommand.Execute(null);

    viewModel.NextTrackCommand.Execute(null);

    Assert.Equal(second, viewModel.PlayingTrack);
    Assert.Equal(second.FilePath, player.LastOpenedFilePath);
}

[Fact]
public void PreviousTrack_AtSecond_GoesBackToFirst()
{
    var (viewModel, player, _) = CreateViewModelWithPlayer();
    Track first = viewModel.DisplayedTracks[0];
    Track second = viewModel.DisplayedTracks[1];

    viewModel.SelectedTrack = second;
    viewModel.PlayPauseCommand.Execute(null);

    viewModel.PreviousTrackCommand.Execute(null);

    Assert.Equal(first, viewModel.PlayingTrack);
    Assert.Equal(first.FilePath, player.LastOpenedFilePath);
}

[Fact]
public void ToggleMuteCommand_FlipsIsMuted()
{
    var (viewModel, _, _) = CreateViewModelWithPlayer();

    viewModel.ToggleMuteCommand.Execute(null);
    Assert.True(viewModel.IsMuted);

    viewModel.ToggleMuteCommand.Execute(null);
    Assert.False(viewModel.IsMuted);
}

[Fact]
public void CycleRepeatModeCommand_CyclesOffCurrentLibraryOff()
{
    var (viewModel, _, _) = CreateViewModelWithPlayer();

    Assert.Equal(RepeatMode.Off, viewModel.RepeatMode);

    viewModel.CycleRepeatModeCommand.Execute(null);
    Assert.Equal(RepeatMode.Current, viewModel.RepeatMode);

    viewModel.CycleRepeatModeCommand.Execute(null);
    Assert.Equal(RepeatMode.Library, viewModel.RepeatMode);

    viewModel.CycleRepeatModeCommand.Execute(null);
    Assert.Equal(RepeatMode.Off, viewModel.RepeatMode);
}
```

- [ ] **Step 2: Run tests and verify compile failure**

Run:

```powershell
dotnet test .\MusicLibrary.Tests\MusicLibrary.Tests.csproj --filter "SkipForward|SkipBackward|PreviousTrack|NextTrack|ToggleMute|CycleRepeatMode"
```

Expected: compile errors for missing commands.

- [ ] **Step 3: Add command properties and implementations**

Modify `MusicLibrary/ViewModels/MainViewModel.cs`:

Add these `ICommand` properties after the existing `DeleteTrackCommand` property:

```csharp
public ICommand SkipForwardCommand { get; }
public ICommand SkipBackwardCommand { get; }
public ICommand PreviousTrackCommand { get; }
public ICommand NextTrackCommand { get; }
public ICommand ToggleMuteCommand { get; }
public ICommand CycleRepeatModeCommand { get; }
```

In the constructor, after the existing `DeleteTrackCommand = ...` line, add:

```csharp
SkipForwardCommand = new RelayCommand(_ => SkipBy(TimeSpan.FromSeconds(10)), _ => PlayingTrack is not null);
SkipBackwardCommand = new RelayCommand(_ => SkipBy(TimeSpan.FromSeconds(-10)), _ => PlayingTrack is not null);
PreviousTrackCommand = new RelayCommand(_ => GoToTrackByOffset(-1), _ => CanGoToOffset(-1));
NextTrackCommand = new RelayCommand(_ => GoToTrackByOffset(+1), _ => CanGoToOffset(+1));
ToggleMuteCommand = new RelayCommand(_ => IsMuted = !IsMuted);
CycleRepeatModeCommand = new RelayCommand(_ => CycleRepeatMode());
```

Add these private helpers near the other private methods (e.g. just before `PlayOrPause`):

```csharp
private void SkipBy(TimeSpan delta)
{
    if (PlayingTrack is null)
    {
        return;
    }

    TimeSpan target = _audioPlayerService.Position + delta;
    TimeSpan duration = _audioPlayerService.Duration;
    if (target < TimeSpan.Zero)
    {
        target = TimeSpan.Zero;
    }
    else if (duration > TimeSpan.Zero && target > duration)
    {
        target = duration;
    }

    _audioPlayerService.Position = target;
    CurrentPosition = target;
}

private bool CanGoToOffset(int offset)
{
    if (PlayingTrack is null || DisplayedTracks.Count == 0)
    {
        return false;
    }

    int index = IndexOfPlayingTrack();
    if (index < 0)
    {
        return false;
    }

    int target = index + offset;
    return target >= 0 && target < DisplayedTracks.Count;
}

private void GoToTrackByOffset(int offset)
{
    if (PlayingTrack is null)
    {
        return;
    }

    int index = IndexOfPlayingTrack();
    if (index < 0)
    {
        return;
    }

    int target = index + offset;
    if (target < 0 || target >= DisplayedTracks.Count)
    {
        return;
    }

    Track next = DisplayedTracks[target];
    SelectedTrack = next;
    StartOrResumeTrack(next);
}

private int IndexOfPlayingTrack()
{
    if (PlayingTrack is null)
    {
        return -1;
    }

    for (int i = 0; i < DisplayedTracks.Count; i++)
    {
        if (DisplayedTracks[i].Id == PlayingTrack.Id)
        {
            return i;
        }
    }
    return -1;
}

private void CycleRepeatMode()
{
    RepeatMode = RepeatMode switch
    {
        RepeatMode.Off => RepeatMode.Current,
        RepeatMode.Current => RepeatMode.Library,
        _ => RepeatMode.Off
    };
}
```

- [ ] **Step 4: Run the new tests**

Run:

```powershell
dotnet test .\MusicLibrary.Tests\MusicLibrary.Tests.csproj --filter "SkipForward|SkipBackward|PreviousTrack|NextTrack|ToggleMute|CycleRepeatMode"
```

Expected:

```text
Пройден!  - не пройдено: 0, пройдено: 8
```

- [ ] **Step 5: Run all tests to verify nothing else broke**

Run:

```powershell
dotnet test .\MusicLibrary.sln
```

Expected:

```text
Пройден!  - не пройдено: 0
```

- [ ] **Step 6: Commit**

```powershell
git add .\MusicLibrary\ViewModels\MainViewModel.cs `
        .\MusicLibrary.Tests\MainViewModelTests.cs
git commit -m "feat: add skip, prev, next, mute, cycle-repeat commands"
```

---

## Task 7: ViewModel — Seek Anti-Feedback Flag And SeekToCommand (TDD)

**Files:**
- Modify: `MusicLibrary/ViewModels/MainViewModel.cs`
- Modify: `MusicLibrary.Tests/MainViewModelTests.cs`

- [ ] **Step 1: Add the failing tests**

Add these tests to `MainViewModelTests.cs`:

```csharp
[Fact]
public void SeekToCommand_WritesPositionToPlayer()
{
    var (viewModel, player, _) = CreateViewModelWithPlayer();
    viewModel.SelectedTrack = viewModel.DisplayedTracks[0];
    viewModel.PlayPauseCommand.Execute(null);

    viewModel.SeekToCommand.Execute(TimeSpan.FromSeconds(45));

    Assert.Equal(TimeSpan.FromSeconds(45), player.Position);
}

[Fact]
public void SeekToCommand_NoPlayingTrack_DoesNothing()
{
    var (viewModel, player, _) = CreateViewModelWithPlayer();
    player.Position = TimeSpan.FromSeconds(7);

    viewModel.SeekToCommand.Execute(TimeSpan.FromSeconds(45));

    Assert.Equal(TimeSpan.FromSeconds(7), player.Position);
}
```

- [ ] **Step 2: Run tests and verify compile failure**

Run:

```powershell
dotnet test .\MusicLibrary.Tests\MusicLibrary.Tests.csproj --filter "SeekToCommand"
```

Expected: compile error for missing `SeekToCommand`.

- [ ] **Step 3: Implement SeekToCommand**

In `MainViewModel.cs`, add this property next to the other commands:

```csharp
public ICommand SeekToCommand { get; }
```

In the constructor, after `CycleRepeatModeCommand = ...`, add:

```csharp
SeekToCommand = new RelayCommand(parameter => SeekTo(parameter as TimeSpan?), _ => PlayingTrack is not null);
```

Add the helper near the other privates:

```csharp
private void SeekTo(TimeSpan? target)
{
    if (target is null || PlayingTrack is null)
    {
        return;
    }

    TimeSpan duration = _audioPlayerService.Duration;
    TimeSpan clamped = target.Value;
    if (clamped < TimeSpan.Zero)
    {
        clamped = TimeSpan.Zero;
    }
    else if (duration > TimeSpan.Zero && clamped > duration)
    {
        clamped = duration;
    }

    _audioPlayerService.Position = clamped;
    CurrentPosition = clamped;
}
```

- [ ] **Step 4: Wire IsSeeking into the progress timer**

The `IsSeeking` property already exists from Task 5. Now make the progress timer respect it. In `MainViewModel.cs`, find `RefreshProgress` and replace with:

```csharp
private void RefreshProgress()
{
    // Пока пользователь тянет ползунок seek-слайдера, не перезаписываем его значение из плеера —
    // иначе позиция будет дёргаться обратно на каждом тике таймера.
    if (_isSeeking)
    {
        return;
    }

    CurrentPosition = _audioPlayerService.Position;
}
```

- [ ] **Step 5: Run tests**

Run:

```powershell
dotnet test .\MusicLibrary.Tests\MusicLibrary.Tests.csproj --filter "SeekToCommand"
```

Expected:

```text
Пройден!  - не пройдено: 0, пройдено: 2
```

Then full suite:

```powershell
dotnet test .\MusicLibrary.sln
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```powershell
git add .\MusicLibrary\ViewModels\MainViewModel.cs `
        .\MusicLibrary.Tests\MainViewModelTests.cs
git commit -m "feat: add SeekToCommand and IsSeeking timer guard"
```

---

## Task 8: ViewModel — MediaEnded Strategy Dispatch + MediaFailed Chain Stop (TDD)

**Files:**
- Modify: `MusicLibrary/ViewModels/MainViewModel.cs`
- Modify: `MusicLibrary.Tests/MainViewModelTests.cs`

- [ ] **Step 1: Add the failing dispatch tests**

Add these tests to `MainViewModelTests.cs`:

```csharp
[Fact]
public void MediaEnded_RepeatOff_ClearsPlayingTrack()
{
    var (viewModel, player, _) = CreateViewModelWithPlayer();
    viewModel.SelectedTrack = viewModel.DisplayedTracks[1]; // последний трек
    viewModel.PlayPauseCommand.Execute(null);
    player.RaiseOpenedForTest();

    player.RaiseEndedForTest();

    Assert.Null(viewModel.PlayingTrack);
    Assert.False(viewModel.IsPlaying);
}

[Fact]
public void MediaEnded_RepeatCurrent_RestartsSameTrack()
{
    var (viewModel, player, _) = CreateViewModelWithPlayer();
    Track first = viewModel.DisplayedTracks[0];
    viewModel.RepeatMode = RepeatMode.Current;
    viewModel.SelectedTrack = first;
    viewModel.PlayPauseCommand.Execute(null);
    player.RaiseOpenedForTest();
    int playsBefore = player.PlayCallCount;

    player.RaiseEndedForTest();

    Assert.Equal(first, viewModel.PlayingTrack);
    Assert.True(player.PlayCallCount > playsBefore);
}

[Fact]
public void MediaEnded_RepeatLibrary_AdvancesAndWrapsAtEnd()
{
    var (viewModel, player, _) = CreateViewModelWithPlayer();
    Track first = viewModel.DisplayedTracks[0];
    Track second = viewModel.DisplayedTracks[1];
    viewModel.RepeatMode = RepeatMode.Library;
    viewModel.SelectedTrack = first;
    viewModel.PlayPauseCommand.Execute(null);
    player.RaiseOpenedForTest();

    player.RaiseEndedForTest();
    Assert.Equal(second, viewModel.PlayingTrack);
    player.RaiseOpenedForTest(second.FilePath);

    player.RaiseEndedForTest();
    Assert.Equal(first, viewModel.PlayingTrack);
}

[Fact]
public void MediaEnded_RepeatOff_NotAtLast_AutoAdvances()
{
    var (viewModel, player, _) = CreateViewModelWithPlayer();
    Track first = viewModel.DisplayedTracks[0];
    Track second = viewModel.DisplayedTracks[1];
    viewModel.SelectedTrack = first;
    viewModel.PlayPauseCommand.Execute(null);
    player.RaiseOpenedForTest();

    player.RaiseEndedForTest();

    Assert.Equal(second, viewModel.PlayingTrack);
    Assert.Equal(second.FilePath, player.LastOpenedFilePath);
}

[Fact]
public void MediaFailed_DuringAutoNext_StopsChain()
{
    var (viewModel, player, _) = CreateViewModelWithPlayer();
    viewModel.SelectedTrack = viewModel.DisplayedTracks[0];
    viewModel.PlayPauseCommand.Execute(null);
    player.RaiseOpenedForTest();

    // Симулируем ошибку — авто-next не должен попытаться запустить следующий.
    player.RaiseFailedForTest("decoder error");

    Assert.Null(viewModel.PlayingTrack);
    Assert.False(viewModel.IsPlaying);
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test .\MusicLibrary.Tests\MusicLibrary.Tests.csproj --filter "MediaEnded_Repeat|MediaEnded_RepeatOff_NotAtLast|MediaFailed_DuringAutoNext"
```

Expected: existing `HandleMediaEnded` simply resets state, so these tests fail because no auto-advance happens.

- [ ] **Step 3: Replace HandleMediaEnded with strategy dispatch**

In `MainViewModel.cs`, replace the entire `HandleMediaEnded` method (and add `ResolveStrategy`) with:

```csharp
private void HandleMediaEnded()
{
    Track? finished = PlayingTrack;

    // Сбрасываем «тикалку» и текущую позицию, но PlayingTrack пока сохраняем —
    // если стратегия отдаст следующий трек, его сразу запустит StartOrResumeTrack.
    _audioPlayerService.Stop();
    _progressTimer.Stop();
    IsPlaying = false;
    _isPaused = false;
    CurrentPosition = TimeSpan.Zero;
    _pendingHistoryTrack = null;

    if (finished is null)
    {
        PlayingTrack = null;
        return;
    }

    Track? next = ResolveStrategy(RepeatMode).GetNext(finished, DisplayedTracks);

    if (next is null)
    {
        PlayingTrack = null;
        SetStatus(OperationResult.Info("Воспроизведение завершено."));
        return;
    }

    SelectedTrack = next;
    StartOrResumeTrack(next);
}

private static IPlaybackQueueStrategy ResolveStrategy(RepeatMode mode) => mode switch
{
    RepeatMode.Current => RepeatCurrentStrategy.Instance,
    RepeatMode.Library => RepeatLibraryStrategy.Instance,
    _ => NoRepeatStrategy.Instance,
};
```

Add the `using` for the strategies if missing:

```csharp
using MusicLibrary.Services.Playback;
```

(`MusicLibrary.Services.Playback` was already in usings; `RepeatMode` lives in `MusicLibrary.Models` which is also already imported.)

- [ ] **Step 4: Verify HandleMediaFailed already breaks the chain**

Open the existing `HandleMediaFailed` method in `MainViewModel.cs`. Confirm it calls `ResetPlaybackState()` and does **not** invoke `StartOrResumeTrack` or any auto-next logic. If that is already the case (it should be, from package 1), no change is needed for the chain-stop semantics. Add a comment above the method to document the contract:

```csharp
// MediaFailed обрывает цепочку auto-next: иначе на повреждённой библиотеке
// мы получим бесконечный цикл алертов «следующий трек тоже не открывается».
private void HandleMediaFailed(string message)
{
    ResetPlaybackState();
    SetStatus(OperationResult.Error($"Ошибка воспроизведения: {message}"));
}
```

- [ ] **Step 5: Run the dispatch tests**

Run:

```powershell
dotnet test .\MusicLibrary.Tests\MusicLibrary.Tests.csproj --filter "MediaEnded_Repeat|MediaEnded_RepeatOff_NotAtLast|MediaFailed_DuringAutoNext"
```

Expected:

```text
Пройден!  - не пройдено: 0, пройдено: 5
```

Then run the full suite:

```powershell
dotnet test .\MusicLibrary.sln
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```powershell
git add .\MusicLibrary\ViewModels\MainViewModel.cs `
        .\MusicLibrary.Tests\MainViewModelTests.cs
git commit -m "feat: dispatch MediaEnded through queue strategy"
```

---

## Task 9: PlayerIcons.xaml Resource Dictionary

**Files:**
- Create: `MusicLibrary/Resources/PlayerIcons.xaml`
- Modify: `MusicLibrary/App.xaml`

- [ ] **Step 1: Create the icons resource dictionary**

Create `MusicLibrary/Resources/PlayerIcons.xaml`:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Каждая иконка — Geometry, рисуется внутри Path с заливкой PrimaryBrush.
         Иконки рассчитаны на 16x16 viewport и масштабируются под размер кнопки. -->

    <!-- Воспроизвести: треугольник вправо. -->
    <Geometry x:Key="IconPlayGeometry">M 4,2 L 4,14 L 14,8 Z</Geometry>

    <!-- Пауза: две вертикальные полоски. -->
    <Geometry x:Key="IconPauseGeometry">M 4,2 H 7 V 14 H 4 Z M 9,2 H 12 V 14 H 9 Z</Geometry>

    <!-- Перемотка назад на 10 секунд: два треугольника, направленные влево. -->
    <Geometry x:Key="IconSkipBack10Geometry">M 14,2 L 14,14 L 8,8 Z M 8,2 L 8,14 L 2,8 Z</Geometry>

    <!-- Перемотка вперёд на 10 секунд: два треугольника, направленные вправо. -->
    <Geometry x:Key="IconSkipForward10Geometry">M 2,2 L 2,14 L 8,8 Z M 8,2 L 8,14 L 14,8 Z</Geometry>

    <!-- Предыдущий трек: вертикальная полоска плюс треугольник влево. -->
    <Geometry x:Key="IconPrevGeometry">M 2,2 H 4 V 14 H 2 Z M 14,2 L 14,14 L 5,8 Z</Geometry>

    <!-- Следующий трек: треугольник вправо плюс вертикальная полоска. -->
    <Geometry x:Key="IconNextGeometry">M 2,2 L 2,14 L 11,8 Z M 12,2 H 14 V 14 H 12 Z</Geometry>

    <!-- Громкость: динамик с двумя дугами справа. -->
    <Geometry x:Key="IconVolumeGeometry">M 2,6 L 5,6 L 9,3 L 9,13 L 5,10 L 2,10 Z M 11,6 Q 13,8 11,10 M 12,4 Q 16,8 12,12</Geometry>

    <!-- Звук выключен: тот же динамик плюс косая черта. -->
    <Geometry x:Key="IconMutedGeometry">M 2,6 L 5,6 L 9,3 L 9,13 L 5,10 L 2,10 Z M 11,4 L 15,12 M 15,4 L 11,12</Geometry>

    <!-- Повтор выключен: серая круговая стрелка. -->
    <Geometry x:Key="IconRepeatGeometry">M 4,5 H 11 L 11,3 L 14,6 L 11,9 L 11,7 H 5 V 9 H 4 Z M 12,11 H 5 L 5,13 L 2,10 L 5,7 L 5,9 H 11 V 7 H 12 Z</Geometry>

</ResourceDictionary>
```

- [ ] **Step 2: Register the dictionary in App.xaml**

Modify `MusicLibrary/App.xaml`. In the `<ResourceDictionary.MergedDictionaries>` block, add this line **before** `<ResourceDictionary Source="Resources/TrackTemplates.xaml" />`:

```xml
<ResourceDictionary Source="Resources/PlayerIcons.xaml" />
```

- [ ] **Step 3: Build to verify XAML compiles**

Run:

```powershell
dotnet build .\MusicLibrary\MusicLibrary.csproj
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 4: Commit**

```powershell
git add .\MusicLibrary\Resources\PlayerIcons.xaml `
        .\MusicLibrary\App.xaml
git commit -m "feat: add player icons resource dictionary"
```

---

## Task 10: MainWindow.xaml — Player Bar And Hotkeys

**Files:**
- Modify: `MusicLibrary/MainWindow.xaml`

- [ ] **Step 1: Add Window.InputBindings for hotkeys**

Open `MusicLibrary/MainWindow.xaml`. Just after the `<Window ...>` opening tag and before `<Grid>` (or whatever the immediate child is), add:

```xml
<Window.InputBindings>
    <!-- Горячие клавиши плеера. Левая/правая стрелки перехватываются Slider/ListBox,
         когда на них фокус — починка вынесена за рамки этого пакета. -->
    <KeyBinding Key="Space" Command="{Binding PlayPauseCommand}" />
    <KeyBinding Key="Left"  Command="{Binding SkipBackwardCommand}" />
    <KeyBinding Key="Right" Command="{Binding SkipForwardCommand}" />
    <KeyBinding Key="Left"  Modifiers="Control" Command="{Binding PreviousTrackCommand}" />
    <KeyBinding Key="Right" Modifiers="Control" Command="{Binding NextTrackCommand}" />
    <KeyBinding Key="M"     Command="{Binding ToggleMuteCommand}" />
    <KeyBinding Key="R"     Command="{Binding CycleRepeatModeCommand}" />
</Window.InputBindings>
```

- [ ] **Step 2: Replace the existing transport controls with the new player bar**

Find the existing center column containing the cover, title, and the legacy `<Button Content="Воспроизвести"...>` and `<Button Content="Стоп"...>`. Locate the inner `<StackPanel>` that holds these buttons (it sits between the metadata row and the Save / Delete buttons).

Replace just the legacy Play/Pause and Stop buttons with the new player bar block. Keep the Save and Delete buttons exactly where they are. The new block consists of three rows:

```xml
<!-- Seek-слайдер: позволяет перетаскивать ползунок и кликать по дорожке.
     IsMoveToPointEnabled="True" — клик по дорожке сразу перемещает thumb;
     PreviewMouseUp на всём слайдере объединяет drag-completed и click-without-drag. -->
<Grid Margin="0,8,0,0">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="Auto" />
    </Grid.ColumnDefinitions>
    <TextBlock Grid.Column="0"
               Text="{Binding CurrentPositionText}"
               Foreground="{StaticResource MutedForegroundBrush}"
               FontSize="12"
               VerticalAlignment="Center"
               Margin="0,0,8,0" />
    <Slider x:Name="SeekSlider"
            Grid.Column="1"
            Minimum="0"
            Maximum="{Binding ProgressMaximum}"
            Value="{Binding ProgressValue, Mode=TwoWay}"
            IsMoveToPointEnabled="True"
            IsEnabled="{Binding PlayingTrack, Converter={StaticResource NullToBooleanConverter}}"
            Thumb.DragStarted="OnSeekDragStarted"
            PreviewMouseUp="OnSeekPreviewMouseUp" />
    <TextBlock Grid.Column="2"
               Text="{Binding CurrentDurationText}"
               Foreground="{StaticResource MutedForegroundBrush}"
               FontSize="12"
               VerticalAlignment="Center"
               Margin="8,0,0,0" />
</Grid>

<!-- Транспортная строка: ⏮  ⏪  ▶/⏸  ⏩  ⏭ -->
<StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,12,0,0">
    <Button Style="{StaticResource IconButtonStyle}" Command="{Binding PreviousTrackCommand}" ToolTip="Предыдущий трек (Ctrl+←)">
        <Path Data="{StaticResource IconPrevGeometry}" Fill="{StaticResource PrimaryBrush}" Width="16" Height="16" Stretch="Uniform" />
    </Button>
    <Button Style="{StaticResource IconButtonStyle}" Command="{Binding SkipBackwardCommand}" ToolTip="Перемотка назад на 10 секунд (←)">
        <Path Data="{StaticResource IconSkipBack10Geometry}" Fill="{StaticResource PrimaryBrush}" Width="16" Height="16" Stretch="Uniform" />
    </Button>
    <Button x:Name="PlayPauseButton"
            Style="{StaticResource PrimaryIconButtonStyle}"
            Command="{Binding PlayPauseCommand}"
            ToolTip="Воспроизвести / Пауза (Space)">
        <!-- Data приходит из PlayPauseIconStyle (Play по умолчанию, Pause при IsPlaying=True). -->
        <Path x:Name="PlayPauseIcon"
              Style="{StaticResource PlayPauseIconStyle}"
              Fill="{StaticResource BackgroundBrush}"
              Width="20" Height="20"
              Stretch="Uniform" />
    </Button>
    <Button Style="{StaticResource IconButtonStyle}" Command="{Binding SkipForwardCommand}" ToolTip="Перемотка вперёд на 10 секунд (→)">
        <Path Data="{StaticResource IconSkipForward10Geometry}" Fill="{StaticResource PrimaryBrush}" Width="16" Height="16" Stretch="Uniform" />
    </Button>
    <Button Style="{StaticResource IconButtonStyle}" Command="{Binding NextTrackCommand}" ToolTip="Следующий трек (Ctrl+→)">
        <Path Data="{StaticResource IconNextGeometry}" Fill="{StaticResource PrimaryBrush}" Width="16" Height="16" Stretch="Uniform" />
    </Button>
</StackPanel>

<!-- Утилитарная строка: громкость, mute, repeat. -->
<Grid Margin="0,12,0,0">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="Auto" />
    </Grid.ColumnDefinitions>

    <StackPanel Grid.Column="0" Orientation="Horizontal" VerticalAlignment="Center">
        <Path Data="{StaticResource IconVolumeGeometry}" Fill="{StaticResource MutedForegroundBrush}" Width="14" Height="14" Stretch="Uniform" Margin="0,0,8,0" />
        <Slider x:Name="VolumeSlider"
                Width="120"
                Minimum="0"
                Maximum="1"
                LargeChange="0.1"
                SmallChange="0.05"
                Value="{Binding Volume, Mode=TwoWay}"
                VerticalAlignment="Center" />
        <TextBlock Text="{Binding Volume, StringFormat={}{0:P0}}"
                   Foreground="{StaticResource MutedForegroundBrush}"
                   FontSize="12"
                   VerticalAlignment="Center"
                   Margin="8,0,0,0" />
    </StackPanel>

    <Button x:Name="MuteButton"
            Grid.Column="1"
            Style="{StaticResource IconButtonStyle}"
            Command="{Binding ToggleMuteCommand}"
            ToolTip="Без звука (M)"
            Margin="12,0,0,0">
        <!-- Data приходит из MuteIconStyle и переключается по IsMuted. -->
        <Path x:Name="MuteIcon"
              Style="{StaticResource MuteIconStyle}"
              Fill="{StaticResource PrimaryBrush}"
              Width="16" Height="16"
              Stretch="Uniform" />
    </Button>

    <Button x:Name="RepeatButton"
            Grid.Column="2"
            Style="{StaticResource IconButtonStyle}"
            Command="{Binding CycleRepeatModeCommand}"
            ToolTip="Режим повтора (R)"
            Margin="12,0,0,0">
        <StackPanel Orientation="Horizontal">
            <!-- Fill (золотой/серый) приходит из RepeatIconStyle по RepeatMode. -->
            <Path x:Name="RepeatIcon"
                  Style="{StaticResource RepeatIconStyle}"
                  Data="{StaticResource IconRepeatGeometry}"
                  Width="16" Height="16"
                  Stretch="Uniform" />
            <!-- Text и Foreground выставляет RepeatLabelStyle. -->
            <TextBlock x:Name="RepeatLabel"
                       Style="{StaticResource RepeatLabelStyle}"
                       FontSize="12"
                       VerticalAlignment="Center"
                       Margin="6,0,0,0" />
        </StackPanel>
    </Button>
</Grid>
```

- [ ] **Step 3: Define the icon-toggle styles**

The XAML in step 2 already references four styles by key (`PlayPauseIconStyle`, `MuteIconStyle`, `RepeatIconStyle`, `RepeatLabelStyle`). Define them now. Add a `<StackPanel.Resources>` block as the first child of the parent `<StackPanel>` that wraps the seek/transport/utility blocks (the same panel that hosts the new player bar). If a different panel type is used, place the resources inside its `.Resources` element with the same content:

```xml
<StackPanel.Resources>
    <Style x:Key="PlayPauseIconStyle" TargetType="Path">
        <Setter Property="Data" Value="{StaticResource IconPlayGeometry}" />
        <Style.Triggers>
            <DataTrigger Binding="{Binding IsPlaying}" Value="True">
                <Setter Property="Data" Value="{StaticResource IconPauseGeometry}" />
            </DataTrigger>
        </Style.Triggers>
    </Style>

    <Style x:Key="MuteIconStyle" TargetType="Path">
        <Setter Property="Data" Value="{StaticResource IconVolumeGeometry}" />
        <Style.Triggers>
            <DataTrigger Binding="{Binding IsMuted}" Value="True">
                <Setter Property="Data" Value="{StaticResource IconMutedGeometry}" />
            </DataTrigger>
        </Style.Triggers>
    </Style>

    <Style x:Key="RepeatIconStyle" TargetType="Path">
        <Setter Property="Fill" Value="{StaticResource MutedForegroundBrush}" />
        <Style.Triggers>
            <DataTrigger Binding="{Binding RepeatMode}" Value="Current">
                <Setter Property="Fill" Value="{StaticResource PrimaryBrush}" />
            </DataTrigger>
            <DataTrigger Binding="{Binding RepeatMode}" Value="Library">
                <Setter Property="Fill" Value="{StaticResource PrimaryBrush}" />
            </DataTrigger>
        </Style.Triggers>
    </Style>

    <Style x:Key="RepeatLabelStyle" TargetType="TextBlock">
        <Setter Property="Text" Value="Off" />
        <Setter Property="Foreground" Value="{StaticResource MutedForegroundBrush}" />
        <Style.Triggers>
            <DataTrigger Binding="{Binding RepeatMode}" Value="Current">
                <Setter Property="Text" Value="1" />
                <Setter Property="Foreground" Value="{StaticResource PrimaryBrush}" />
            </DataTrigger>
            <DataTrigger Binding="{Binding RepeatMode}" Value="Library">
                <Setter Property="Text" Value="All" />
                <Setter Property="Foreground" Value="{StaticResource PrimaryBrush}" />
            </DataTrigger>
        </Style.Triggers>
    </Style>
</StackPanel.Resources>
```

The Path/TextBlock elements in step 2 reference these via `Style="{StaticResource ...}"`, so locally setting `Data`, `Text`, or `Foreground` would override the style and break the toggle — only set those locally where the value is constant (e.g. `Fill="{StaticResource BackgroundBrush}"` on `PlayPauseIcon`, which never changes color).

- [ ] **Step 4: Add IconButtonStyle and PrimaryIconButtonStyle to ButtonStyles.xaml**

Open `MusicLibrary/Resources/ButtonStyles.xaml` and append (before the closing `</ResourceDictionary>`):

```xml
<!-- Маленькая иконочная кнопка для транспорта и утилит плеера. -->
<Style x:Key="IconButtonStyle" TargetType="Button">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderThickness" Value="0" />
    <Setter Property="Padding" Value="8" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="Width" Value="40" />
    <Setter Property="Height" Value="40" />
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border x:Name="Bg" Background="{TemplateBinding Background}" CornerRadius="20" Padding="{TemplateBinding Padding}">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="Bg" Property="Background" Value="#1AD4A574" />
                    </Trigger>
                    <Trigger Property="IsEnabled" Value="False">
                        <Setter Property="Opacity" Value="0.4" />
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>

<!-- Главная Play/Pause кнопка: золотая заливка, крупнее обычной иконочной. -->
<Style x:Key="PrimaryIconButtonStyle" TargetType="Button" BasedOn="{StaticResource IconButtonStyle}">
    <Setter Property="Background" Value="{StaticResource PrimaryBrush}" />
    <Setter Property="Width" Value="56" />
    <Setter Property="Height" Value="56" />
    <Setter Property="Margin" Value="6,0" />
</Style>
```

- [ ] **Step 5: Add NullToBooleanConverter for the seek slider IsEnabled**

Create `MusicLibrary/Converters/NullToBooleanConverter.cs`:

```csharp
using System.Globalization;
using System.Windows.Data;

namespace MusicLibrary.Converters;

/// <summary>
/// Конвертер: null → false, всё остальное → true.
/// Используется для отключения seek-слайдера, когда нет играющего трека.
/// </summary>
public sealed class NullToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
```

Register it in `MusicLibrary/App.xaml` next to the other converters (after `StatusKindToBrushConverter`):

```xml
<converters:NullToBooleanConverter x:Key="NullToBooleanConverter" />
```

- [ ] **Step 6: Build the project and verify XAML compiles**

Run:

```powershell
dotnet build .\MusicLibrary\MusicLibrary.csproj
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 7: Commit**

```powershell
git add .\MusicLibrary\MainWindow.xaml `
        .\MusicLibrary\Resources\ButtonStyles.xaml `
        .\MusicLibrary\Converters\NullToBooleanConverter.cs `
        .\MusicLibrary\App.xaml
git commit -m "feat: add MusicBakh player bar and hotkeys"
```

---

## Task 11: MainWindow.xaml.cs — Seek Slider Event Handlers

**Files:**
- Modify: `MusicLibrary/MainWindow.xaml.cs`

- [ ] **Step 1: Add the two event handlers**

Open `MusicLibrary/MainWindow.xaml.cs`. Add these `using` directives at the top if not already present:

```csharp
using MusicLibrary.ViewModels;
using System.Windows.Input;
```

Append these methods inside the `MainWindow` class:

```csharp
// Drag по seek-слайдеру: ставим флаг, чтобы тик прогресс-таймера не перетёр Value.
private void OnSeekDragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
{
    if (DataContext is MainViewModel viewModel)
    {
        viewModel.IsSeeking = true;
    }
}

// Отпускание мыши: одинаково отрабатывает и завершение drag, и одиночный клик
// благодаря IsMoveToPointEnabled — Value уже находится в финальной позиции.
private void OnSeekPreviewMouseUp(object sender, MouseButtonEventArgs e)
{
    if (DataContext is not MainViewModel viewModel)
    {
        return;
    }

    if (sender is System.Windows.Controls.Slider slider)
    {
        viewModel.SeekToCommand.Execute(TimeSpan.FromSeconds(slider.Value));
    }

    viewModel.IsSeeking = false;
}
```

- [ ] **Step 2: Build to confirm wiring**

Run:

```powershell
dotnet build .\MusicLibrary\MusicLibrary.csproj
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 3: Commit**

```powershell
git add .\MusicLibrary\MainWindow.xaml.cs
git commit -m "feat: add seek slider event handlers"
```

---

## Task 12: MainWindow.xaml.cs — Wire Settings Storage At Startup

**Files:**
- Modify: `MusicLibrary/MainWindow.xaml.cs`

Composition root in this codebase lives in `MainWindow.xaml.cs` (not `App.xaml.cs`, which is empty). The constructor builds all services and constructs `MainViewModel`. We extend the constructor to load settings before the audio service is created.

- [ ] **Step 1: Inject storage and apply settings**

Open `MusicLibrary/MainWindow.xaml.cs`. Replace the `MainWindow()` constructor body with:

```csharp
public MainWindow()
{
    InitializeComponent();

    // В учебной работе логика располагалась в окне. Здесь окно только собирает зависимости,
    // а сценарии приложения выполняет MainViewModel через сервисы.
    var storage = new JsonUserTrackStorage();
    var repository = new CompositeTrackRepository(new InMemoryTrackRepository(), storage);

    var sharedHttpClient = new HttpClient();
    var musicBrainz = new MusicBrainzClient(sharedHttpClient);
    var tagReader = new TagLibSharpTagReader();
    var genreNormalizer = new RussianGenreNormalizer();
    var itunesClient = new ItunesCoverClient(sharedHttpClient);
    var metadataResolver = new DefaultMetadataResolver(tagReader, musicBrainz, itunesClient, genreNormalizer);

    var procedural = new ProceduralCoverGenerator();
    var coverResolver = new CompositeCoverResolver(itunesClient, procedural);

    var importer = new TrackImporter(storage, metadataResolver, coverResolver, sharedHttpClient);
    var openFileDialog = new OpenFileDialogService();
    var addTrackDialog = new AddTrackDialogService(openFileDialog, importer);

    // Поднимаем настройки плеера до создания плеера и ViewModel —
    // громкость и mute должны примениться до первой команды Play.
    var playerSettingsStorage = new JsonPlayerSettingsStorage(JsonPlayerSettingsStorage.DefaultPath);
    PlayerSettings playerSettings = playerSettingsStorage.Load();

    var audioPlayerService = new MediaPlayerAudioService();
    audioPlayerService.Volume = playerSettings.Volume;
    audioPlayerService.IsMuted = playerSettings.IsMuted;

    _viewModel = new MainViewModel(
        repository,
        new FileService(),
        new SaveFileDialogService(),
        audioPlayerService,
        addTrackDialog,
        storage,
        new MessageBoxConfirmationService(),
        playerSettingsStorage);

    // RepeatMode выставляется после конструктора ViewModel, потому что setter
    // также сохраняет настройки — это безвредная повторная запись.
    _viewModel.RepeatMode = playerSettings.RepeatMode;

    DataContext = _viewModel;
}
```

The seek event handlers (`OnSeekDragStarted` and `OnSeekPreviewMouseUp`) added in Task 11 stay below the constructor. The `OnClosed` override stays unchanged.

- [ ] **Step 2: Build to confirm composition compiles**

Run:

```powershell
dotnet build .\MusicLibrary\MusicLibrary.csproj
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 3: Run the app to verify nothing crashes at startup**

Run:

```powershell
dotnet run --project .\MusicLibrary\MusicLibrary.csproj
```

Verify the window opens. Close it.

- [ ] **Step 4: Commit**

```powershell
git add .\MusicLibrary\MainWindow.xaml.cs
git commit -m "feat: load player settings at startup"
```

---

## Task 13: Append §11 To work_diff.md

**Files:**
- Modify: `MusicLibrary/work_diff.md`

- [ ] **Step 1: Append the section**

Open `MusicLibrary/work_diff.md` and append at the end:

```markdown

## 11. Интерактивные элементы плеера

В работе описано базовое воспроизведение: выбрал трек — нажал кнопку — играет. Громкости, перемотки и режимов повтора в работе не предусмотрено.

Второй UX-пакет MusicBakh добавляет полноценную панель плеера:

- активный seek-слайдер с перетаскиванием и кликом по дорожке;
- кнопки перемотки на ±10 секунд;
- кнопки «предыдущий» и «следующий» — переход по видимому списку библиотеки;
- слайдер громкости (0–100%) и отдельная кнопка «без звука»;
- переключатель режима повтора: Off / Current (повтор трека) / Library (повтор всей видимой библиотеки);
- авто-переход к следующему треку после завершения текущего, с учётом режима повтора;
- горячие клавиши: Space — Play/Pause, ←/→ — перемотка ±10 секунд, Ctrl+←/→ — предыдущий/следующий трек, M — без звука, R — следующий режим повтора;
- сохранение громкости, состояния mute и режима повтора между запусками приложения (`%LocalAppData%\MusicBakh\player-settings.json`).

Известное ограничение: когда фокус находится на seek-слайдере, слайдере громкости или ListBox библиотеки/истории, клавиши ←/→ перехватываются этими контролами и до Window не доходят. Перенести их через `KeyDown + e.Handled` потребует code-behind — отнесено в следующий пакет.

Причина: базовая работа описывает «прослушать выбранный трек», но реальный плеер без перемотки и громкости неудобен. Управление воспроизведением — ожидаемый минимум для современного desktop-плеера, и без него UX из первого пакета (двойной клик, подсветка играющего трека) не доходит до конечного смысла.

Функция проекта не меняется по смыслу: библиотека, фильтр, воспроизведение, сохранение и история остаются теми же сценариями. Добавляются интерактивные элементы управления, которые относятся к UX, а не к новой функциональности.
```

- [ ] **Step 2: Commit**

```powershell
git add .\MusicLibrary\work_diff.md
git commit -m "docs: record interactive playback controls package"
```

---

## Task 14: Full Verification

**Files:**
- Verify all modified files.

- [ ] **Step 1: Run the full automated test suite**

Run:

```powershell
dotnet test .\MusicLibrary.sln
```

Expected:

```text
Пройден!  - не пройдено: 0
```

The total count should be 63 (existing) + 10 (strategy) + 6 (storage) + 16 (new ViewModel tests) = 95.

- [ ] **Step 2: Run a Release build**

Run:

```powershell
dotnet build .\MusicLibrary.sln -c Release
```

Expected:

```text
Сборка успешно завершена.
    Предупреждений: 0
    Ошибок: 0
```

- [ ] **Step 3: Run the app and click through every new control**

Run:

```powershell
dotnet run --project .\MusicLibrary\MusicLibrary.csproj
```

Verify visually and by interaction:

- The window title is still "MusicBakh".
- A new player bar is visible in the center column under the title/artist metadata.
- Pick any track and double-click — playback starts, the seek slider begins moving.
- Drag the seek slider — the track jumps to that position when released.
- Click on the seek track without dragging — the track jumps to the click position.
- Drag the volume slider down to 0 — playback becomes silent; the percentage label updates.
- Click the mute button — playback becomes silent without changing the volume slider; the icon switches to muted.
- Click mute again — sound returns.
- Click repeat once — the label changes to "1" and the icon goes gold; let the track end — it restarts.
- Click repeat again — the label changes to "All"; let the last track in the filter end — it wraps to the first track.
- Press Space — play/pause toggles.
- Press Ctrl+→ — the next track in the visible list starts.
- Press Ctrl+← — goes back.
- Press M — mute toggles.
- Press R — repeat cycles.
- Quit the app and relaunch — volume, mute state, and repeat mode are restored from the previous session.

Close the app after verification.

- [ ] **Step 4: Verify the settings file was written**

Run:

```powershell
Get-Content "$env:LocalAppData\MusicBakh\player-settings.json"
```

Expected: a JSON object with `volume`, `isMuted`, `repeatMode` fields reflecting the last interaction.

- [ ] **Step 5: Review the diff stat for the package**

Run:

```powershell
git log --oneline f376d9d..HEAD
git diff --stat f376d9d..HEAD -- MusicLibrary MusicLibrary.Tests
```

Expected commit list (in order):

```text
feat: add RepeatMode enum
feat: add playback queue strategies
feat: add JSON player settings storage
feat: add Volume and IsMuted to audio player service
feat: add volume, mute, repeat-mode state with persistence
feat: add skip, prev, next, mute, cycle-repeat commands
feat: add SeekToCommand and IsSeeking timer guard
feat: dispatch MediaEnded through queue strategy
feat: add player icons resource dictionary
feat: add MusicBakh player bar and hotkeys
feat: add seek slider event handlers
feat: load player settings at startup
docs: record interactive playback controls package
```

(plus all package-1 commits before this point.)

`git status --short` should show no untracked files related to this package.

- [ ] **Step 6: Commit any final polish**

If manual smoke surfaced UI tweaks, fold them in:

```powershell
git add .\MusicLibrary
git commit -m "fix: polish playback control interactions"
```

If nothing changed:

```powershell
git status --short
```

Expected: clean tree (modulo external untracked files like `.claude/`).

---

## Self-Review Checklist

- [x] Spec coverage: every section of the design spec is mapped to a task above (RepeatMode → Task 1; strategies → Task 2; storage → Task 3; service extension → Task 4; ViewModel state + persistence → Task 5; commands → Task 6 + 7; dispatch → Task 8; icons → Task 9; UI + hotkeys → Task 10; seek handlers → Task 11; composition → Task 12; work_diff → Task 13; verification → Task 14).
- [x] Placeholder scan: every step contains the actual code, command, or markdown content needed; no "TBD" / "implement later" / "similar to" markers.
- [x] Type consistency: `IPlaybackQueueStrategy.GetNext`, `IPlayerSettingsStorage.Load/Save`, `PlayerSettings(double, bool, RepeatMode)`, `RepeatMode { Off, Current, Library }`, `MainViewModel.{Volume, IsMuted, IsSeeking, RepeatMode, SkipForwardCommand, SkipBackwardCommand, PreviousTrackCommand, NextTrackCommand, ToggleMuteCommand, CycleRepeatModeCommand, SeekToCommand}`, slider event handler names `OnSeekDragStarted`/`OnSeekPreviewMouseUp` — all consistent across tasks.
- [x] Comments in C# and XAML are in Russian throughout; identifier names stay English.
- [x] Out-of-scope items from the spec (queue, shuffle, equalizer, NAudio, lyrics, atomic write, hotkey override, resume-on-restart, gapless) do not appear in any task.

## Invariants Checklist

- `SelectedTrack` and `PlayingTrack` semantics from package 1 are preserved.
- `_pendingHistoryTrack` mechanism keeps working for auto-next: each new track is added to history only after its `MediaOpened`.
- `MediaFailed` always ends the auto-next chain.
- `IsSeeking = true` blocks the timer's writes to `CurrentPosition` but does not block the user's writes via `SeekToCommand`.
- Save-on-change is unconditional for `Volume`, `IsMuted`, `RepeatMode` — no throttle, no debounce.
- Hotkeys live on `Window.InputBindings` only; no `KeyDown` overrides in code-behind.
- Strategies are stateless singletons and never throw.
- `JsonPlayerSettingsStorage.Load()` never throws; `Save()` only swallows IO/permission exceptions.
