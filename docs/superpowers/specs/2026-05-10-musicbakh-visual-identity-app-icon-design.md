# MusicBakh Visual Identity & App Icon Design Spec

## Status

Design approved for specification.

Figma reference:

- File: `MusicBakh Visual Identity & App Icon`
- URL: `https://www.figma.com/design/rEQaDqABGp6PVk9X46MmsE`
- Board node: `2:2`

Important: the Figma board is a mood/reference board only. Its layout is not a source of exact production spacing because the generated board contains overlapping sections. The accepted visual direction is the icon/logo concept itself.

## Goal

Create the first production-ready visual identity package for MusicBakh:

- a recognizable application icon for the window, taskbar, executable, and future installer;
- a small taskbar-safe mark that remains readable down to `16x16`;
- a primary brand mark for the app header, README, release notes, and installer screens;
- a small set of asset and usage rules so later release work does not invent a second identity.

The package must make MusicBakh feel like a named desktop product rather than a default WPF application.

## Non-Goals

This package does not include:

- audio-reactive cover pulsation;
- installer creation;
- README writing;
- GitHub repository cleanup;
- new playback behavior;
- layout redesign of the whole app;
- replacing all in-app icons.

Installer and README will consume the assets created here, but they are separate packages.

## Visual Direction

Direction: **Premium Library + restrained Modern Audio**.

The identity should look like a personal music library with a polished desktop feel, not like a loud neon player. The current dark/gold interface remains the foundation. A small cold audio accent is allowed only where it helps the icon communicate sound/wave energy.

Core traits:

- dark, calm, premium;
- gold as the main brand accent;
- small cyan/steel audio accent;
- no noisy neon glow;
- no tiny text inside small icons;
- no generic default WPF window icon.

## Brand Marks

### Primary Logo

The primary logo is:

`MB + audio wave`

Purpose:

- app header;
- README hero image;
- installer welcome/header art;
- documentation screenshots;
- future splash/branding surfaces if needed.

Requirements:

- `MB` is the primary monogram.
- The monogram uses a serif-like premium shape, visually compatible with the current `HeadingFont` direction.
- The audio wave sits below or beside the monogram and must be simple enough to reproduce in XAML/PNG.
- The logo may include the wordmark `MusicBakh` in large placements, but the compact logo mark must work without the wordmark.
- The logo must not rely on cyan as the main color. Cyan is optional accent only.

### App / Taskbar Icon

The app icon is:

`rounded dark square + inner music disc + gold rim + small audio wave`

Purpose:

- `.exe` icon;
- WPF window icon;
- Windows taskbar icon;
- future installer icon;
- desktop shortcut.

Requirements:

- Outer shape: rounded square, dark graphite background.
- Inner shape: circular disc/record silhouette.
- Accent: gold rim or ring.
- Audio signal: small wave/equalizer line, preferably cyan/steel with restrained glow.
- No letters inside the app icon.
- No readable text requirement at any size.
- The icon must remain identifiable at `16x16`, even if inner details simplify.

## Color Tokens

Use current MusicBakh colors as the source of truth:

| Role | Hex | Usage |
| --- | --- | --- |
| Background | `#16161F` | outer app icon tile, app background |
| Panel | `#2A2A3F` | icon inner contrast, brand board panels |
| Primary gold | `#D4A574` | rim, logo line, main accent |
| Dark gold | `#B8864F` | secondary rim/shadow, borders |
| Text | `#F4ECE3` | wordmark on dark surfaces |
| Muted text | `#A69A8F` | descriptor text |
| Audio accent | `#6BC7D8` | small wave/glow only |

Invariant: cyan must not become the dominant brand color. If the asset reads as blue/cyan first and gold second, it fails.

## Asset Deliverables

Create source assets under:

```text
docs/assets/musicbakh/
```

Required source/export files:

```text
docs/assets/musicbakh/musicbakh-app-icon.svg
docs/assets/musicbakh/musicbakh-primary-logo.svg
docs/assets/musicbakh/musicbakh-app-icon-256.png
docs/assets/musicbakh/musicbakh-primary-logo.png
docs/assets/musicbakh/musicbakh.ico
```

Copy runtime assets into the WPF project under:

```text
MusicLibrary/Assets/Brand/musicbakh.ico
MusicLibrary/Assets/Brand/musicbakh-logo.png
```

The `.ico` must contain these sizes:

```text
256x256
128x128
64x64
48x48
32x32
16x16
```

Preferred generation flow:

1. Build or export the icon from a vector source.
2. Render PNG sizes from the vector.
3. Pack the PNG sizes into a single `.ico`.
4. Verify the `.ico` visually, not only by file existence.

## WPF Integration

Current state:

- `MusicLibrary/MusicLibrary.csproj` has no `ApplicationIcon`.
- `MusicLibrary/MainWindow.xaml` has `Title="MusicBakh"` but no `Icon`.
- The header currently uses an inline XAML `MB` + wave placeholder inside a rounded square.

Required integration:

### Project Icon

Set the executable icon in `MusicLibrary/MusicLibrary.csproj`:

```xml
<ApplicationIcon>Assets\Brand\musicbakh.ico</ApplicationIcon>
```

The icon file must also be available as a WPF resource for the window icon. If SDK-style item globs conflict with explicit resource inclusion, the implementation must use the minimal project-file item configuration that builds without duplicate item errors.

### Window Icon

Set the main window icon in `MusicLibrary/MainWindow.xaml`:

```xml
Icon="Assets/Brand/musicbakh.ico"
```

This follows WPF pack URI behavior for resource files and should make the title bar and taskbar use the MusicBakh icon during normal app launch.

### Header Logo

Replace the current inline header mark with the production logo asset:

```text
MusicLibrary/Assets/Brand/musicbakh-logo.png
```

