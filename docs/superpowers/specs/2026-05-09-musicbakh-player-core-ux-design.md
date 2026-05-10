# MusicBakh Player Core UX Design Spec

Date: 2026-05-09

## Status

Ready for user review.

## Context

The current WPF application started as an individual first-year SPO/OAP project named "Музыкальная библиотека". The written work describes a simpler local music library: display tracks, filter by genre, play a selected audio file, save/copy a file, keep playback history, and check file existence.

The current implementation has already moved beyond the original text:

- lightweight MVVM with `MainViewModel`;
- service interfaces for files, playback, storage, dialogs, metadata, covers and import;
- built-in playback through WPF `MediaPlayer`;
- local fixed library plus user-imported tracks stored in JSON;
- imported metadata and cover resolution;
- real cover images;
- separated `SelectedTrack` and `PlayingTrack`.

`MusicLibrary/work_diff.md` is the canonical place to explain differences between the written work and the implementation. This package must update that file after implementation.

During the UX session, the chosen direction was:

- keep the current dark luxury visual base;
- add more audio-player clarity;
- use `MusicBakh` as the visible product name;
- keep "Музыкальная библиотека" as the educational/functional subtitle;
- make the app feel more complete without turning package 1 into the full playback-control or queue package.

Figma session file:

https://www.figma.com/design/knnrQfduwDpKOWw4WZJQOH

Figma MCP editing is currently rate-limited on the Starter plan, so this spec is the source of truth for package 1 until Figma can be updated again.

## Package Goal

Package 1, "MusicBakh Player Core UX", establishes the first visible product-level UX layer:

1. Rebrand the main window header from generic "Музыкальная Библиотека" to the hybrid identity:
   - primary name: `MusicBakh`;
   - secondary descriptor: `Музыкальная библиотека`.
2. Make the currently playing track visually obvious in the library list.
3. Preserve the distinction between selected track and playing track.
4. Add direct launch affordances:
   - double-click a library track to start playback;
   - double-click or activate a history item to play it again;
   - double-click the central cover as a shortcut reserved for entering Now Playing mode later, but package 1 may only add the command placeholder/disabled affordance if Now Playing is out of scope.
5. Make the central player panel look more like a compact player while avoiding package 2/3/4 functionality creep.

## Non-Goals

Package 1 must not implement:

- automatic next-track playback;
- queue data model;
- History/Queue tabs;
- next/previous commands;
- clickable seek slider;
- volume or mute controls;
- repeat current or repeat queue;
- Now Playing mode shell;
- NAudio or real audio analysis;
- animated cover pulse;
- import wizard redesign;
- persistent playback history, unless already present elsewhere;
- full app icon generation.

If any of these become necessary while implementing package 1, stop and move them into the correct later package.

## Current Code Anchors

Expected relevant files:

- `MusicLibrary/MainWindow.xaml`
- `MusicLibrary/MainWindow.xaml.cs`
- `MusicLibrary/ViewModels/MainViewModel.cs`
- `MusicLibrary/Resources/TrackTemplates.xaml`
- `MusicLibrary/Resources/ListStyles.xaml`
- `MusicLibrary/Resources/ButtonStyles.xaml`
- `MusicLibrary/Resources/Brushes.xaml`
- `MusicLibrary/Resources/Colors.xaml`
- `MusicLibrary/Models/Track.cs`
- `MusicLibrary/Models/PlaybackEntry.cs`
- `MusicLibrary.Tests/MainViewModelTests.cs`
- `MusicLibrary/work_diff.md`

Current useful state already exists:

- `SelectedTrack`: what the user is inspecting.
- `PlayingTrack`: what the audio service is playing.
- `IsSelectedPlaying`: selected track and playing track are the same.
- `ShowOtherPlayingBadge`: a different track is playing than the selected one.
- `PlaybackHistory`: recent confirmed plays.

Package 1 should build on these concepts instead of collapsing them.

## UX Principles

### 1. Selection Is Not Playback

Selecting a track means "show details for this track".

Playing a track means "audio output is currently associated with this track".

These states must remain independent:

- clicking/selecting another track must not stop current playback;
- changing the genre filter must not stop current playback;
- the playing track may disappear from the filtered list, but playback must continue;
- if selected track differs from playing track, the central panel must make that clear.

### 2. Playing State Must Be Visually Strong

The chosen state direction is the expressive option:

- selected track: visible but calm highlight;
- playing track: stronger glow/outline treatment;
- selected + playing: combines selected clarity and playing emphasis;
- playing should be recognizable without relying only on text.

Use restraint:

- glow must be soft, not noisy;
- text readability must remain better than the effect;
- the card must not shift size when state changes;
- no animation in package 1 unless it is simple WPF visual state polish and does not pretend to be audio-reactive.

### 3. MusicBakh Identity Is Light Rebranding

This is not a full redesign. Keep:

- dark background;
- gold accent;
- existing three-column layout;
- cover-forward visual language.

Change:

- window/header title should present `MusicBakh`;
- subtitle or small descriptor should say `Музыкальная библиотека`;
- current note glyph may become a simple `MB`/wave-ish placeholder if no final icon exists yet;
- final `.ico` belongs to the visual identity package, not this one.

