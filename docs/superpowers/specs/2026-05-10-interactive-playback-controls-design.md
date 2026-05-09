# Interactive Playback Controls Design Spec

Date: 2026-05-10

## Status

Ready for user review.

## Context

The MusicBakh WPF application has shipped its first UX package: rebrand, selected-vs-playing visual separation, double-click playback, compact center panel. Playback itself remains rudimentary — there is a Play/Pause button, a Stop button, and a one-way progress bar that the user cannot interact with. There is no volume control, no mute, no seek, no track navigation, no repeat behavior. After a track ends, the player simply halts.

This spec defines package 2 of the MusicBakh roadmap: **Interactive Playback Controls**. The goal is a functional, modern player bar comparable to baseline desktop players (Spotify, AIMP, foobar2000), built on top of the existing `MediaPlayer`-based service.

Package 2 is sized to be implementable as a single plan and reviewable as a single PR. Three further packages are planned after this one before the first release; their content is out of scope here.

The canonical write-up for differences between the written individual project and the implementation is `MusicLibrary/work_diff.md`. This package must add a new section to that file at the end of implementation.

## Package Goal

Add an interactive playback control bar to the center column of MusicBakh, with seek, volume, mute, repeat, track navigation, skip ±10s, hotkeys, and persistence of user preferences across application restarts.

## Scope

### In scope

- Active seek slider — drag-to-seek, with click-to-jump, with anti-feedback protection.
- Skip ±10 seconds buttons.
- Previous track / Next track buttons that traverse `DisplayedTracks` (the genre-filtered visible list).
- Volume slider (0–100%).
- Mute toggle.
- Repeat-mode toggle with three states: Off, Repeat current, Repeat library.
- Auto-next behavior on `MediaEnded`, dispatched through a strategy chosen by the current repeat mode.
- Player-style icons rendered as XAML `Path` geometry (no system font dependencies).
- Window-level hotkeys for the most-used controls.
- Persistence of `Volume`, `IsMuted`, `RepeatMode` between application launches.
- Test coverage for strategies, settings storage, and the new ViewModel surface.

### Out of scope (explicit)

- Queue tab / separate "Up next" panel / Add-to-queue.
- Shuffle / random playback.
- Crossfade / gapless playback.
- Equalizer.
- NAudio frequency-domain visualization — already deferred to a later package per package 1's spec.
- Synchronized lyrics.
- Atomic-write of the settings file (temp + rename + fsync).
- Hotkey conflict resolution when focus is on a `Slider` or `ListBox` (those controls intercept `Left`/`Right` natively and require code-behind to override).
- Resume-on-restart of `Position` (the player starts at 0:00 each launch).
- Multi-track gapless decoding (each track opens fresh).

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│ MainWindow.xaml                                         │
│  ├─ PlayerBar (a layout block in the center column)     │
│  │   • Prev / Skip-10 / Play-Pause / Skip+10 / Next     │
│  │   • Seek slider + current/total time                 │
│  │   • Volume slider + Mute button                      │
│  │   • Repeat-mode toggle                               │
│  └─ Window.InputBindings (hotkeys)                      │
└─────────────────────────────────────────────────────────┘
                          ↕ commands / bindings
┌─────────────────────────────────────────────────────────┐
│ MainViewModel  (extended)                               │
│  • RepeatMode {Off, Current, Library}                   │
│  • Volume, IsMuted, IsSeeking                           │
│  • Commands: SkipForward/Back, PreviousTrack, NextTrack │
│              ToggleMute, CycleRepeatMode, SeekTo        │
│  • HandleMediaEnded dispatches to                       │
│    IPlaybackQueueStrategy.GetNext                       │
└──┬─────────────────────────────────┬────────────────────┘
   ↓                                 ↓
┌──────────────────────┐    ┌────────────────────────────┐
│ IAudioPlayerService  │    │ IPlaybackQueueStrategy     │
│  + Volume (0..1)     │    │   GetNext(current, list)   │
│  + IsMuted           │    │ Implementations:           │
│  + Position (existing)│   │   NoRepeatStrategy         │
│ MediaPlayerAudioSvc  │    │   RepeatCurrentStrategy    │
│   proxies onto       │    │   RepeatLibraryStrategy    │
│   System.Windows.    │    │ Selected per call          │
│   Media.MediaPlayer  │    │   from RepeatMode          │
└──────────────────────┘    └────────────────────────────┘

