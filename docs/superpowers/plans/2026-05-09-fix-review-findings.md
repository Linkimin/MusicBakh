# Fix Review Findings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the code review findings in the WPF music library while keeping the approved lightweight MVVM architecture and leaving the `DispatcherTimer`/`CommandManager` tradeoff unchanged.

**Architecture:** The app keeps `InMemoryTrackRepository` as the programmatic metadata source, but it now points to real local MP3 files copied into the project. Playback history is moved from optimistic `Play()` success to confirmed `MediaOpened`, and visual templates are updated so real cover images and selected-state styling match the Figma design better.

**Tech Stack:** C# 14, .NET 10 Windows, WPF/XAML, `System.Windows.Media.MediaPlayer`, `ObservableCollection<T>`, xUnit, PowerShell for asset copy and generated PNG covers.

---

## File Map

```text
E:\MusicLibraryRelize\MusicLibrary\Services\Tracks\InMemoryTrackRepository.cs
E:\MusicLibraryRelize\MusicLibrary\ViewModels\MainViewModel.cs
E:\MusicLibraryRelize\MusicLibrary\Resources\TrackTemplates.xaml
E:\MusicLibraryRelize\MusicLibrary\Resources\ListStyles.xaml
E:\MusicLibraryRelize\MusicLibrary\MainWindow.xaml
E:\MusicLibraryRelize\MusicLibrary\Music\*.mp3
E:\MusicLibraryRelize\MusicLibrary\Covers\*.png
E:\MusicLibraryRelize\MusicLibrary.Tests\MainViewModelTests.cs
E:\MusicLibraryRelize\MusicLibrary.Tests\RepositoryAssetTests.cs
```

Responsibilities:

- `InMemoryTrackRepository.cs`: real track metadata, local MP3 filenames, factual durations, cover paths.
- `MainViewModel.cs`: pending-history state so history is added only after `MediaOpened`.
- `TrackTemplates.xaml`: real cover images in track cards and history.
- `MainWindow.xaml`: real cover image in central player.
- `ListStyles.xaml`: selected-state visual no longer hidden by the card template.
- `RepositoryAssetTests.cs`: guards against missing music/cover assets and bad durations.

---

### Task 1: Copy Real MP3 Assets Into the Project

**Files:**
- Create: `E:\MusicLibraryRelize\MusicLibrary\Music\*.mp3`
- Delete: `E:\MusicLibraryRelize\MusicLibrary\Music\*.wav`

- [ ] **Step 1: Copy all MP3 files**

Run:

```powershell
$source = 'E:\Music'
$target = 'E:\MusicLibraryRelize\MusicLibrary\Music'
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -LiteralPath (Get-ChildItem -LiteralPath $source -File -Filter *.mp3).FullName -Destination $target -Force
```

Expected: `E:\MusicLibraryRelize\MusicLibrary\Music` contains 16 `.mp3` files.

- [ ] **Step 2: Remove generated WAV demo tones**

Run:

```powershell
Remove-Item -LiteralPath 'E:\MusicLibraryRelize\MusicLibrary\Music\blue-in-green.wav' -ErrorAction SilentlyContinue
Remove-Item -LiteralPath 'E:\MusicLibraryRelize\MusicLibrary\Music\bohemian-rhapsody.wav' -ErrorAction SilentlyContinue
Remove-Item -LiteralPath 'E:\MusicLibraryRelize\MusicLibrary\Music\clair-de-lune.wav' -ErrorAction SilentlyContinue
Remove-Item -LiteralPath 'E:\MusicLibraryRelize\MusicLibrary\Music\moonlight-sonata.wav' -ErrorAction SilentlyContinue
Remove-Item -LiteralPath 'E:\MusicLibraryRelize\MusicLibrary\Music\stairway-to-heaven.wav' -ErrorAction SilentlyContinue
Remove-Item -LiteralPath 'E:\MusicLibraryRelize\MusicLibrary\Music\take-five.wav' -ErrorAction SilentlyContinue
```

