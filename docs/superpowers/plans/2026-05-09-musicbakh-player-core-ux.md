# MusicBakh Player Core UX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first MusicBakh UX package: light rebrand, visible currently playing track in the library, double-click playback from library/history, and a tighter player shell without queue or audio-reactive pulse.

**Architecture:** Keep the existing MVVM shape: `MainViewModel` owns playback intent and state, XAML only binds commands and visual states, and a tiny converter compares the current template item with `PlayingTrack`. Double-click paths reuse the same ViewModel start method as the main play button, so history, library cards, and button playback cannot drift into different behavior.

**Tech Stack:** .NET 10, C# 14, WPF, XAML `InputBindings`, XAML `MultiBinding`, xUnit, existing `RelayCommand`, existing `IAudioPlayerService`.

---

## Context

Primary specs:

- `docs/superpowers/specs/2026-05-09-musicbakh-player-core-ux-design.md`
- `docs/superpowers/specs/2026-05-09-musicbakh-player-core-ux-full-design.md`

Current documentation check:

- Context7 library: `/websites/learn_microsoft_en-us_dotnet_desktop`
- Relevant WPF patterns confirmed: `DataTemplate.Triggers` with `DataTrigger`, `Window.InputBindings`/command binding model, and object element syntax for `MultiBinding`.

This package intentionally excludes:

- Active seekable progress bar.
- Automatic next track.
- Taskbar/application icon.
- NAudio frequency pulse.
- Queue tab and queue logic.
- Import wizard redesign.

## File Structure

- Modify `MusicLibrary/ViewModels/MainViewModel.cs`: add library/history replay commands and centralize playback start logic.
- Create `MusicLibrary/Converters/TrackIdentityMatchConverter.cs`: compare a track row with `MainViewModel.PlayingTrack` by `Track.Id`.
- Modify `MusicLibrary/App.xaml`: register `TrackIdentityMatchConverter`.
- Modify `MusicLibrary/Resources/TrackTemplates.xaml`: add double-click bindings, playing-state visuals, and clickable history item behavior.
- Modify `MusicLibrary/MainWindow.xaml`: rebrand shell to MusicBakh and compact the center player panel.
- Modify `MusicLibrary.Tests/MainViewModelTests.cs`: cover command behavior for library double-click and history replay.
- Create `MusicLibrary.Tests/TrackIdentityMatchConverterTests.cs`: cover converter true/false/null behavior.
- Modify `MusicLibrary/work_diff.md`: add section 10 documenting the first MusicBakh UX package difference from the written work.

## Task 1: Add ViewModel Command Tests

**Files:**

- Modify: `MusicLibrary.Tests/MainViewModelTests.cs`

- [ ] **Step 1: Add failing tests for direct track start and history replay**

Add these tests after `PlayPauseCommand_ChangingTrack_StopsPreviousAndStartsNew`:

```csharp
[Fact]
public void PlayTrackCommand_SelectsTrackAndStartsPlayback()
{
    var (viewModel, player) = CreateViewModelWithPlayer();
    Track second = viewModel.DisplayedTracks[1];

    viewModel.PlayTrackCommand.Execute(second);

    Assert.Equal(second, viewModel.SelectedTrack);
    Assert.Equal(second, viewModel.PlayingTrack);
    Assert.True(viewModel.IsPlaying);
    Assert.Equal(second.FilePath, player.LastOpenedFilePath);
}

[Fact]
public void PlayTrackCommand_IgnoresNonTrackParameter()
{
    var (viewModel, player) = CreateViewModelWithPlayer();

    viewModel.PlayTrackCommand.Execute("not a track");

    Assert.Null(viewModel.SelectedTrack);
    Assert.Null(viewModel.PlayingTrack);
    Assert.False(viewModel.IsPlaying);
    Assert.Null(player.LastOpenedFilePath);
}

[Fact]
public void ReplayHistoryEntryCommand_SelectsEntryTrackAndStartsPlayback()
{
    var (viewModel, player) = CreateViewModelWithPlayer();
    Track second = viewModel.DisplayedTracks[1];
    var entry = new PlaybackEntry { Track = second, PlayedAt = DateTime.Now };
    viewModel.PlaybackHistory.Add(entry);

    viewModel.ReplayHistoryEntryCommand.Execute(entry);

    Assert.Equal(second, viewModel.SelectedTrack);
    Assert.Equal(second, viewModel.PlayingTrack);
    Assert.True(viewModel.IsPlaying);
    Assert.Equal(second.FilePath, player.LastOpenedFilePath);
}
```