The header must keep the existing product text:

```text
MusicBakh
Музыкальная библиотека
```

The header mark should remain approximately the same footprint as the current `54x54` mark so the first viewport layout does not shift.

## Implementation Boundaries

Allowed files:

```text
MusicLibrary/MusicLibrary.csproj
MusicLibrary/MainWindow.xaml
MusicLibrary/Assets/Brand/*
docs/assets/musicbakh/*
docs/superpowers/specs/*
docs/superpowers/plans/*
```

Allowed optional helper:

```text
docs/assets/musicbakh/generate_musicbakh_assets.ps1
```

Do not modify:

```text
MusicLibrary/ViewModels/*
MusicLibrary/Services/*
MusicLibrary/Models/*
MusicLibrary/work_diff.md
```

This is a visual asset and project configuration package. No playback/data logic should change.

## Accessibility And Readability

The icon must pass these checks:

- `256x256`: disc, rim, and wave are clearly visible.
- `64x64`: disc silhouette and wave are visible.
- `32x32`: the icon still reads as a music/audio app.
- `16x16`: the icon remains a dark/gold app mark; tiny wave may simplify, but the icon must not become a muddy square.

The header logo must:

- not blur noticeably at app scale;
- not introduce text smaller than the existing subtitle;
- not reduce contrast against the current dark header;
- not push the `MusicBakh` title or add-track button out of alignment.

## Failure Patterns

| Failure | Why It Fails | Required Handling |
| --- | --- | --- |
| `.ico` contains only `256x256` | Windows taskbar/scaling may downsample poorly | Include all required sizes in the `.ico`. |
| Icon includes `MB` text | Text becomes unreadable at small sizes | App icon uses disc + wave only. |
| Cyan dominates the icon | Breaks current MusicBakh identity | Keep cyan as a small wave/glow accent. |
| Header logo is exported only as huge PNG | WPF may scale it poorly | Export a header-sized PNG and verify at runtime. |
| `ApplicationIcon` set but `Window.Icon` omitted | Executable may look right while running window/taskbar still looks default in some contexts | Set both project icon and window icon. |
| Explicit resource inclusion causes duplicate item build error | SDK-style projects include some items automatically | Adjust item metadata/removal in `.csproj` instead of forcing duplicate includes. |
| Asset paths contain spaces or Cyrillic | More fragile in project files and installer scripts | Use ASCII paths and filenames. |
| Figma layout is treated as exact implementation | Generated board has overlaps | Use Figma as visual reference only. |

## Required Comments

Some implementation comments are mandatory because asset/project icon behavior is otherwise opaque:

- In `MusicLibrary.csproj`, add a short XML comment above `ApplicationIcon` explaining that it controls the executable/taskbar icon.
- In `MainWindow.xaml`, add a short XML comment near the header logo only if the XAML structure is not self-evident after replacing the placeholder.
- If a helper script is added for asset generation, comment the step that packs multiple PNG sizes into one `.ico`.

Do not add comments explaining obvious XAML properties such as `Width`, `Height`, or `Margin`.

## Verification

Automated verification:

```powershell
dotnet build .\MusicLibrary.sln --no-restore
dotnet test .\MusicLibrary.sln --no-restore
dotnet build .\MusicLibrary.sln -c Release --no-restore
```

Expected:

```text
Сборка успешно завершена.
    Предупреждений: 0
    Ошибок: 0

Пройден!   : не пройдено     0
```

Startup smoke:

```powershell
$exe = Join-Path (Get-Location) 'MusicLibrary\bin\Release\net10.0-windows\MusicLibrary.exe'
$p = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 5
$p.Refresh()
$title = $p.MainWindowTitle
$exited = $p.HasExited
if (-not $exited) {
    $closed = $p.CloseMainWindow()
    Start-Sleep -Seconds 2
    $p.Refresh()
    if (-not $p.HasExited) {
        Stop-Process -Id $p.Id -Force
    }
}
[pscustomobject]@{
    ExitedBeforeClose = $exited
    MainWindowTitle = $title
    CloseRequested = if ($exited) { $false } else { $closed }
} | Format-List
```

Expected:

```text
ExitedBeforeClose : False
MainWindowTitle   : MusicBakh
CloseRequested    : True
```

Manual visual verification:

```text
1. Title bar icon is MusicBakh icon, not default WPF icon.
2. Taskbar icon is MusicBakh icon while app is running.
3. Release exe displays MusicBakh icon in File Explorer.
4. Header logo appears aligned with the MusicBakh title.
5. Header layout remains stable at 1180x760 and at minimum window size.
6. 16x16/32x32 icon previews are not muddy or text-dependent.
7. Icon reads as dark/gold premium audio, with cyan only as a small signal accent.
```

## Acceptance Criteria

The package is complete when:

- `musicbakh.ico` exists in docs assets and runtime assets.
- `musicbakh.ico` contains `256/128/64/48/32/16`.
- `musicbakh-logo.png` exists in runtime assets.
- WPF executable uses `musicbakh.ico` through `ApplicationIcon`.
- `MainWindow` uses `Icon="Assets/Brand/musicbakh.ico"`.
- Header uses the production logo asset instead of the temporary inline placeholder.
- Build/test/release build pass.
- Runtime smoke confirms the app still launches as `MusicBakh`.
- Manual visual check confirms title bar/taskbar/exe/header icon surfaces.

## Work Diff Note

After implementation, add a short entry to `MusicLibrary/work_diff.md` only if the release documentation flow still expects it. The entry should say that the project now has a production MusicBakh visual identity and app icon, while the written coursework may not mention these branding details.

## Open Decisions

No open design decisions remain.

Implementation may choose the exact asset generation tool, but it must produce the required files and pass the verification above.