Expected: no `.wav` files remain in `MusicLibrary\Music`.

- [ ] **Step 3: Verify asset count**

Run:

```powershell
Get-ChildItem -LiteralPath 'E:\MusicLibraryRelize\MusicLibrary\Music' -File -Filter *.mp3 | Measure-Object
Get-ChildItem -LiteralPath 'E:\MusicLibraryRelize\MusicLibrary\Music' -File -Filter *.wav | Measure-Object
```

Expected: first count is `16`; second count is `0`.

- [ ] **Step 4: Commit**

```powershell
git add MusicLibrary\Music
git commit -m "chore: replace demo tones with real local tracks"
```

---

### Task 2: Update Repository Metadata to Match Real Tracks

**Files:**
- Modify: `E:\MusicLibraryRelize\MusicLibrary\Services\Tracks\InMemoryTrackRepository.cs`
- Test: `E:\MusicLibraryRelize\MusicLibrary.Tests\RepositoryAssetTests.cs`

- [ ] **Step 1: Add repository asset tests first**

Create `E:\MusicLibraryRelize\MusicLibrary.Tests\RepositoryAssetTests.cs`:

```csharp
using MusicLibrary.Services.Tracks;

namespace MusicLibrary.Tests;

public sealed class RepositoryAssetTests
{
    [Fact]
    public void Repository_ContainsSixteenRealTracks()
    {
        var repository = new InMemoryTrackRepository();

        var tracks = repository.GetTracks();

        Assert.Equal(16, tracks.Count);
        Assert.All(tracks, track => Assert.EndsWith(".mp3", track.FilePath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Repository_TrackFilesExist()
    {
        var repository = new InMemoryTrackRepository();

        var missingFiles = repository.GetTracks()
            .Where(track => !File.Exists(track.FilePath))
            .Select(track => track.FilePath)
            .ToList();

        Assert.Empty(missingFiles);
    }

    [Fact]
    public void Repository_DurationsAreFactualAndNonZero()
    {
        var repository = new InMemoryTrackRepository();

        var tracks = repository.GetTracks();

        Assert.All(tracks, track => Assert.True(track.Duration.TotalSeconds > 60, $"{track.Title} has suspicious duration."));
        Assert.Contains(tracks, track => track.Title == "Я свободен" && track.Duration == TimeSpan.FromSeconds(204));
        Assert.Contains(tracks, track => track.Title == "Satisfaction" && track.Duration == TimeSpan.FromSeconds(285));
        Assert.Contains(tracks, track => track.Title == "KILLA!" && track.Duration == TimeSpan.FromSeconds(106));
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test MusicLibrary.Tests\MusicLibrary.Tests.csproj --filter RepositoryAssetTests
```

Expected: fails because repository still contains 6 WAV demo entries.

- [ ] **Step 3: Replace repository contents**

Replace `InMemoryTrackRepository.cs` with:

```csharp
using MusicLibrary.Models;
using System.IO;

namespace MusicLibrary.Services.Tracks;

/// <summary>
/// Учебный источник данных: список треков задается программно, как описано в работе.
/// Файлы при этом лежат в локальной папке приложения, поэтому проект можно запускать без внешних путей.
/// </summary>
public sealed class InMemoryTrackRepository : ITrackRepository
{
    public IReadOnlyList<Track> GetTracks()
    {
        string musicFolder = Path.Combine(AppContext.BaseDirectory, "Music");
        string coversFolder = Path.Combine(AppContext.BaseDirectory, "Covers");

        return new List<Track>
        {
            Create(1, "Satisfaction", "Benny Benassi", "Электроника", 285, "Benny Benassi - Satisfaction.mp3", "satisfaction.png"),
            Create(2, "In My Mind", "Dynoro, Gigi D'Agostino", "Электроника", 183, "Dynoro, Gigi D'Agostino - In My Mind.mp3", "in-my-mind.png"),
            Create(3, "Антидепрессант", "FIZICA", "Инди", 234, "FIZICA - Антидепрессант.mp3", "antidepressant.png"),
            Create(4, "Я свободен", "Кипелов", "Рок", 204, "Кипелов - Я свободен.mp3", "ya-svoboden.png"),
            Create(5, "Судно (Борис Рыжий)", "Molchat Doma", "Постпанк", 141, "Molchat Doma - Судно (борис рижий).mp3", "sudno.png"),
            Create(6, "Hayloft II", "Mother Mother", "Инди", 215, "Mother Mother - Hayloft II.mp3", "hayloft-ii.png"),
            Create(7, "Hysteria", "Muse", "Рок", 227, "Muse - Hysteria.mp3", "hysteria.png"),
            Create(8, "VORACITY", "MYTH ROID", "Аниме/OST", 230, "MYTH ROID - VORACITY (ПовелительВладыка ТВ-3Overlord TV-3 OP).mp3", "voracity.png"),
            Create(9, "Gods", "Onsa Media", "Аниме/OST", 222, "Onsa Media - Gods.mp3", "gods.png"),
            Create(10, "Soap Lagoon (Russian ver.)", "Onsa Media", "Аниме/OST", 223, "Onsa Media - Soap Lagoon (Russian ver.).mp3", "soap-lagoon.png"),
            Create(11, "Meds", "Placebo feat. Alison Mosshart", "Рок", 175, "Placebo feat. Alison Mosshart - Meds.mp3", "meds.png"),
            Create(12, "We Drink Your Blood", "Powerwolf", "Метал", 222, "Powerwolf - We Drink Your Blood.mp3", "we-drink-your-blood.png"),
            Create(13, "HOLLOW HUNGER (Raon cover)", "Raon", "Аниме/OST", 220, "Raon - HOLLOW HUNGER _ Overlord IV OP┃Raon cover.mp3", "hollow-hunger.png"),
            Create(14, "KILLA!", "SHADXWBXRN", "Фонк", 106, "SHADXWBXRN - KILLA!.mp3", "killa.png"),
            Create(15, "KNIGHT", "SHADXWBXRN", "Фонк", 122, "SHADXWBXRN - KNIGHT.mp3", "knight.png"),
            Create(16, "Seven Nation Army", "The White Stripes", "Рок", 231, "The White Stripes - Seven Nation Army.mp3", "seven-nation-army.png")
        };

        Track Create(int id, string title, string artist, string genre, int durationSeconds, string fileName, string coverName)
        {
            return new Track
            {
                Id = id,
                Title = title,
                Artist = artist,
                Genre = genre,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                FilePath = Path.Combine(musicFolder, fileName),
                CoverPath = Path.Combine(coversFolder, coverName)
            };
        }
    }
}
```

- [ ] **Step 4: Run tests**

Run:

```powershell
dotnet test MusicLibrary.Tests\MusicLibrary.Tests.csproj --filter RepositoryAssetTests
```

Expected: repository count/file/duration tests pass except cover existence, which is added in the next task.

- [ ] **Step 5: Commit**

```powershell
git add MusicLibrary\Services\Tracks\InMemoryTrackRepository.cs MusicLibrary.Tests\RepositoryAssetTests.cs
git commit -m "feat: use real track metadata"
```

---

### Task 3: Generate Local PNG Covers and Bind Them in XAML

**Files:**
- Create: `E:\MusicLibraryRelize\MusicLibrary\Covers\*.png`
- Modify: `E:\MusicLibraryRelize\MusicLibrary\Resources\TrackTemplates.xaml`
- Modify: `E:\MusicLibraryRelize\MusicLibrary\MainWindow.xaml`
- Modify: `E:\MusicLibraryRelize\MusicLibrary.Tests\RepositoryAssetTests.cs`

- [ ] **Step 1: Generate one cover per track**

Run this PowerShell script:

```powershell
Add-Type -AssemblyName System.Drawing

$covers = @(
  @{ File='satisfaction.png'; Title='SAT'; Accent=[System.Drawing.Color]::FromArgb(212,165,116) },
  @{ File='in-my-mind.png'; Title='IMM'; Accent=[System.Drawing.Color]::FromArgb(116,177,212) },
  @{ File='antidepressant.png'; Title='FIZ'; Accent=[System.Drawing.Color]::FromArgb(172,116,212) },
  @{ File='ya-svoboden.png'; Title='ЯС'; Accent=[System.Drawing.Color]::FromArgb(212,116,116) },
  @{ File='sudno.png'; Title='СД'; Accent=[System.Drawing.Color]::FromArgb(116,143,212) },
  @{ File='hayloft-ii.png'; Title='H2'; Accent=[System.Drawing.Color]::FromArgb(212,116,175) },
  @{ File='hysteria.png'; Title='HYS'; Accent=[System.Drawing.Color]::FromArgb(212,132,116) },
  @{ File='voracity.png'; Title='VOR'; Accent=[System.Drawing.Color]::FromArgb(116,212,183) },
  @{ File='gods.png'; Title='GOD'; Accent=[System.Drawing.Color]::FromArgb(212,196,116) },
  @{ File='soap-lagoon.png'; Title='SL'; Accent=[System.Drawing.Color]::FromArgb(116,212,209) },
  @{ File='meds.png'; Title='MED'; Accent=[System.Drawing.Color]::FromArgb(155,116,212) },
  @{ File='we-drink-your-blood.png'; Title='PWR'; Accent=[System.Drawing.Color]::FromArgb(212,86,86) },
  @{ File='hollow-hunger.png'; Title='HH'; Accent=[System.Drawing.Color]::FromArgb(128,212,116) },
  @{ File='killa.png'; Title='K!'; Accent=[System.Drawing.Color]::FromArgb(212,116,206) },
  @{ File='knight.png'; Title='KNT'; Accent=[System.Drawing.Color]::FromArgb(151,151,151) },
  @{ File='seven-nation-army.png'; Title='7NA'; Accent=[System.Drawing.Color]::FromArgb(212,165,116) }
)

$target = 'E:\MusicLibraryRelize\MusicLibrary\Covers'
New-Item -ItemType Directory -Force -Path $target | Out-Null

foreach ($cover in $covers) {
    $bitmap = [System.Drawing.Bitmap]::new(400, 400)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $rect = [System.Drawing.Rectangle]::new(0, 0, 400, 400)
    $bg = [System.Drawing.Drawing2D.LinearGradientBrush]::new($rect, [System.Drawing.Color]::FromArgb(10,10,15), [System.Drawing.Color]::FromArgb(42,42,63), 45)
    $graphics.FillRectangle($bg, $rect)
    $accentBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(180, $cover.Accent))
    $graphics.FillEllipse($accentBrush, 245, -60, 230, 230)
    $graphics.FillEllipse($accentBrush, -80, 260, 210, 210)
    $font = [System.Drawing.Font]::new('Segoe UI', 58, [System.Drawing.FontStyle]::Bold)
    $textBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(232,232,240))
    $format = [System.Drawing.StringFormat]::new()
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $graphics.DrawString($cover.Title, $font, $textBrush, $rect, $format)
    $graphics.Dispose()
    $bitmap.Save((Join-Path $target $cover.File), [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}
```

Expected: `MusicLibrary\Covers` contains 16 `.png` files.

- [ ] **Step 2: Extend repository asset tests for covers**

Modify `RepositoryAssetTests.cs` by adding:

```csharp
[Fact]
public void Repository_CoverFilesExist()
{
    var repository = new InMemoryTrackRepository();

    var missingCovers = repository.GetTracks()
        .Where(track => !File.Exists(track.CoverPath))
        .Select(track => track.CoverPath)
        .ToList();

    Assert.Empty(missingCovers);
}
```