- [ ] **Step 2: Run tests and verify they fail for missing commands**

Run:

```powershell
dotnet test .\MusicLibrary.Tests\MusicLibrary.Tests.csproj --filter "PlayTrackCommand_SelectsTrackAndStartsPlayback|PlayTrackCommand_IgnoresNonTrackParameter|ReplayHistoryEntryCommand_SelectsEntryTrackAndStartsPlayback"
```

Expected:

```text
Failed!  - Failed: 3
CS1061: 'MainViewModel' does not contain a definition for 'PlayTrackCommand'
CS1061: 'MainViewModel' does not contain a definition for 'ReplayHistoryEntryCommand'
```

If the compiler reports only the first missing property, treat that as the expected red state.

## Task 2: Implement Playback Entry Points

**Files:**

- Modify: `MusicLibrary/ViewModels/MainViewModel.cs`
- Test: `MusicLibrary.Tests/MainViewModelTests.cs`

- [ ] **Step 1: Add command properties**

In `MainViewModel`, add these properties after `DeleteTrackCommand`:

```csharp
public ICommand PlayTrackCommand { get; }
public ICommand ReplayHistoryEntryCommand { get; }
```

- [ ] **Step 2: Initialize the commands in the constructor**

In the constructor, after `DeleteTrackCommand = ...`, add:

```csharp
PlayTrackCommand = new RelayCommand(
    parameter => PlaySpecificTrack(parameter as Track),
    parameter => parameter is Track);

ReplayHistoryEntryCommand = new RelayCommand(
    parameter => ReplayHistoryEntry(parameter as PlaybackEntry),
    parameter => parameter is PlaybackEntry);
```

- [ ] **Step 3: Replace `PlayOrPause` and add shared start helpers**

Replace the full existing `PlayOrPause` method with this code, then add the two helper methods directly under it:

```csharp
private void PlayOrPause()
{
    if (SelectedTrack is null)
    {
        SetStatus(OperationResult.Error("Выберите трек."));
        return;
    }

    if (IsSelectedPlaying && IsPlaying)
    {
        _audioPlayerService.Pause();
        _progressTimer.Stop();
        IsPlaying = false;
        _isPaused = true;
        SetStatus(OperationResult.Info("Воспроизведение приостановлено."));
        return;
    }

    StartOrResumeTrack(SelectedTrack);
}

private void PlaySpecificTrack(Track? track)
{
    if (track is null)
    {
        return;
    }

    SelectedTrack = track;
    StartOrResumeTrack(track);
}

private void ReplayHistoryEntry(PlaybackEntry? entry)
{
    if (entry?.Track is null)
    {
        return;
    }

    PlaySpecificTrack(entry.Track);
}

private void StartOrResumeTrack(Track track)
{
    // Every playback entry point goes through this method so the Play button,
    // library double-click, and history replay keep identical pause/history rules.
    if (!_fileService.Exists(track.FilePath))
    {
        SetStatus(OperationResult.Error($"Файл не найден: {track.FilePath}"));
        return;
    }

    bool isResume = _isPaused && PlayingTrack is not null && PlayingTrack.Id == track.Id;

    if (!isResume)
    {
        if (PlayingTrack is not null)
        {
            ResetPlaybackState();
        }

        PlayingTrack = track;
        _pendingHistoryTrack = track;

        OperationResult openResult = _audioPlayerService.Open(track.FilePath);
        if (!openResult.IsSuccess)
        {
            ResetPlaybackState();
            SetStatus(openResult);
            return;
        }
    }

    OperationResult playResult = _audioPlayerService.Play();
    SetStatus(playResult);

    if (playResult.IsSuccess)
    {
        IsPlaying = true;
        _isPaused = false;
        _progressTimer.Start();
        return;
    }

    if (!isResume)
    {
        ResetPlaybackState();
    }
}
```