┌──────────────────────────────────────┐
│ IPlayerSettingsStorage (new)         │
│   Load() → PlayerSettings record     │
│   Save(settings)                     │
│ JsonPlayerSettingsStorage            │
│   %LocalAppData%\MusicBakh\          │
│   player-settings.json               │
└──────────────────────────────────────┘
```

### Files created

- `MusicLibrary/Models/RepeatMode.cs` — enum.
- `MusicLibrary/Services/Playback/IPlaybackQueueStrategy.cs` plus `NoRepeatStrategy.cs`, `RepeatCurrentStrategy.cs`, `RepeatLibraryStrategy.cs`.
- `MusicLibrary/Services/Storage/IPlayerSettingsStorage.cs`, `JsonPlayerSettingsStorage.cs`, `PlayerSettings.cs`.
- `MusicLibrary/Resources/PlayerIcons.xaml` — Path geometry for the seven player icons.
- `MusicLibrary.Tests/PlaybackQueueStrategyTests.cs`, `JsonPlayerSettingsStorageTests.cs`.

### Files modified

- `MusicLibrary/Services/Playback/IAudioPlayerService.cs` — add `Volume`, `IsMuted`.
- `MusicLibrary/Services/Playback/MediaPlayerAudioService.cs` — proxy to `MediaPlayer.Volume` / `MediaPlayer.IsMuted`.
- `MusicLibrary/ViewModels/MainViewModel.cs` — new state, new commands, seek-protection logic, `MediaEnded` dispatch.
- `MusicLibrary/MainWindow.xaml` — replace existing transport controls in the center column with the new player bar; add `Window.InputBindings`.
- `MusicLibrary/MainWindow.xaml.cs` — two thin event handlers for `Thumb.DragStarted` / `Thumb.DragCompleted` on the seek slider (the only code-behind in this package).
- `MusicLibrary/App.xaml.cs` — wire the new storage and load settings before the ViewModel is constructed.
- `MusicLibrary/App.xaml` — register any reusable styles for player buttons / icon brushes.
- `MusicLibrary.Tests/MainViewModelTests.cs` — extend the fake `IAudioPlayerService` and add command/dispatch tests.
- `MusicLibrary/work_diff.md` — append section 11 explaining the package.

## Service layer

### `IAudioPlayerService` extensions

```csharp
public interface IAudioPlayerService : IDisposable
{
    // ...existing members...

    double Volume { get; set; }   // 0.0..1.0; setter clamped by implementation
    bool IsMuted { get; set; }
}
```

`MediaPlayerAudioService` proxies directly to `_player.Volume` and `_player.IsMuted`. `MediaPlayer` does not throw on these properties. The setter for `Volume` clamps to `[0.0, 1.0]` defensively before assigning.

### `IPlaybackQueueStrategy`

```csharp
public interface IPlaybackQueueStrategy
{
    Track? GetNext(Track current, IReadOnlyList<Track> displayedTracks);
}
```

Three stateless singleton implementations.

| Strategy | Behavior |
|---|---|
| `NoRepeatStrategy` | Returns the next track by index in `displayedTracks`. Returns `null` if `current` is the last item, not present, or the list is empty. |
| `RepeatCurrentStrategy` | Always returns `current` regardless of list contents. |
| `RepeatLibraryStrategy` | Returns the next track by index, wrapping to index 0 after the last. If `current` is not in the list, returns the first item, or `null` if the list is empty. A list containing one track returns that same track (intentional infinite repeat). |

`MainViewModel.HandleMediaEnded` resolves the strategy on each call:

```csharp
private static IPlaybackQueueStrategy ResolveStrategy(RepeatMode mode) => mode switch
{
    RepeatMode.Current => RepeatCurrentStrategy.Instance,
    RepeatMode.Library => RepeatLibraryStrategy.Instance,
    _ => NoRepeatStrategy.Instance,
};
```

### `IPlayerSettingsStorage`

```csharp
public interface IPlayerSettingsStorage
{
    PlayerSettings Load();              // never throws; returns Default on any failure
    void Save(PlayerSettings settings); // swallows IOExceptions to logs
}