### 4. Compact Player, Not Full Control Suite

Package 1 may improve the central panel hierarchy:

- cover;
- title and artist;
- genre/duration;
- playback progress display;
- primary play/pause action;
- stop/save/delete actions.

It may visually prepare for icon buttons, but must not implement:

- next/previous;
- volume;
- mute;
- repeat;
- seek-by-click.

If icon-style controls are introduced in package 1, they must map only to existing commands.

## Required UI Behavior

### Header

Main header should display:

- primary: `MusicBakh`;
- secondary: `Музыкальная библиотека`;
- add-track action remains available and visually aligned with the new header.

Window title should become either:

- `MusicBakh`;
- or `MusicBakh - Музыкальная библиотека`.

Recommendation: `MusicBakh - Музыкальная библиотека`.

### Library Track Cards

Each track card must support four visual states:

1. Normal
   - no selection;
   - not playing.
2. Selected
   - calm highlight;
   - selected dot or selected border is acceptable.
3. Playing
   - prominent playing border/glow;
   - compact audio/play indicator;
   - must not require selected state.
4. Selected + Playing
   - selected state and playing state both visible;
   - no duplicate labels;
   - no layout shift.

The card template should get all state from ViewModel-provided properties or stable binding helpers. Avoid fragile XAML tricks that require comparing object references inside the template unless they are simple and testable.

### Double-Click Track Launch

Double-clicking a track in the library list must:

- select that track if it is not already selected;
- start playback of that track;
- use the same play path as the normal play command;
- preserve file-existence checks;
- preserve `MediaOpened`-confirmed history behavior;
- not duplicate history entries before `MediaOpened`;
- not start two playback operations for one double-click.

Keyboard accessibility should not regress:

- existing button-based playback must still work;
- Enter-to-play may be considered if simple, but is not required for package 1.

### History Replay

History item activation must:

- select the history item's track;
- start playback through the same play path as library playback;
- handle missing files with the normal error message;
- not create a history entry until the replayed media opens successfully;
- keep the history item template readable.

Preferred interaction:

- double-click history item to replay;
- optional small replay icon can be added later if it does not clutter the panel.

### Other Playing Badge

When `SelectedTrack` differs from `PlayingTrack`, the central panel must show a concise "currently playing" badge.

The badge must:

- include artist and title;
- not steal primary focus from the selected track details;
- remain visible enough to avoid user confusion;
- not imply that clicking it is supported unless a command exists.

Optional package 1 behavior:

- clicking the badge selects `PlayingTrack`.

If implemented, this behavior must be tested.

### Central Panel

The central panel must remain stable across states:

- no selected track;
- selected but not playing;
- selected and playing;
- different track playing;
- playback failed;
- imported user track selected;
- fixed built-in track selected.

Text buttons may remain if icon button design is deferred, but spacing and hierarchy should be improved around the now-playing state.

The progress display remains read-only in package 1.

## ViewModel Design

### Required State

The ViewModel must expose enough state for XAML to render track cards safely:

- selected track identity;
- playing track identity;
- whether a track card is selected;
- whether a track card is playing;
- whether a track card is both selected and playing.

Acceptable implementation options:

1. Add lightweight computed helpers on `Track` wrappers or card view models.
2. Add an `IsTrackPlaying(Track track)` style method only if binding/event refresh remains clean.
3. Use converter/multi-binding only if tests and XAML remain maintainable.

Recommendation: introduce a small presentation model for displayed tracks only if XAML state binding becomes awkward. Do not rewrite the entire repository or storage model for this package.

### Commands

Package 1 should introduce explicit commands instead of hiding behavior in event handlers:

- `PlayTrackCommand` or equivalent parameterized command for starting a specific track;
- `ReplayHistoryEntryCommand` or equivalent parameterized command;
- optional `SelectPlayingTrackCommand`;
- optional `OpenNowPlayingCommand` placeholder only if needed by UI.

The existing `PlayPauseCommand` must keep working for the selected track.

### Core Playback Path

All playback entry points must converge into one internal method, for example:

- selected-track play button;
- library double-click;
- history replay.

The method must preserve the existing rules:

- validate selected/input track;
- check file exists before opening;
- pause only when current selected playing track is already playing and the user invokes play/pause;
- when starting a different track, reset the old playback state;
- add history only after `MediaOpened` for the correct file path;
- ignore stale `MediaOpened` events.

## Required Comments

Package 1 must add short code comments in the following places if the touched implementation is not self-evident:

1. **Shared playback-start method**
   - Comment why all play entry points route through one method.
   - Purpose: prevent double-click/history replay from bypassing file checks or history timing.

2. **Playing state refresh logic**
   - Comment why UI state must be refreshed when `SelectedTrack` or `PlayingTrack` changes.
   - Purpose: selected and playing are intentionally independent.

3. **History replay command**
   - Comment that replaying from history starts playback through the normal path and must not add history optimistically.

4. **Track-card state binding/converter**
   - If a converter, multi-binding, or presentation wrapper is used, comment the selected-vs-playing distinction.