- [ ] **Step 4: Run command tests and verify they pass**

Run:

```powershell
dotnet test .\MusicLibrary.Tests\MusicLibrary.Tests.csproj --filter "PlayTrackCommand_SelectsTrackAndStartsPlayback|PlayTrackCommand_IgnoresNonTrackParameter|ReplayHistoryEntryCommand_SelectsEntryTrackAndStartsPlayback"
```

Expected:

```text
Passed!  - Failed: 0, Passed: 3
```

- [ ] **Step 5: Run existing playback regression tests**

Run:

```powershell
dotnet test .\MusicLibrary.Tests\MusicLibrary.Tests.csproj --filter "MediaOpened|MediaFailed|StopCommand|ChangingSelectedTrack|ChangingGenre|PlayPauseCommand"
```

Expected:

```text
Passed!  - Failed: 0
```

- [ ] **Step 6: Commit ViewModel command behavior**

Run:

```powershell
git add .\MusicLibrary\ViewModels\MainViewModel.cs .\MusicLibrary.Tests\MainViewModelTests.cs
git commit -m "feat: add MusicBakh playback entry commands"
```

Expected:

```text
[codex/wpf-music-library ...] feat: add MusicBakh playback entry commands
```

## Task 3: Add Playing-Track Identity Converter

**Files:**

- Create: `MusicLibrary/Converters/TrackIdentityMatchConverter.cs`
- Create: `MusicLibrary.Tests/TrackIdentityMatchConverterTests.cs`
- Modify: `MusicLibrary/App.xaml`

- [ ] **Step 1: Write failing converter tests**

Create `MusicLibrary.Tests/TrackIdentityMatchConverterTests.cs`:

```csharp
using MusicLibrary.Converters;
using MusicLibrary.Models;
using System.Globalization;

namespace MusicLibrary.Tests;

public sealed class TrackIdentityMatchConverterTests
{
    [Fact]
    public void Convert_ReturnsTrue_WhenTrackIdsMatch()
    {
        var converter = new TrackIdentityMatchConverter();
        var current = new Track { Id = 7, Title = "A", Artist = "B", Genre = "Рок", FilePath = "a.mp3" };
        var playing = new Track { Id = 7, Title = "Copy", Artist = "B", Genre = "Рок", FilePath = "copy.mp3" };

        object result = converter.Convert(
            new object[] { current, playing },
            typeof(bool),
            parameter: null,
            CultureInfo.InvariantCulture);

        Assert.True((bool)result);
    }

    [Fact]
    public void Convert_ReturnsFalse_WhenTrackIdsDiffer()
    {
        var converter = new TrackIdentityMatchConverter();
        var current = new Track { Id = 7, Title = "A", Artist = "B", Genre = "Рок", FilePath = "a.mp3" };
        var playing = new Track { Id = 8, Title = "C", Artist = "D", Genre = "Джаз", FilePath = "c.mp3" };

        object result = converter.Convert(
            new object[] { current, playing },
            typeof(bool),
            parameter: null,
            CultureInfo.InvariantCulture);

        Assert.False((bool)result);
    }

    [Fact]
    public void Convert_ReturnsFalse_WhenAnyValueIsMissing()
    {
        var converter = new TrackIdentityMatchConverter();
        var current = new Track { Id = 7, Title = "A", Artist = "B", Genre = "Рок", FilePath = "a.mp3" };

        object result = converter.Convert(
            new object?[] { current, null! },
            typeof(bool),
            parameter: null,
            CultureInfo.InvariantCulture);

        Assert.False((bool)result);
    }
}
```

- [ ] **Step 2: Run converter tests and verify they fail**