public sealed record PlayerSettings(double Volume, bool IsMuted, RepeatMode RepeatMode)
{
    public static PlayerSettings Default { get; } =
        new(Volume: 1.0, IsMuted: false, RepeatMode: RepeatMode.Off);
}
```

JSON file location: `%LocalAppData%\MusicBakh\player-settings.json`.

JSON shape:

```json
{ "volume": 0.7, "isMuted": false, "repeatMode": "Library" }
```

`repeatMode` is serialized as the enum member name via `JsonStringEnumConverter`. This makes future renames produce predictable corruption (caught by the loader) rather than silent integer drift.

### `Load()` behavior matrix

| Condition | Result |
|---|---|
| File does not exist | `PlayerSettings.Default` |
| File exists, JSON valid, all fields present | Parsed values, with `Volume` clamped to `[0, 1]` |
| File exists, JSON corrupted or invalid | `PlayerSettings.Default`, logged warning |
| File exists, `repeatMode` value not a known enum member | `PlayerSettings.Default`, logged warning |

### `Save()` behavior

- Creates `%LocalAppData%\MusicBakh\` if it does not exist.
- Overwrites the file in a single `File.WriteAllText` call. Not atomic. If the process is killed mid-write, the file may be truncated; subsequent `Load()` will return defaults.
- Catches `IOException` and `UnauthorizedAccessException`, logs them, does not throw.

`MainViewModel` calls `Save()` in the setters of `Volume`, `IsMuted`, `RepeatMode`. The volume slider drags trigger many `PropertyChanged` events; this is acceptable because `Save()` writes a single ~50-byte JSON file. No throttling for this package.

## ViewModel changes

### New state

```csharp
private RepeatMode _repeatMode;
private double _volume = 1.0;
private bool _isMuted;
private bool _isSeeking;

public RepeatMode RepeatMode { get; set; }   // setter triggers Save
public double Volume { get; set; }            // setter writes to player + Save
public bool IsMuted { get; set; }             // setter writes to player + Save
public bool IsSeeking { get; set; }           // never persisted
```

### New commands

```csharp
public ICommand SkipForwardCommand { get; }       // Position += 10s, clamp to Duration
public ICommand SkipBackwardCommand { get; }       // Position -= 10s, clamp to Zero
public ICommand PreviousTrackCommand { get; }      // index-1 in DisplayedTracks, no wrap
public ICommand NextTrackCommand { get; }          // index+1 in DisplayedTracks, no wrap
public ICommand ToggleMuteCommand { get; }
public ICommand CycleRepeatModeCommand { get; }    // Off → Current → Library → Off
public ICommand SeekToCommand { get; }             // CommandParameter is TimeSpan
```

`PreviousTrackCommand` / `NextTrackCommand` traverse `DisplayedTracks` from `PlayingTrack` (not `SelectedTrack`). The user expects "next" to mean "after what is currently playing", not "after what is highlighted in the panel". `CanExecute` returns `false` when `PlayingTrack` is null, when `PlayingTrack` is at index 0 (for Prev), or at the last index (for Next), or when `PlayingTrack` is no longer in `DisplayedTracks` at all (e.g. removed user track or filter changed).

### Seek anti-feedback (chosen approach: 1A — `IsSeeking` flag)

The seek slider is `TwoWay`-bound to `ProgressValue`. The `_progressTimer` writes `CurrentPosition` from the audio service every 500ms, which would normally fight against the user's drag. The protection works as follows:

```
Thumb.DragStarted    →  IsSeeking = true
                        Timer tick checks IsSeeking; if true, does NOT update CurrentPosition.
                        The slider Value freely tracks the user's drag.

Thumb.DragCompleted  →  IsSeeking = false
                        SeekToCommand.Execute(TimeSpan.FromSeconds(slider.Value))
                        The next timer tick reads Position from the player and resyncs.
```

XAML wiring:

```xml
<Slider x:Name="SeekSlider"
        Maximum="{Binding ProgressMaximum}"
        Value="{Binding ProgressValue, Mode=TwoWay}"
        IsMoveToPointEnabled="True"
        Thumb.DragStarted="OnSeekDragStarted"
        PreviewMouseUp="OnSeekPreviewMouseUp" />