5. **Double-click event bridge**
   - If XAML/code-behind is used to bridge `MouseDoubleClick` to a command, comment that code-behind only forwards UI gestures and contains no playback logic.

Do not add comments to obvious property assignments or trivial XAML styling.

## Failure Patterns

### Missing File

When a user starts playback from:

- play button;
- library double-click;
- history item replay;

and the file is missing:

- no crash;
- no history entry;
- no stale `PlayingTrack`;
- status message shows the missing path or a clear user-facing error;
- previous valid playback should stop only if the new track actually began replacing it. If the implementation currently stops before validating the new file, the spec should prefer validating before stopping where practical.

### Stale MediaOpened

If track A is opened, then track B is started before A reports `MediaOpened`:

- A's late event must not add A to history;
- A's late event must not update duration/progress for B;
- B remains the only pending history candidate.

This behavior already exists and must not regress.

### Double-Click Reentrancy

Double-clicking quickly must not:

- add duplicate history rows before `MediaOpened`;
- open the same file twice if the selected track is already playing;
- leave the UI in a paused-but-playing visual state.

If debounce is needed, it should be minimal and localized.

### Playing Track Hidden By Filter

If the user starts a rock track and then switches filter to another genre:

- playback continues;
- `PlayingTrack` remains set;
- central panel indicates what is playing;
- list does not need to show the hidden playing track in package 1.

Queue behavior for filtered lists belongs to a later package.

### History Item For Deleted User Track

If history contains a user-imported track that has since been deleted:

- replay must fail gracefully with the normal file-missing path;
- no crash;
- no history entry;
- UI status explains the issue.

Removing stale history entries automatically is optional and belongs to a later state-system/history package.

### Deleted Currently Playing User Track

If the selected user track is deleted while playing:

- current playback must stop or reset cleanly;
- `PlayingTrack` clears;
- selected item clears or moves to a safe state;
- no card remains visually marked as playing.

This behavior already partially exists and must not regress.

### Null Selection

With no selected track:

- play/save/delete commands are disabled or show clear errors if invoked programmatically;
- central panel shows a calm empty state;
- no binding errors should appear in output due to null `SelectedTrack`.

## Accessibility And Usability

Minimum expectations:

- all primary actions remain reachable by mouse;
- text remains readable at the current minimum window size;
- selected and playing states are distinguishable by more than color when feasible;
- no state relies only on glow;
- controls should have clear labels/tooltips when icon-only buttons are introduced;
- Russian UI text remains consistent.

Icon-only controls are allowed only if their function is obvious or a tooltip is defined.

## Visual Constraints

Keep:

- dark base;
- gold accent;
- real cover art;
- compact three-column desktop layout.

Avoid:

- excessive neon;
- heavy animated effects;
- large marketing-style hero treatment;
- UI cards nested inside decorative cards unless the existing WPF structure already requires it;
- layout shifts when a track changes state;
- text overflow in buttons or cards.

## Testing Requirements

Unit tests should cover ViewModel behavior, not XAML visuals directly.

Required tests:

1. Starting playback through the selected play command still works.
2. Starting playback for a specific library track selects it and uses the normal playback path.
3. Replaying a history item selects that track and uses the normal playback path.
4. History is not added before `MediaOpened` for library double-click or history replay.
5. `MediaOpened` adds history only for the pending correct track.
6. Missing file from a specific-track start path shows an error and does not add history.
7. Selecting a different track while another track plays does not stop playback.
8. Changing genre filter while a different track plays does not clear `PlayingTrack`.
9. Deleting a currently playing user track clears playback state.
10. Playing-state computed properties raise property changed notifications when `PlayingTrack` changes.

Visual verification should include a manual checklist:

- normal card;
- selected card;
- playing card;
- selected + playing card;
- playing track hidden by filter;
- other-playing badge;
- history replay;
- header branding;
- empty selected state;
- user track delete button still visible only for user tracks.

## Work Diff Update

After implementation, update `MusicLibrary/work_diff.md` with a new section:

Suggested title:

`## 10. MusicBakh Player Core UX`

It should explain:

- visible name changed to `MusicBakh`, while the project remains a musical library for the written work;
- selected track and playing track are still intentionally separate;
- playing track receives a clearer visual state;
- library/history replay gestures route through the same playback logic;
- this package does not yet implement queue, interactive seek, volume, repeat, Now Playing mode or NAudio pulse.

## Acceptance Criteria

Package 1 is complete when:

- the app header uses the MusicBakh hybrid identity;
- the library list clearly distinguishes normal, selected, playing, selected+playing states;
- double-clicking a library track starts playback through the normal validated path;
- replaying from history starts playback through the normal validated path;
- history timing remains `MediaOpened`-confirmed;
- selecting/filtering does not stop current playback;
- central panel clearly handles selected-vs-playing mismatch;
- tests pass;
- Release build passes with 0 warnings;
- `work_diff.md` documents the new differences.

## Open Questions

These are intentionally deferred out of package 1:

- exact final `.ico` artwork for MusicBakh;
- exact Now Playing mode layout;
- queue tab layout;
- volume/repeat button positions;
- NAudio package choice and visualization algorithm.

No package 1 implementation should block on these questions.