Run:

```powershell
dotnet test .\MusicLibrary.Tests\MusicLibrary.Tests.csproj --filter TrackIdentityMatchConverterTests
```

Expected:

```text
Failed!  - Failed: 3
CS0246: The type or namespace name 'TrackIdentityMatchConverter' could not be found
```

- [ ] **Step 3: Implement the converter**

Create `MusicLibrary/Converters/TrackIdentityMatchConverter.cs`:

```csharp
using MusicLibrary.Models;
using System.Globalization;
using System.Windows.Data;

namespace MusicLibrary.Converters;

/// <summary>
/// Compares the track rendered by a row with the ViewModel's PlayingTrack.
/// The comparison uses Id so copied Track objects still highlight consistently.
/// </summary>
public sealed class TrackIdentityMatchConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not Track currentTrack || values[1] is not Track playingTrack)
        {
            return false;
        }

        return currentTrack.Id == playingTrack.Id;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
```

- [ ] **Step 4: Register the converter resource**

In `MusicLibrary/App.xaml`, add this resource after `StatusKindToBrushConverter`:

```xml
<converters:TrackIdentityMatchConverter x:Key="TrackIdentityMatchConverter" />
```

The converter must remain inside `<ResourceDictionary>` and outside `<ResourceDictionary.MergedDictionaries>`.

- [ ] **Step 5: Run converter tests and verify they pass**

Run:

```powershell
dotnet test .\MusicLibrary.Tests\MusicLibrary.Tests.csproj --filter TrackIdentityMatchConverterTests
```

Expected:

```text
Passed!  - Failed: 0, Passed: 3
```

- [ ] **Step 6: Commit converter**

Run:

```powershell
git add .\MusicLibrary\Converters\TrackIdentityMatchConverter.cs .\MusicLibrary\App.xaml .\MusicLibrary.Tests\TrackIdentityMatchConverterTests.cs
git commit -m "feat: add playing track identity converter"
```

Expected:

```text
[codex/wpf-music-library ...] feat: add playing track identity converter
```

## Task 4: Wire Library and History Interactions in XAML

**Files:**

- Modify: `MusicLibrary/Resources/TrackTemplates.xaml`
- Test: `MusicLibrary/Resources/TrackTemplates.xaml` via WPF build

- [ ] **Step 1: Replace the track card template with command-aware markup**

In `MusicLibrary/Resources/TrackTemplates.xaml`, replace the full `TrackCardTemplate` with:

```xml
<DataTemplate x:Key="TrackCardTemplate">
    <Border x:Name="CardRoot" Padding="14" CornerRadius="12" Background="{StaticResource AccentBrush}" BorderBrush="{StaticResource GoldBorderBrush}" BorderThickness="1" Cursor="Hand">
        <Border.InputBindings>
            <MouseBinding MouseAction="LeftDoubleClick"
                          Command="{Binding DataContext.PlayTrackCommand, RelativeSource={RelativeSource AncestorType=ListBox}}"
                          CommandParameter="{Binding}" />
        </Border.InputBindings>
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="64" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="22" />
            </Grid.ColumnDefinitions>
            <Border Width="64" Height="64" CornerRadius="10">
                <Border.Background>
                    <ImageBrush ImageSource="{Binding CoverPath}" Stretch="UniformToFill" />
                </Border.Background>
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
            <Grid Grid.Column="2" Width="18" HorizontalAlignment="Right" VerticalAlignment="Top">
                <Ellipse x:Name="SelectedDot" Width="8" Height="8" Fill="{StaticResource PrimaryBrush}" Visibility="Collapsed" HorizontalAlignment="Right" />
                <StackPanel x:Name="PlayingBars" Orientation="Horizontal" Visibility="Collapsed" HorizontalAlignment="Right" Height="14">
                    <Rectangle Width="3" Height="8" RadiusX="1.5" RadiusY="1.5" Fill="{StaticResource PrimaryBrush}" Margin="0,6,2,0" />
                    <Rectangle Width="3" Height="14" RadiusX="1.5" RadiusY="1.5" Fill="{StaticResource PrimaryBrush}" Margin="0,0,2,0" />
                    <Rectangle Width="3" Height="10" RadiusX="1.5" RadiusY="1.5" Fill="{StaticResource PrimaryBrush}" Margin="0,4,0,0" />
                </StackPanel>
            </Grid>
        </Grid>
    </Border>
    <DataTemplate.Triggers>
        <DataTrigger Binding="{Binding RelativeSource={RelativeSource AncestorType=ListBoxItem}, Path=IsSelected}" Value="True">
            <Setter TargetName="CardRoot" Property="Background" Value="{StaticResource SelectedTrackBrush}" />
            <Setter TargetName="CardRoot" Property="BorderBrush" Value="{StaticResource PrimaryBrush}" />
            <Setter TargetName="CardRoot" Property="BorderThickness" Value="2" />
            <Setter TargetName="SelectedDot" Property="Visibility" Value="Visible" />
        </DataTrigger>
        <DataTrigger Value="True">
            <DataTrigger.Binding>
                <MultiBinding Converter="{StaticResource TrackIdentityMatchConverter}">
                    <Binding />
                    <Binding RelativeSource="{RelativeSource AncestorType=ListBox}" Path="DataContext.PlayingTrack" />
                </MultiBinding>
            </DataTrigger.Binding>
            <Setter TargetName="CardRoot" Property="Background" Value="#402A2018" />
            <Setter TargetName="CardRoot" Property="BorderBrush" Value="{StaticResource PrimaryBrush}" />
            <Setter TargetName="CardRoot" Property="BorderThickness" Value="2" />
            <Setter TargetName="SelectedDot" Property="Visibility" Value="Collapsed" />
            <Setter TargetName="PlayingBars" Property="Visibility" Value="Visible" />
        </DataTrigger>
    </DataTemplate.Triggers>
</DataTemplate>
```

- [ ] **Step 2: Add double-click replay to history items**

In the same file, replace the opening border of `HistoryItemTemplate`:

```xml
<Border Padding="12" CornerRadius="10" Background="#332A2A3F" BorderBrush="{StaticResource GoldBorderBrush}" BorderThickness="1" Margin="0,0,0,10">
```

with:

```xml
<Border Padding="12" CornerRadius="10" Background="#332A2A3F" BorderBrush="{StaticResource GoldBorderBrush}" BorderThickness="1" Margin="0,0,0,10" Cursor="Hand">
    <Border.InputBindings>
        <MouseBinding MouseAction="LeftDoubleClick"
                      Command="{Binding DataContext.ReplayHistoryEntryCommand, RelativeSource={RelativeSource AncestorType=ListBox}}"
                      CommandParameter="{Binding}" />
    </Border.InputBindings>
```

Keep the existing inner `<Grid>` under the new `<Border.InputBindings>`.

- [ ] **Step 3: Build the WPF project and verify XAML compiles**

Run:

```powershell
dotnet build .\MusicLibrary\MusicLibrary.csproj
```

Expected:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

- [ ] **Step 4: Commit XAML interactions**

Run:

```powershell
git add .\MusicLibrary\Resources\TrackTemplates.xaml
git commit -m "feat: wire MusicBakh library playback interactions"
```

Expected:

```text
[codex/wpf-music-library ...] feat: wire MusicBakh library playback interactions
```

## Task 5: Apply MusicBakh Shell Design

**Files:**

- Modify: `MusicLibrary/MainWindow.xaml`
- Test: `MusicLibrary/MainWindow.xaml` via WPF build and manual app run

- [ ] **Step 1: Rename the window title**

In the root `<Window>`, replace:

```xml
Title="Музыкальная Библиотека"
```

with:

```xml
Title="MusicBakh"
```

- [ ] **Step 2: Replace the top header block**

Replace the entire first header `<Border Grid.Row="0"...>` block with:

```xml
<Border Background="{StaticResource CardOverlayBrush}" BorderBrush="{StaticResource GoldBorderBrush}" BorderThickness="0,0,0,1" Padding="28,18">
    <DockPanel VerticalAlignment="Center">
        <Button DockPanel.Dock="Right" Content="＋ Добавить трек" Command="{Binding AddTrackCommand}" Style="{StaticResource SecondaryButtonStyle}" Height="42" Padding="18,0" />
        <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
            <Border Width="54" Height="54" CornerRadius="12" Background="{StaticResource GoldGradientBrush}">
                <Grid>
                    <TextBlock Text="MB" FontSize="18" FontWeight="Bold" Foreground="{StaticResource BackgroundBrush}" HorizontalAlignment="Center" VerticalAlignment="Center" Margin="0,0,0,8" />
                    <Polyline Points="12,38 18,32 24,36 30,26 36,34 42,30"
                              Stroke="{StaticResource BackgroundBrush}"
                              StrokeThickness="2"
                              StrokeStartLineCap="Round"
                              StrokeEndLineCap="Round"
                              StrokeLineJoin="Round"
                              VerticalAlignment="Bottom"
                              Margin="0,0,0,8" />
                </Grid>
            </Border>
            <StackPanel Margin="14,0,0,0" VerticalAlignment="Center">
                <TextBlock Text="MusicBakh" FontFamily="{StaticResource HeadingFont}" Foreground="{StaticResource PrimaryBrush}" FontSize="28" FontWeight="SemiBold" />
                <TextBlock Text="Музыкальная библиотека" Foreground="{StaticResource MutedForegroundBrush}" FontSize="13" Margin="1,2,0,0" />
            </StackPanel>
        </StackPanel>
    </DockPanel>
</Border>
```

- [ ] **Step 3: Compact the center panel**

Inside the center column `<Border Grid.Column="1"...>`, make these exact changes:

Replace:

```xml
Padding="28"
```

with:

```xml
Padding="24"
```

Replace the cover border:

```xml
<Border Width="196" Height="196" CornerRadius="18" BorderBrush="{StaticResource PrimaryBrush}" BorderThickness="2" HorizontalAlignment="Center">
```

with:

```xml
<Border Width="176" Height="176" CornerRadius="16" BorderBrush="{StaticResource PrimaryBrush}" BorderThickness="2" HorizontalAlignment="Center">
```

Replace the title text block:

```xml
<TextBlock Text="{Binding SelectedTrack.Title, TargetNullValue='Выберите трек'}" Foreground="{StaticResource ForegroundBrush}" FontSize="22" FontWeight="SemiBold" TextAlignment="Center" TextWrapping="Wrap" Margin="0,24,0,6" />
```

with:

```xml
<TextBlock Text="{Binding SelectedTrack.Title, TargetNullValue='Выберите трек'}" Foreground="{StaticResource ForegroundBrush}" FontSize="21" FontWeight="SemiBold" TextAlignment="Center" TextWrapping="Wrap" Margin="0,20,0,6" />
```

Replace the metadata stack margin:

```xml
<StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,14,0,20">
```

with:

```xml
<StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,12,0,18">
```

- [ ] **Step 4: Build the solution**

Run:

```powershell
dotnet build .\MusicLibrary.sln
```

Expected:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

- [ ] **Step 5: Manually verify visible UX**

Run:

```powershell
dotnet run --project .\MusicLibrary\MusicLibrary.csproj
```

Expected visible result:

```text
The app window opens with the title "MusicBakh".
The header shows the MB monogram and wave mark.
Double-clicking a library track starts playback and highlights that track with the playing bars.
Clicking another track only changes the selected center panel; the previous playing track remains highlighted.
Double-clicking a history item selects and starts that track.
```

Close the WPF app window after checking these states.

- [ ] **Step 6: Commit shell design**

Run:

```powershell
git add .\MusicLibrary\MainWindow.xaml
git commit -m "feat: apply MusicBakh player shell"
```

Expected:

```text
[codex/wpf-music-library ...] feat: apply MusicBakh player shell
```