```

Two thin handlers in `MainWindow.xaml.cs`. Each is one or two lines and forwards to the ViewModel; this is the only code-behind introduced by the package.

Click-to-jump (no drag) is handled uniformly with drag-completion: `PreviewMouseUp` on the entire `Slider` (not just the `Thumb`) fires for both cases — drag-released and click-without-drag. The handler reads the final `Value`, invokes `SeekToCommand`, and clears `IsSeeking`. `Slider.IsMoveToPointEnabled="True"` is set so a click on the track jumps the value to that position before `PreviewMouseUp` fires.

Final wiring summary in `MainWindow.xaml.cs`:

- `Thumb.DragStarted` → `IsSeeking = true`
- `PreviewMouseUp` on the slider → `SeekToCommand.Execute(TimeSpan.FromSeconds(slider.Value))`; `IsSeeking = false`

`Thumb.DragCompleted` is not needed — `PreviewMouseUp` covers both drag and click endings.

### `HandleMediaEnded` dispatch (chosen approach: 2B — strategy)

```csharp
private void HandleMediaEnded()
{
    Track? finished = PlayingTrack;

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

    StartOrResumeTrack(next);
}
```

`StartOrResumeTrack` is the centralized method introduced in package 1. It already handles the `_pendingHistoryTrack` flow correctly, so auto-next entries appear in the history naturally after each track's `MediaOpened` fires.

### `HandleMediaFailed` interaction with auto-next

If a track fails (corrupt file, missing codec, unreadable path), the chain stops: `HandleMediaFailed` calls `ResetPlaybackState` and sets the status. We do not auto-skip to the next track. Auto-skipping on failure would create infinite-loop alerts on a partially broken library.

### Persistence wiring at startup

In `App.xaml.cs` (composition root):

```
1. settingsStorage = new JsonPlayerSettingsStorage(...);
2. settings = settingsStorage.Load();
3. audioPlayerService = new MediaPlayerAudioService();
4. audioPlayerService.Volume = settings.Volume;
5. audioPlayerService.IsMuted = settings.IsMuted;
6. mainViewModel = new MainViewModel(..., settingsStorage);
7. mainViewModel.RepeatMode = settings.RepeatMode;  // setter calls Save again, harmless
```

The redundant `Save` after step 7 is acceptable: the file is small and infrequent. Avoiding it would require an extra `IsLoading` flag on the ViewModel — not worth the complexity.

## UI

### Layout (center column, top to bottom)

```
┌────────────────────────────────────────────────┐
│             ┌───────────────┐                  │
│             │   Cover 176   │                  │
│             └───────────────┘                  │
│                                                │
│         Title                                  │
│         Artist · Genre · Duration              │
│                                                │
│  ─────────────────●───────────────────         │  seek slider
│  1:23                              4:55        │  time labels
│                                                │
│        ⏮    ⏪    ▶    ⏩    ⏭                  │  transport row
│              (Play/Pause larger)               │
│                                                │
│  🔊 ──●──── 70%   🔇    ⏹    🔁 Off            │  utility row
│                                                │
│   [ Сохранить копию ]   [ Удалить ]           │  existing
│   статус-строка                                │  existing
└────────────────────────────────────────────────┘
```

The cover, title, status, Save, and Delete elements keep their existing position and styling. The new seek/transport/utility blocks are inserted between metadata and Save. The legacy text-button "Воспроизвести"/"Пауза" is replaced by the icon-based Play/Pause in the transport row. The existing Stop button is restyled as an `IconStop` button and moved to the utility row, between the mute icon and the repeat toggle — keeping the command reachable without dominating the transport row. Layout updated:

```
🔊 ──●──── 70%   🔇    ⏹    🔁 Off
```

### Icons (Path geometry, in `PlayerIcons.xaml`)

| Resource key | Visual | Concept |
|---|---|---|
| `IconPlay` | Filled triangle right | `M 0,0 L 0,12 L 10,6 Z` |
| `IconPause` | Two vertical bars | Two rectangles 4×12 with a 3-unit gap |
| `IconSkipBack10` | Double triangle left | Two leftward triangles |
| `IconSkipForward10` | Double triangle right | Two rightward triangles |
| `IconPrev` | Bar + triangle left | `M 0,0 H 2 V 12 H 0 Z M 12,0 L 12,12 L 4,6 Z` |
| `IconNext` | Triangle right + bar | Mirror of `IconPrev` |
| `IconVolume` | Speaker with arcs | Trapezoid + two arcs to the right |
| `IconMuted` | Speaker with strike | `IconVolume` plus an overlay stroke |
| `IconRepeatOff` | Grey circular arrow | Path in `MutedForegroundBrush` |
| `IconRepeatCurrent` | Gold arrow with "1" | Path + a TextBlock "1" centered |
| `IconRepeatLibrary` | Full gold circular arrow | Path in `PrimaryBrush` |
| `IconStop` | Filled square | `M 0,0 H 12 V 12 H 0 Z` in `PrimaryBrush` |

Exact path coordinates are encoded in the implementation plan, not here.

Buttons consume icons via `<ContentControl Content="{StaticResource IconPlay}" />` inside a button template. The Play/Pause button is a single Button whose `Content` switches via `DataTrigger` on `IsSelectedPlaying && IsPlaying`.

### Control state (enabled / disabled)

| Control | Disabled when |
|---|---|
| Seek slider | `PlayingTrack is null` |
| Skip ±10s | `PlayingTrack is null` |
| Prev | `PlayingTrack is null` or playing-track is index 0 in `DisplayedTracks` or not in the list |
| Next | `PlayingTrack is null` or playing-track is the last index in `DisplayedTracks` or not in the list |
| Play/Pause | `SelectedTrack is null` (existing rule, unchanged) |
| Volume slider | never |
| Mute | never |
| Repeat toggle | never |

### Hotkeys

```xml
<Window.InputBindings>
    <KeyBinding Key="Space" Command="{Binding PlayPauseCommand}" />
    <KeyBinding Key="Left"  Command="{Binding SkipBackwardCommand}" />
    <KeyBinding Key="Right" Command="{Binding SkipForwardCommand}" />
    <KeyBinding Key="Left"  Modifiers="Control" Command="{Binding PreviousTrackCommand}" />
    <KeyBinding Key="Right" Modifiers="Control" Command="{Binding NextTrackCommand}" />
    <KeyBinding Key="M"     Command="{Binding ToggleMuteCommand}" />
    <KeyBinding Key="R"     Command="{Binding CycleRepeatModeCommand}" />