- [ ] **Step 3: Update `TrackTemplates.xaml` to use images and expose selected state**

Replace `TrackTemplates.xaml` with:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <DataTemplate x:Key="TrackCardTemplate">
        <Border x:Name="CardRoot" Padding="14" CornerRadius="12" Background="{StaticResource AccentBrush}" BorderBrush="{StaticResource GoldBorderBrush}" BorderThickness="1">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="64" />
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="14" />
                </Grid.ColumnDefinitions>
                <Border Width="64" Height="64" CornerRadius="10" Background="{StaticResource SecondaryBrush}" ClipToBounds="True">
                    <Image Source="{Binding CoverPath}" Stretch="UniformToFill" />
                </Border>
                <StackPanel Grid.Column="1" Margin="14,0,0,0" VerticalAlignment="Center">
                    <TextBlock Text="{Binding Title}" Foreground="{StaticResource ForegroundBrush}" FontWeight="SemiBold" FontSize="16" TextTrimming="CharacterEllipsis" />
                    <TextBlock Text="{Binding Artist}" Foreground="{StaticResource MutedForegroundBrush}" FontSize="13" TextTrimming="CharacterEllipsis" Margin="0,3,0,8" />
                    <StackPanel Orientation="Horizontal">
                        <Border Background="#1AD4A574" BorderBrush="{StaticResource GoldBorderBrush}" BorderThickness="1" CornerRadius="6" Padding="8,3">
                            <TextBlock Text="{Binding Genre}" Foreground="{StaticResource PrimaryBrush}" FontSize="12" />
                        </Border>
                        <TextBlock Text="{Binding DurationText}" Foreground="{StaticResource MutedForegroundBrush}" FontSize="12" Margin="10,3,0,0" />
                    </StackPanel>
                </StackPanel>
                <Ellipse x:Name="SelectedDot" Grid.Column="2" Width="8" Height="8" Fill="{StaticResource PrimaryBrush}" Visibility="Collapsed" VerticalAlignment="Top" HorizontalAlignment="Right" />
            </Grid>
        </Border>
        <DataTemplate.Triggers>
            <DataTrigger Binding="{Binding RelativeSource={RelativeSource AncestorType=ListBoxItem}, Path=IsSelected}" Value="True">
                <Setter TargetName="CardRoot" Property="Background" Value="{StaticResource SelectedTrackBrush}" />
                <Setter TargetName="CardRoot" Property="BorderBrush" Value="{StaticResource PrimaryBrush}" />
                <Setter TargetName="CardRoot" Property="BorderThickness" Value="2" />
                <Setter TargetName="SelectedDot" Property="Visibility" Value="Visible" />
            </DataTrigger>
        </DataTemplate.Triggers>
    </DataTemplate>

    <DataTemplate x:Key="HistoryItemTemplate">
        <Border Padding="12" CornerRadius="10" Background="#332A2A3F" BorderBrush="{StaticResource GoldBorderBrush}" BorderThickness="1" Margin="0,0,0,10">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="48" />
                    <ColumnDefinition Width="*" />
                </Grid.ColumnDefinitions>
                <Border Width="48" Height="48" CornerRadius="8" ClipToBounds="True" Background="{StaticResource SecondaryBrush}">
                    <Image Source="{Binding Track.CoverPath}" Stretch="UniformToFill" />
                </Border>
                <StackPanel Grid.Column="1" Margin="10,0,0,0">
                    <TextBlock Text="{Binding Track.Title}" Foreground="{StaticResource ForegroundBrush}" FontWeight="SemiBold" TextTrimming="CharacterEllipsis" />
                    <TextBlock Text="{Binding Track.Artist}" Foreground="{StaticResource MutedForegroundBrush}" FontSize="12" TextTrimming="CharacterEllipsis" />
                    <TextBlock Text="{Binding PlayedAt, StringFormat={}{0:HH:mm:ss}}" Foreground="{StaticResource PrimaryBrush}" FontSize="12" Margin="0,4,0,0" />
                </StackPanel>
            </Grid>
        </Border>
    </DataTemplate>
