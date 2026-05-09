# MusicBakh Player Core UX Full Design

Date: 2026-05-09

Related spec:

`docs/superpowers/specs/2026-05-09-musicbakh-player-core-ux-design.md`

Figma session:

https://www.figma.com/design/knnrQfduwDpKOWw4WZJQOH

## Design Intent

Package 1 turns the current "Музыкальная Библиотека" into the first visible version of **MusicBakh**: a personal, polished local music player that still clearly satisfies the educational project theme.

The design must feel like a natural evolution of the existing WPF app, not a replacement:

- keep the dark, gold-accented look;
- keep the three-column desktop layout;
- make the currently playing track obvious;
- make playback entry points more direct;
- make the center panel feel more like a player;
- avoid queue, seek, volume, repeat, Now Playing and NAudio scope.

The tone is **luxury dark + player clarity**. It should look a little more like a real music app, but not like a neon club screen yet.

## Product Identity

### Naming

Visible product name:

`MusicBakh`

Supporting descriptor:

`Музыкальная библиотека`

Window title:

`MusicBakh - Музыкальная библиотека`

The app should not remove the educational framing. The written work can still describe the project as a music library, while the application itself presents a more product-like name.

### Header Structure

Current header:

- left brand icon block;
- title text;
- add-track button on the right.

Package 1 header:

- left brand mark placeholder;
- primary text: `MusicBakh`;
- secondary text: `Музыкальная библиотека`;
- right action: `Добавить трек`.

The descriptor should be visually smaller than the product name. It should not compete with the title.

### Brand Mark Placeholder

Final app icon is out of scope for package 1, but the header can stop using a generic music note if implementation cost is low.

Preferred placeholder:

- square or rounded-square gold gradient badge;
- text `MB`;
- optional tiny wave-like line if easy in XAML.

Fallback:

- keep the note symbol for now, but place it under the new `MusicBakh` title.

Do not spend package 1 time generating final `.ico` assets.

## Layout

### Global Layout

Keep the current three-column structure:

1. Left: library and genre filter.
2. Center: selected/playing track details and controls.
3. Right: playback history.

No Now Playing mode in package 1.

No responsive layout rewrite in package 1.

Minimum window constraints remain close to current:

- `MinWidth` around 980;
- `MinHeight` around 640.

### Visual Balance

The center column should receive slightly stronger visual hierarchy:

- cover remains the visual anchor;
- playback/progress zone should feel intentionally part of the player;
- status text should be calm and readable;
- action buttons should be grouped more like controls than form buttons.

Left and right panels remain utilitarian: scanning, filtering, replaying.

## Color And Token Use

Use existing palette unless a new token is needed:

- background: `#0A0A0F`;
- card: `#16161F`;
- secondary: `#1F1F2E`;
- accent: `#2A2A3F`;
- primary gold: `#D4A574`;
- dark gold: `#B8864F`;
- foreground: `#E8E8F0`;
- muted foreground: `#9999B3`;
- success: `#78C68A`;
- error: `#E06C75`.

Package 1 may add these semantic brushes:

- `PlayingTrackBrush`: a richer dark-gold/amber overlay;
- `PlayingTrackBorderBrush`: strong gold;
- `PlayingTrackGlowBrush`: gold at low opacity;
- `NowPlayingBadgeBrush`: low-opacity gold fill.

Avoid introducing a new dominant hue. Playing can use gold rather than green; green is better reserved for success messages.

## Typography

Keep current font resources:

- heading: `Playfair Display, Georgia`;
- body: `Montserrat, Segoe UI`.

Header:

- `MusicBakh`: heading font, 28-30 px, semi-bold, gold.
- `Музыкальная библиотека`: body font, 13-14 px, muted.

Track cards:

- title: 15-16 px, semi-bold;
- artist: 12-13 px, muted;
- genre/duration: 11-12 px.

Center panel:

- track title: 22 px, semi-bold;
- artist: 15 px, muted;
- badge/status: 12-13 px.

Do not scale fonts with viewport width.

## Components

### 1. Header Brand Block

Purpose:

Orient the user and introduce MusicBakh.

Structure:

- brand badge 52x52;
- text stack:
  - `MusicBakh`;
  - `Музыкальная библиотека`;
- add-track button aligned right.

States:

- add-track enabled when import services are available;
- disabled appearance should follow existing button disabled style.

Interaction:

- add-track button keeps existing behavior.

Implementation note:

The header should not contain playback logic.

### 2. Genre Filter

Package 1 does not redesign the filter.

Keep:

- label `Фильтр по жанру`;
- current styled ComboBox;
- filtering behavior.

Required invariant:

Changing filter must not stop playback or clear `PlayingTrack`.

### 3. Track Card

Purpose:

Let the user scan the library and distinguish selected vs currently playing.

Base structure:

- cover thumbnail 64x64;
- title;
- artist;
- genre pill;
- duration;
- state indicator area.

Card dimensions must stay stable across all states.

#### Normal State

Visual:

- background: `AccentBrush` or current card background;
- subtle gold border;
- no dot/glow;
- cover visible.

Meaning:

Track is neither selected nor playing.

#### Selected State

Visual:

- slightly brighter/warmer background, likely existing `SelectedTrackBrush`;
- gold border at 2 px or strong 1 px;
- selected dot may remain.

Meaning:

Track details are shown in center panel.

Important:

Selected state must feel calm. It should not look like audio playback.

#### Playing State

Visual:

- stronger gold border;
- soft gold glow around card;
- compact audio/play indicator;
- optional tiny text `играет` only if it does not clutter the card.

Recommended indicator:

- a small equalizer-like group of 3 vertical bars in the right column;
- static in package 1;
- no fake frequency animation.

Meaning:

This track is the active `PlayingTrack`.

Important:

Playing state must be visible even when the card is not selected.

#### Selected + Playing State

Visual:

- combine selected background and playing border/glow;
- indicator visible;
- no duplicate selected dot if it creates noise.

Meaning:

The user is inspecting the same track that is playing.

Implementation preference:

If both selected and playing indicators are too busy, prefer:

- selected = warmer background;
- playing = glow + indicator.

Do not add extra text that shifts layout.

### 4. Center Cover

Purpose:

Show selected track cover and become the future entry point to Now Playing.

Package 1 behavior:

- displays selected track cover;
- keeps existing shadow;
- double-click can be wired as a no-op placeholder only if it does not confuse the user.

Recommendation:

Do not add a visible Now Playing button in package 1. The mode is not implemented yet. Avoid UI that promises unavailable behavior.

If double-click cover is implemented now, it should do nothing user-facing or show a neutral message like "Режим Now Playing будет добавлен позже" only in development builds. Prefer deferring this interaction to the Now Playing package.

### 5. Center Metadata

Structure:

- title;
- artist;
- genre pill;
- duration.

Empty selected state:

- title: `Выберите трек`;
- artist empty or muted helper text;
- genre/duration hidden or neutral.

Do not show empty pills with blank text.

### 6. Progress Display

Package 1 progress remains read-only.

Visual:

- keep `ProgressBar`;
- improve vertical spacing if the current layout feels cramped;
- show current time and duration only when selected track is playing.

When selected track is not the playing track:

- hide progress for selected track;
- show other-playing badge.

Do not implement seeking.

### 7. Other Playing Badge

Purpose:

Prevent confusion when user selects one track while another keeps playing.

Visibility:

Show when:

- `PlayingTrack != null`;
- `SelectedTrack == null` or `SelectedTrack.Id != PlayingTrack.Id`.

Content:

`Играет: {Artist} - {Title}`

Visual:

- low-opacity gold fill;
- subtle gold border;
- small play/equalizer indicator if available;
- text is one line with ellipsis.

Interaction:

Optional in package 1:

- clicking badge selects `PlayingTrack`.

If clickable:

- cursor should indicate click;
- tooltip: `Показать играющий трек`;
- test command behavior.

If not clickable:

- no hover styling that suggests action.

### 8. Action Controls

Current actions:

- play/pause;
- stop;
- save;
- delete for user tracks.

Package 1 may improve grouping:

- primary play/pause remains most prominent;
- stop/save/delete remain secondary;
- delete remains error-colored and visible only for user tracks.

No new playback controls:

- no previous;
- no next;
- no volume;
- no mute;
- no repeat.

Icon buttons:

Allowed only for existing commands if labels/tooltips are clear. Text buttons may remain.

### 9. Status Message

Purpose:

Show user-facing result of operations.

Package 1 does not redesign the full state system.

Keep:

- info/success/error coloring via existing converter;
- centered text in central panel.

Avoid:

- status text overlapping controls;
- overly long messages without wrapping;
- replacing failure handling with exceptions.

### 10. Playback History Item

Purpose:

Show confirmed past plays and allow quick replay.

Structure:

- cover thumbnail;
- title;
- artist;
- played time.

Interaction:

- double-click item replays that track;
- item becomes selected track;
- playback uses normal validated path.

Visual affordance:

- subtle hover style if easy;
- cursor can be hand only if replay is active.

Optional:

- small replay icon on hover is allowed only if it does not clutter the history list.

No history management yet:

- no delete history item;
- no clear history;
- no persistent history work.

## Interaction Design

### Library Single Click

Effect:

- updates `SelectedTrack`;
- center panel displays selected track;
- does not affect `PlayingTrack`;
- does not stop playback.

### Library Double Click

Effect:

1. selects the track;
2. starts the track through shared playback-start method;
3. sets pending history only after successful `Play`;
4. adds history only after matching `MediaOpened`.

Failure:

- missing file shows error;
- no history row;
- previous playback should not be destroyed before the new track passes validation where practical.

### Play/Pause Button

If selected track is playing and audio is playing:

- pauses.

If selected track is the paused playing track:

- resumes.

If selected track differs from playing track:

- starts selected track through shared playback-start method.

If no selected track:

- command disabled or error if invoked programmatically.

### Stop Button

Stops current `PlayingTrack`, even if selected track differs.

After stop:

- `PlayingTrack = null`;
- progress resets;
- no card remains in playing state;
- selected track remains selected.

### History Double Click

Effect:

1. selects `entry.Track`;
2. starts it through shared playback-start method.

Failure:

- if file missing, show error and do not add history.

### Filter Change

Effect:

- rebuilds `DisplayedTracks` for selected genre;
- does not modify `PlayingTrack`;
- does not stop playback;
- if selected track disappears from filter, selected state may remain or clear depending on current app behavior, but playback must remain clear via central badge.

Recommendation:

Keep `SelectedTrack` if possible; clearing selection can make the center panel less useful while audio continues. If current implementation makes this hard, package 1 can defer this nuance as long as `PlayingTrack` remains visible in center badge.

## Data And State Design

### State Model

Core state:

- `_allTracks`: source list for built-in + user tracks;
- `DisplayedTracks`: filtered display list;
- `SelectedTrack`: inspected track;
- `PlayingTrack`: track associated with audio service;
- `_pendingHistoryTrack`: track waiting for confirmed `MediaOpened`;
- `PlaybackHistory`: confirmed played entries.

### Recommended Presentation State

The cleanest design is to make track-card state explicit.

Possible approaches:

#### Option A: Track card wrapper

Create a small view model wrapper around `Track`, for example:

- `TrackListItemViewModel.Track`;
- `IsSelected`;
- `IsPlaying`;
- `IsSelectedPlaying`.

Pros:

- XAML becomes simple;
- state is testable;
- no fragile object comparison in templates.

Cons:

- changes `DisplayedTracks` type;
- more refactor.

#### Option B: MultiBinding/converters

Keep `DisplayedTracks` as `Track`, use converters comparing current item to selected/playing track.

Pros:

- less data structure change.

Cons:

- WPF binding becomes more complex;
- state refresh can be harder;
- tests cover less.

#### Option C: Minimal attached state

Expose selected/playing IDs and bind with converter.

Pros:

- less object coupling;
- stable identity comparison.

Cons:

- still converter-heavy.

Recommendation:

Use the smallest approach that keeps XAML maintainable. If implementation starts accumulating converter hacks, switch to a wrapper. Package 1 should not rewrite repositories/storage.

### Identity Rule

Track identity should compare by stable `Id`, not by display text.

File path comparison remains important only for async media events.

## Command Design

### Required Commands

Add commands or equivalent methods:

- `PlayTrackCommand` with `Track` parameter;
- `ReplayHistoryEntryCommand` with `PlaybackEntry` parameter.

Optional:

- `SelectPlayingTrackCommand`.

Existing commands remain:

- `PlayPauseCommand`;
- `StopCommand`;
- `SaveTrackCommand`;
- `AddTrackCommand`;
- `DeleteTrackCommand`.

### Shared Playback Method

All playback-start entry points should route through one internal method, e.g.:

`StartTrack(Track track, PlaybackStartSource source)`

`PlaybackStartSource` can be an enum if useful:

- `SelectedButton`;
- `LibraryDoubleClick`;
- `HistoryReplay`.

The source is optional. Use it only if it improves logging/status/testing.

Required comment:

Explain that this method exists so library double-click and history replay cannot bypass file checks, stale-event protection or confirmed-history timing.

## Code-Behind Design

MVVM remains the rule.

Code-behind may only bridge UI-specific gestures:

- `MouseDoubleClick` on ListBoxItem;
- `MouseDoubleClick` on history item;
- possibly cover double-click later.

Code-behind must not:

- check file existence;
- call audio service;
- mutate history;
- implement playback branching.

Required comment:

If code-behind is used, comment that it forwards WPF gestures to ViewModel commands and contains no playback logic.

## Failure And Recovery Design

### Missing Track File

User action:

- play button;
- library double-click;
- history replay.

Result:

- show error status;
- do not set `PlayingTrack` to the missing track;
- do not add pending history;
- do not add confirmed history.

Preferred:

- keep existing playback running if the new file fails validation.

### Media Open Race

Existing stale `MediaOpened` protection must remain.

Rule:

Only the currently playing track's matching file path can update duration and confirm history.

### Rapid Double Click

Rule:

Double-click on already playing selected track should not open another duplicate playback session.

If the current behavior pauses on repeated play/pause, double-click playback should call the "start this track" path, not blindly execute `PlayPauseCommand` twice.

### Deleted User Track

If the currently playing user track is deleted:

- stop audio;
- clear `PlayingTrack`;
- remove from displayed/all tracks;
- clear selected if needed;
- no playing state remains visible.

If history item points to deleted user track:

- replay fails with missing-file status.

### Null/Empty States

No selected track:

- center title says `Выберите трек`;
- cover can show neutral placeholder or empty dark frame;
- controls are disabled or visually subdued;
- no blank genre pill.

No history:

- package 1 can keep empty list;
- richer empty state belongs to state-system package.

## Required Implementation Comments

The implementation must include short, useful comments at these points if touched:

1. Shared playback-start method:
   - why it centralizes play button, double-click and history replay.
2. `SelectedTrack`/`PlayingTrack` refresh:
   - why selection is independent from playback.
3. History replay command:
   - why it does not insert history immediately.
4. Track-card state converter/wrapper:
   - why card visual state compares against playing track.
5. Code-behind gesture bridge:
   - why code-behind is only forwarding UI gestures.

Comments should explain intent, not restate obvious code.

## Visual QA Checklist

Verify manually:

- header says `MusicBakh`;
- descriptor says `Музыкальная библиотека`;
- add-track button still aligned and enabled;
- normal track card is calm;
- selected track card is visible but not overdramatic;
- playing track card is unmistakable;
- selected+playing track card is readable;
- playing card does not change height/width;
- switching filter does not stop playback;
- selecting another track while one plays shows other-playing badge;
- missing selected track cover does not break layout;
- history item double-click starts playback;
- user-track delete button still only appears for user tracks;
- no text clips at minimum window size.

## Test Design

### Unit Tests

Focus on ViewModel behavior:

- `PlayTrackCommand_SelectsTrackAndStartsPlayback`;
- `PlayTrackCommand_DoesNotAddHistoryBeforeMediaOpened`;
- `ReplayHistoryEntryCommand_SelectsTrackAndStartsPlayback`;
- `ReplayHistoryEntryCommand_DoesNotAddHistoryBeforeMediaOpened`;
- `MissingFile_FromPlayTrackCommand_DoesNotSetPlayingTrack`;
- `SelectedTrack_Change_DoesNotStopExistingPlayback`;
- `SelectedGenre_Change_DoesNotClearPlayingTrack`;
- `DeleteSelectedTrack_WhenPlayingUserTrack_StopsPlaybackAndClearsPlayingTrack`;
- `PlayingTrack_Change_RaisesCardStateNotifications`.

### UI Smoke

Manual WPF run:

1. Start app.
2. Confirm brand header.
3. Select a track.
4. Play it.
5. Select another track while audio continues.
6. Double-click another library track.
7. Wait for history.
8. Double-click history entry.
9. Switch filters while playing.
10. Try deleted/missing user-track case if easy to simulate.

## Work Diff Design

Add a section to `MusicLibrary/work_diff.md` after implementation:

`## 10. MusicBakh Player Core UX`

The section should say:

- UI now uses `MusicBakh` as a product name;
- the educational identity "музыкальная библиотека" is preserved as descriptor;
- selection and playback are intentionally separate states;
- playing track has stronger visual indication;
- library and history replay gestures use the same validated playback logic;
- queue, seek, volume, repeat, Now Playing and NAudio pulse are future packages.

## Final Design Summary

Package 1 should leave the app feeling like this:

The user opens **MusicBakh**, sees a polished dark music-library interface, can browse tracks without interrupting playback, immediately understands which track is actually playing, can start a track by double-clicking it, can replay from history, and can still use the original educational functions: filter, play, save, import, delete user tracks, and inspect history.

It is not yet the final player. It is the stable product-facing foundation for the next packages.