## Task 6: Document Work Difference

**Files:**

- Modify: `MusicLibrary/work_diff.md`

- [ ] **Step 1: Append section 10**

Append this text to the end of `MusicLibrary/work_diff.md`:

```markdown

## 10. Первый пакет MusicBakh UX

В работе интерфейс описан как простая музыкальная библиотека с выбором трека, фильтром, воспроизведением, сохранением и историей.

В приложении первый UX-пакет оформляет этот же функционал как продукт MusicBakh:

- в шапке используется название MusicBakh и знак MB с аудиоволной;
- выбранный трек и фактически воспроизводимый трек визуально разделены;
- карточка текущего воспроизводимого трека подсвечивается в библиотеке независимо от выбранного фильтра;
- двойной клик по карточке трека запускает воспроизведение;
- двойной клик по элементу истории снова запускает этот трек;
- центральная панель плеера стала компактнее, чтобы основные элементы помещались без лишней прокрутки.

Причина: базовая работа описывает обязательные функции, но не фиксирует UX для ситуации, когда пользователь смотрит один трек, а играет другой. Разделение состояний делает приложение понятнее и готовит интерфейс к следующим пакетам: активному прогресс-бару, автопереходу к следующему треку и будущей NAudio-визуализации.

Функция проекта не меняется по смыслу: библиотека, фильтр, воспроизведение, сохранение и история остаются теми же учебными сценариями, но получают более ясное поведение и визуальную обратную связь.
```

- [ ] **Step 2: Commit documentation**

Run:

```powershell
git add .\MusicLibrary\work_diff.md
git commit -m "docs: record MusicBakh UX package difference"
```

Expected:

```text
[codex/wpf-music-library ...] docs: record MusicBakh UX package difference
```

## Task 7: Full Verification

**Files:**

- Verify all modified files.

- [ ] **Step 1: Run the full automated test suite**

Run:

```powershell
dotnet test .\MusicLibrary.sln
```

Expected:

```text
Passed!  - Failed: 0
```

- [ ] **Step 2: Run a release build**

Run:

```powershell
dotnet build .\MusicLibrary.sln -c Release
```

Expected:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

- [ ] **Step 3: Review git diff**

Run:

```powershell
git diff --stat HEAD~4..HEAD
git status --short
```

Expected:

```text
MusicLibrary/App.xaml
MusicLibrary/MainWindow.xaml
MusicLibrary/Resources/TrackTemplates.xaml
MusicLibrary/ViewModels/MainViewModel.cs
MusicLibrary/Converters/TrackIdentityMatchConverter.cs
MusicLibrary/work_diff.md
MusicLibrary.Tests/MainViewModelTests.cs
MusicLibrary.Tests/TrackIdentityMatchConverterTests.cs
```

`git status --short` should print no unstaged or untracked files related to this package.

## Invariants Checklist

- `SelectedTrack` means "shown in the center panel".
- `PlayingTrack` means "owned by the audio service".
- Changing selection does not stop playback.
- Changing genre does not stop playback.
- Highlighting of the currently playing track is based on `Track.Id`, not object reference.
- History is added only after `MediaOpened` for the pending track.
- Replaying history uses the same playback path as library double-click.
- Missing files show an error and do not clear the previous valid playback unless a new track successfully takes ownership.
- The first package keeps the existing WPF `MediaPlayer` service and does not introduce NAudio.
- No queue, auto-next, active seek, or taskbar icon work is included in this package.

## Self-Review

- Spec coverage: covered branding, selected-vs-playing visual separation, double-click library start, double-click history replay, compact center panel, failure rules, required comments, tests, and `work_diff.md`.
- Placeholder scan: no deferred implementation markers are present; every code step includes concrete snippets or exact replacements.
- Type consistency: `PlayTrackCommand`, `ReplayHistoryEntryCommand`, `StartOrResumeTrack`, and `TrackIdentityMatchConverter` names are consistent across tests, implementation, XAML resources, and verification commands.