</ResourceDictionary>
```

- [ ] **Step 4: Update central player cover in `MainWindow.xaml`**

Replace the current central cover `TextBlock` inside the 196x196 `Border` with:

```xml
<Image Source="{Binding SelectedTrack.CoverPath}" Stretch="UniformToFill" />
```

Keep the existing `Border` shell, shadow, border thickness, width and height.

- [ ] **Step 5: Run tests**

Run:

```powershell
dotnet test MusicLibrary.sln
```

Expected: all tests pass, including cover existence.

- [ ] **Step 6: Commit**

```powershell
git add MusicLibrary\Covers MusicLibrary\Resources\TrackTemplates.xaml MusicLibrary\MainWindow.xaml MusicLibrary.Tests\RepositoryAssetTests.cs
git commit -m "feat: add local cover art and selected track visuals"
```

---

### Task 4: Move Playback History to Confirmed MediaOpened

**Files:**
- Modify: `E:\MusicLibraryRelize\MusicLibrary\ViewModels\MainViewModel.cs`
- Modify: `E:\MusicLibraryRelize\MusicLibrary.Tests\MainViewModelTests.cs`

- [ ] **Step 1: Add failing tests for confirmed playback history**

Modify `MainViewModelTests.cs`:

Replace `PlayPauseCommand_AddsHistory_WhenPlaybackStarts` with:

```csharp
[Fact]
public void PlayPauseCommand_DoesNotAddHistory_BeforeMediaOpened()
{
    var viewModel = CreateViewModel();
    viewModel.SelectedTrack = viewModel.DisplayedTracks.First();

    viewModel.PlayPauseCommand.Execute(null);

    Assert.Empty(viewModel.PlaybackHistory);
}
```

Add this test:

```csharp
[Fact]
public void MediaOpened_AddsHistoryForPendingTrack()
{
    var (viewModel, player) = CreateViewModelWithPlayer();
    viewModel.SelectedTrack = viewModel.DisplayedTracks.First();

    viewModel.PlayPauseCommand.Execute(null);
    player.RaiseOpenedForTest();

    Assert.Single(viewModel.PlaybackHistory);
    Assert.Equal(viewModel.SelectedTrack, viewModel.PlaybackHistory[0].Track);
}
```

Update `PlayPauseCommand_DoesNotDuplicateHistory_WhenResumingAfterPause`:

```csharp
[Fact]
public void PlayPauseCommand_DoesNotDuplicateHistory_WhenResumingAfterPause()
{
    var (viewModel, player) = CreateViewModelWithPlayer();
    viewModel.SelectedTrack = viewModel.DisplayedTracks.First();

    viewModel.PlayPauseCommand.Execute(null);
    player.RaiseOpenedForTest();
    viewModel.PlayPauseCommand.Execute(null);
    viewModel.PlayPauseCommand.Execute(null);

    Assert.Single(viewModel.PlaybackHistory);
}
```

Update fake player `Open` so it no longer raises `MediaOpened` automatically:

```csharp
public OperationResult Open(string filePath)
{
    return OperationResult.Success("opened");
}