</Window.InputBindings>
```

**Known limitation:** when focus is on a `Slider` or `ListBox`, `Left`/`Right` are intercepted natively (slider nudge / list navigation) and never reach `Window.InputBindings`. `Space`, `Ctrl+Left`/`Right`, `M`, `R` work regardless of focus. Fixing the arrow case requires `KeyDown` with `e.Handled` in code-behind, which is deferred to a later package. The behavior is documented in `work_diff.md` section 11.

### Repeat-toggle UX

A single Button. The icon and tooltip change with `RepeatMode`:

| Mode | Icon | Tooltip |
|---|---|---|
| Off | `IconRepeatOff` (grey) | «Повтор выключен» |
| Current | `IconRepeatCurrent` (gold + "1") | «Повтор текущего трека» |
| Library | `IconRepeatLibrary` (gold) | «Повтор всей библиотеки» |

A click invokes `CycleRepeatModeCommand`, which advances Off → Current → Library → Off.

## Edge cases

| Case | Behavior |
|---|---|
| `Skip-10s` when `Position < 10s` | Position clamped to `TimeSpan.Zero`. |
| `Skip+10s` when `Position+10 > Duration` | Position clamped to `Duration`; `MediaEnded` fires shortly after. |
| `Prev`/`Next` when `PlayingTrack` was removed from `DisplayedTracks` | Command is disabled (CanExecute returns false). User can press Play on a selected track to restart. |
| Single-track list + `Repeat-Library` | Same track repeats forever. Intentional. |
| Empty `DisplayedTracks` + `MediaEnded` | Strategy returns null. Player stops. Status: «Воспроизведение завершено». |
| `MediaFailed` during the auto-next chain | Chain breaks. `ResetPlaybackState`, status: «Ошибка воспроизведения». No automatic skip. |
| `Volume = 0` and `IsMuted = false` | Distinct state from "muted at any volume". Both yield silence; the persisted state preserves the distinction. |
| `IsMuted = true` and the user moves the volume slider | `IsMuted` is not auto-cleared. Each control owns its own state. |
| `player-settings.json` corrupted | `Load()` returns `PlayerSettings.Default`; the file is overwritten on the next `Save()`. |
| `%LocalAppData%\MusicBakh\` does not exist on first `Save()` | Created via `Directory.CreateDirectory`. |
| `repeatMode` JSON value not a known enum member | Caught as parse failure; `Load()` returns Default. |
| Drag finishes outside the slider track | `Thumb.DragCompleted` still fires — same code path applies. |
| User holds Ctrl+Right repeatedly | Each press advances one track, command's `CanExecute` recomputes after each. |

## Testing

### Strategy tests (`PlaybackQueueStrategyTests.cs`)

```
NoRepeatStrategy
  - GetNext_AtMiddle_ReturnsNextByIndex
  - GetNext_AtLast_ReturnsNull
  - GetNext_EmptyList_ReturnsNull
  - GetNext_CurrentNotInList_ReturnsNull