public void RaiseOpenedForTest() => MediaOpened?.Invoke(this, EventArgs.Empty);
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test MusicLibrary.Tests\MusicLibrary.Tests.csproj --filter MainViewModelTests
```

Expected: history tests fail because current ViewModel still adds history immediately after `Play()`.

- [ ] **Step 3: Update `MainViewModel` pending-history logic**

In `MainViewModel.cs`, add a field near `_loadedTrack`:

```csharp
private Track? _pendingHistoryTrack;
```

Change `MediaOpened` subscription from:

```csharp
_audioPlayerService.MediaOpened += (_, _) => RefreshDuration();
```

to:

```csharp
_audioPlayerService.MediaOpened += (_, _) => HandleMediaOpened();
```

In `PlayOrPause`, replace:

```csharp
// История фиксирует новый запуск трека, но не дублирует простое продолжение после паузы.
if (!isResume)
{
    AddToHistory(SelectedTrack);
}
```

with:

```csharp
// Историю добавляем только после MediaOpened, потому что MediaPlayer сообщает ошибки асинхронно.
if (!isResume)
{
    _pendingHistoryTrack = SelectedTrack;
}
```

Add method:

```csharp
private void HandleMediaOpened()
{
    RefreshDuration();

    if (_pendingHistoryTrack is not null)
    {
        AddToHistory(_pendingHistoryTrack);
        _pendingHistoryTrack = null;
    }
}
```

In `Stop`, `HandleMediaEnded`, and `HandleMediaFailed`, set:

```csharp
_pendingHistoryTrack = null;
```

- [ ] **Step 4: Run tests**

Run:

```powershell
dotnet test MusicLibrary.Tests\MusicLibrary.Tests.csproj --filter MainViewModelTests
```

Expected: all ViewModel tests pass.

- [ ] **Step 5: Commit**

```powershell
git add MusicLibrary\ViewModels\MainViewModel.cs MusicLibrary.Tests\MainViewModelTests.cs
git commit -m "fix: add playback history after media opens"
```

---

### Task 5: Verify Design and Runtime Behavior

**Files:**
- Verify: `E:\MusicLibraryRelize\MusicLibrary\MainWindow.xaml`
- Verify: `E:\MusicLibraryRelize\MusicLibrary\Resources\TrackTemplates.xaml`
- Verify: `E:\MusicLibraryRelize\MusicLibrary\MusicLibrary.csproj`

- [ ] **Step 1: Run full tests**

Run:

```powershell
dotnet test MusicLibrary.sln
```

Expected: all tests pass.

- [ ] **Step 2: Run Release build**

Run:

```powershell
dotnet build MusicLibrary.sln -c Release
```

Expected: build succeeds with 0 warnings and 0 errors.

- [ ] **Step 3: Verify copied output assets**

Run:

```powershell
Get-ChildItem -LiteralPath 'E:\MusicLibraryRelize\MusicLibrary\bin\Debug\net10.0-windows\Music' -File -Filter *.mp3 | Measure-Object
Get-ChildItem -LiteralPath 'E:\MusicLibraryRelize\MusicLibrary\bin\Debug\net10.0-windows\Covers' -File -Filter *.png | Measure-Object
```

Expected: both counts are `16`.

- [ ] **Step 4: Manual UI smoke test**

Run:

```powershell
dotnet run --project MusicLibrary\MusicLibrary.csproj
```

Verify manually:

- all 16 tracks appear in the left panel;
- each track has a cover image, not the `♪` symbol;
- selecting a track visibly changes border/background and shows a dot;
- central panel shows the selected cover;
- displayed duration matches repository duration;
- playback starts for copied MP3 files;
- history appears after playback opens;
- save dialog can copy the selected MP3.

- [ ] **Step 5: Commit any final polish**

If manual smoke test required changes:

```powershell
git add MusicLibrary MusicLibrary.Tests
git commit -m "fix: polish review finding corrections"
```

If no changes:

```powershell
git status --short
```

Expected: clean or only untracked ignored `bin/obj` files.

---

## Self-Review Checklist

- Fake WAV tones are removed.
- All 16 MP3 files from `E:\Music` are copied into `MusicLibrary\Music`.
- Repository metadata uses local MP3 paths only.
- Durations match the values measured from Windows Shell.
- Covers are local PNG files and are referenced by repository entries.
- Track list, history and central player show cover images.
- Selected-state is visible and not hidden by an inner opaque card.
- Playback history is added after `MediaOpened`, not immediately after `Play()`.
- `MediaFailed` clears pending history.
- Tests cover repository assets, durations, missing files, media failure, and history timing.