RepeatCurrentStrategy
  - GetNext_AlwaysReturnsCurrent_EvenIfNotInList

RepeatLibraryStrategy
  - GetNext_AtMiddle_ReturnsNextByIndex
  - GetNext_AtLast_WrapsToFirst
  - GetNext_SingleTrackList_ReturnsSameTrack
  - GetNext_EmptyList_ReturnsNull
  - GetNext_CurrentNotInList_ReturnsFirst
```

### Storage tests (`JsonPlayerSettingsStorageTests.cs`)

Tests use a temporary directory per-test to avoid touching the real `%LocalAppData%`.

```
- Load_FileMissing_ReturnsDefault
- Save_ThenLoad_RoundTripsAllFields
- Load_CorruptedJson_ReturnsDefault
- Load_VolumeOutsideRange_ClampedToZeroOne
- Load_UnknownRepeatMode_ReturnsDefault
- Save_CreatesDirectoryIfMissing
```

### ViewModel tests (extensions to `MainViewModelTests.cs`)

The fake `IAudioPlayerService` gains `Volume`/`IsMuted` properties and a `RaiseEndedForTest` helper. A fake `IPlayerSettingsStorage` records `Save` calls into an in-memory list.

```
- Volume_Setter_UpdatesPlayerAndSaves
- IsMuted_Setter_UpdatesPlayerAndSaves
- RepeatMode_Setter_Saves
- SkipForward_AtEnd_ClampsToDuration
- SkipBackward_AtStart_ClampsToZero
- PreviousTrack_AtFirst_CommandDisabled
- NextTrack_AtLast_CommandDisabled
- PreviousTrack_PlayingTrackNotInList_CommandDisabled
- NextTrack_PlayingTrackNotInList_CommandDisabled
- CycleRepeatMode_CyclesOffCurrentLibraryOff
- MediaEnded_RepeatOff_ClearsPlayingTrack
- MediaEnded_RepeatCurrent_RestartsSameTrack
- MediaEnded_RepeatLibrary_AdvancesAndWrapsAtEnd
- MediaFailed_DuringAutoNext_StopsChain
- IsSeeking_True_ProgressTimerDoesNotOverwriteCurrentPosition
- SeekToCommand_WritesPositionToPlayer
```

Composition-level startup restoration (`Volume`, `IsMuted`, `RepeatMode` applied from storage on app launch) is verified by the manual UI smoke test rather than a unit test, because it is a wiring concern in `App.xaml.cs` rather than ViewModel logic. The setter behaviors themselves are covered by the `*_Setter_UpdatesPlayerAndSaves` tests.

## Implementation guidelines

- All comments in C# and XAML are written in Russian. This matches the existing codebase (see `MediaPlayerAudioService.cs`, `MainViewModel.cs`, `InMemoryTrackRepository.cs`). Identifier names stay English.
- Path geometry strings in `PlayerIcons.xaml` should include a brief Russian comment above each path explaining what the icon represents (e.g. `<!-- Перемотка вперёд: два треугольника, направленные вправо -->`), since the path data itself is opaque.
- The `_pendingHistoryTrack` mechanism from package 1 must continue to work — auto-next tracks should still appear in the history once their `MediaOpened` fires.
- Before claiming the package complete, run the WPF application and click through every new control. Builds and unit tests pass clean even when XAML resource resolution is broken (this lesson is from package 1).

## Verification

After implementation, all of the following must hold:

- `dotnet test MusicLibrary.sln` reports 0 failures.
- `dotnet build MusicLibrary.sln -c Release` reports 0 warnings, 0 errors.
- `dotnet run --project MusicLibrary\MusicLibrary.csproj` opens the app, plays a track, and the new player bar is visible and functional.
- Manual UI smoke (must be done by a human on a real Windows desktop):
  - Drag the seek slider — the track jumps and resumes from the new position.
  - Click on the seek track without dragging — the track jumps to that point.
  - Drag the volume slider down to 0 — playback becomes silent.
  - Click mute — playback becomes silent without changing the volume slider position.
  - Click repeat once: icon shows "1", at end of track it restarts.
  - Click repeat twice: icon shows full circle, at end of last track in filter it wraps to the first.
  - Press Space — Play/Pause toggles.
  - Press `Ctrl+Right` — next track starts.
  - Quit and relaunch — volume, mute state, and repeat mode are restored.
- `MusicLibrary/work_diff.md` ends with `## 11. Интерактивные элементы плеера`.
