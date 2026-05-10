# Release UI Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Polish the first-release MusicBakh UI by replacing system scrollbars/sliders with themed controls, hiding seek/time when the selected track is not the playing track, and fixing the repeat indicator layout.

**Architecture:** Keep this package UI-only: create dedicated WPF resource dictionaries for `ScrollBar` and `Slider`, merge them through `App.xaml`, and apply named slider styles in `MainWindow.xaml`. No ViewModel behavior changes are required; the only binding behavior change is making the seek/time row visible only when `IsSelectedPlaying` is true.

**Tech Stack:** WPF XAML, ResourceDictionary, ControlTemplate, ScrollBar, Slider, existing MusicBakh brushes and commands, .NET 10.

---

## Context

Spec: `docs/superpowers/specs/2026-05-10-release-ui-polish-design.md`

Current relevant files:

```text
MusicLibrary/App.xaml
MusicLibrary/MainWindow.xaml
MusicLibrary/Resources/Brushes.xaml
MusicLibrary/Resources/ButtonStyles.xaml
MusicLibrary/Resources/ComboBoxStyles.xaml
MusicLibrary/Resources/ListStyles.xaml
```

Context7 WPF references used:

- WPF merged dictionaries are included with `<ResourceDictionary Source="..." />`.
- WPF `ScrollBar` and `Slider` visuals are customized through `ControlTemplate`, `Track`, `Thumb`, and `RepeatButton`.

## File Map

Create:

```text
MusicLibrary/Resources/ScrollBarStyles.xaml
MusicLibrary/Resources/SliderStyles.xaml
```

Modify:

```text
MusicLibrary/App.xaml
MusicLibrary/MainWindow.xaml
MusicLibrary/Resources/ComboBoxStyles.xaml
```

Do not modify:

```text
MusicLibrary/ViewModels/MainViewModel.cs
MusicLibrary/work_diff.md
MusicLibrary/Resources/ButtonStyles.xaml
```

`ButtonStyles.xaml` already contains `IconButtonStyle` and `PrimaryIconButtonStyle`; this package does not need more button styles.

## Task 1: Themed ScrollBars

**Files:**

- Create: `MusicLibrary/Resources/ScrollBarStyles.xaml`
- Modify: `MusicLibrary/App.xaml`
- Verify: `MusicLibrary/Resources/ComboBoxStyles.xaml` through build and visual smoke

- [ ] **Step 1: Create the scrollbar resource dictionary**

Create `MusicLibrary/Resources/ScrollBarStyles.xaml`:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Прозрачная кнопка страницы: клики по треку скроллят, но системных стрелок нет. -->
    <Style x:Key="MusicBakhScrollBarPageButtonStyle" TargetType="RepeatButton">
        <Setter Property="Focusable" Value="False" />
        <Setter Property="IsTabStop" Value="False" />
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="RepeatButton">
                    <Border Background="{TemplateBinding Background}" />
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Золотой ползунок скролла: тонкий, без изменения размеров при hover/drag. -->
    <Style x:Key="MusicBakhScrollBarThumbStyle" TargetType="Thumb">
        <Setter Property="MinHeight" Value="28" />
        <Setter Property="MinWidth" Value="28" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Thumb">
                    <Border x:Name="ThumbRoot"
                            Background="#80D4A574"
                            CornerRadius="4" />
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="ThumbRoot" Property="Background" Value="#B3D4A574" />
                        </Trigger>
                        <Trigger Property="IsDragging" Value="True">
                            <Setter TargetName="ThumbRoot" Property="Background" Value="{StaticResource PrimaryBrush}" />
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter TargetName="ThumbRoot" Property="Opacity" Value="0.35" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <ControlTemplate x:Key="MusicBakhVerticalScrollBarTemplate" TargetType="{x:Type ScrollBar}">
        <Grid Width="8" Background="Transparent" SnapsToDevicePixels="True">
            <Border Width="8" Background="#0DFFFFFF" CornerRadius="4" />
            <Track x:Name="PART_Track" Orientation="Vertical" IsDirectionReversed="True">
                <Track.DecreaseRepeatButton>
                    <RepeatButton Command="ScrollBar.PageUpCommand"
                                  Style="{StaticResource MusicBakhScrollBarPageButtonStyle}" />
                </Track.DecreaseRepeatButton>
                <Track.Thumb>
                    <Thumb Style="{StaticResource MusicBakhScrollBarThumbStyle}" />
                </Track.Thumb>
                <Track.IncreaseRepeatButton>
                    <RepeatButton Command="ScrollBar.PageDownCommand"
                                  Style="{StaticResource MusicBakhScrollBarPageButtonStyle}" />
                </Track.IncreaseRepeatButton>
            </Track>
        </Grid>
    </ControlTemplate>

    <ControlTemplate x:Key="MusicBakhHorizontalScrollBarTemplate" TargetType="{x:Type ScrollBar}">
        <Grid Height="8" Background="Transparent" SnapsToDevicePixels="True">
            <Border Height="8" Background="#0DFFFFFF" CornerRadius="4" />
            <Track x:Name="PART_Track" Orientation="Horizontal" IsDirectionReversed="False">
                <Track.DecreaseRepeatButton>
                    <RepeatButton Command="ScrollBar.PageLeftCommand"
                                  Style="{StaticResource MusicBakhScrollBarPageButtonStyle}" />
                </Track.DecreaseRepeatButton>
                <Track.Thumb>
                    <Thumb Style="{StaticResource MusicBakhScrollBarThumbStyle}" />
                </Track.Thumb>
                <Track.IncreaseRepeatButton>
                    <RepeatButton Command="ScrollBar.PageRightCommand"
                                  Style="{StaticResource MusicBakhScrollBarPageButtonStyle}" />
                </Track.IncreaseRepeatButton>
            </Track>
        </Grid>
    </ControlTemplate>

    <!-- Глобальный стиль скроллбаров: подхватывается ScrollViewer, ListBox и ComboBox popup. -->
    <Style TargetType="{x:Type ScrollBar}">
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="Foreground" Value="{StaticResource PrimaryBrush}" />
        <Setter Property="Width" Value="8" />
        <Setter Property="MinWidth" Value="8" />
        <Setter Property="Template" Value="{StaticResource MusicBakhVerticalScrollBarTemplate}" />
        <Style.Triggers>
            <Trigger Property="Orientation" Value="Horizontal">
                <Setter Property="Width" Value="Auto" />
                <Setter Property="MinWidth" Value="0" />
                <Setter Property="Height" Value="8" />
                <Setter Property="MinHeight" Value="8" />
                <Setter Property="Template" Value="{StaticResource MusicBakhHorizontalScrollBarTemplate}" />
            </Trigger>
        </Style.Triggers>
    </Style>
</ResourceDictionary>
```

- [ ] **Step 2: Merge ScrollBarStyles in App.xaml**

In `MusicLibrary/App.xaml`, inside `<ResourceDictionary.MergedDictionaries>`, insert `ScrollBarStyles.xaml` after `Brushes.xaml` and before `ButtonStyles.xaml`:

```xml
<ResourceDictionary Source="Resources/Colors.xaml" />
<ResourceDictionary Source="Resources/Brushes.xaml" />
<ResourceDictionary Source="Resources/ScrollBarStyles.xaml" />
<ResourceDictionary Source="Resources/ButtonStyles.xaml" />
<ResourceDictionary Source="Resources/ComboBoxStyles.xaml" />
<ResourceDictionary Source="Resources/ListStyles.xaml" />
<ResourceDictionary Source="Resources/PlayerIcons.xaml" />
<ResourceDictionary Source="Resources/TrackTemplates.xaml" />
```

- [ ] **Step 3: Make the genre popup explicitly use scrollbars**

In `MusicLibrary/Resources/ComboBoxStyles.xaml`, replace the popup `ScrollViewer`:

```xml
<ScrollViewer SnapsToDevicePixels="True" Padding="2">
    <StackPanel IsItemsHost="True" KeyboardNavigation.DirectionalNavigation="Contained" />
</ScrollViewer>
```

with:

```xml
<ScrollViewer SnapsToDevicePixels="True"
              Padding="2"
              VerticalScrollBarVisibility="Auto"
              HorizontalScrollBarVisibility="Disabled">
    <StackPanel IsItemsHost="True" KeyboardNavigation.DirectionalNavigation="Contained" />
</ScrollViewer>
```

- [ ] **Step 4: Build and verify XAML compiles**

Run:

```powershell
dotnet build .\MusicLibrary\MusicLibrary.csproj --no-restore
```

Expected:

```text
Сборка успешно завершена.
    Предупреждений: 0
    Ошибок: 0
```

- [ ] **Step 5: Commit scrollbar styles**

Run:

```powershell
git add .\MusicLibrary\Resources\ScrollBarStyles.xaml `
        .\MusicLibrary\App.xaml `
        .\MusicLibrary\Resources\ComboBoxStyles.xaml
git commit -m "feat: add themed release scrollbars"
```

Expected:

```text
[codex/wpf-music-library ...] feat: add themed release scrollbars
```

## Task 2: Themed Player Sliders

**Files:**

- Create: `MusicLibrary/Resources/SliderStyles.xaml`
- Modify: `MusicLibrary/App.xaml`

- [ ] **Step 1: Create the slider resource dictionary**

Create `MusicLibrary/Resources/SliderStyles.xaml`:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Левая заполненная часть слайдера: показывает текущую позицию / громкость. -->
    <Style x:Key="MusicBakhSliderDecreaseButtonStyle" TargetType="RepeatButton">
        <Setter Property="Focusable" Value="False" />
        <Setter Property="IsTabStop" Value="False" />
        <Setter Property="Background" Value="{StaticResource PrimaryBrush}" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="RepeatButton">
                    <Border Background="{TemplateBinding Background}"
                            CornerRadius="3" />
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Правая незаполненная часть слайдера: тихий фон без системных цветов. -->
    <Style x:Key="MusicBakhSliderIncreaseButtonStyle" TargetType="RepeatButton">
        <Setter Property="Focusable" Value="False" />
        <Setter Property="IsTabStop" Value="False" />
        <Setter Property="Background" Value="#332A2A3F" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="RepeatButton">
                    <Border Background="{TemplateBinding Background}"
                            CornerRadius="3" />
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Круглый thumb: постоянный размер, hover меняет только цвет и тень. -->
    <Style x:Key="MusicBakhSliderThumbStyle" TargetType="Thumb">
        <Setter Property="Width" Value="16" />
        <Setter Property="Height" Value="16" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Thumb">
                    <Ellipse x:Name="ThumbRoot"
                             Fill="{StaticResource PrimaryBrush}"
                             Stroke="{StaticResource BackgroundBrush}"
                             StrokeThickness="1">
                        <Ellipse.Effect>
                            <DropShadowEffect Color="#D4A574" BlurRadius="14" ShadowDepth="0" Opacity="0.24" />
                        </Ellipse.Effect>
                    </Ellipse>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="ThumbRoot" Property="Fill" Value="#E7BF8E" />
                        </Trigger>
                        <Trigger Property="IsDragging" Value="True">
                            <Setter TargetName="ThumbRoot" Property="Fill" Value="#F0CAA0" />
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter TargetName="ThumbRoot" Property="Opacity" Value="0.35" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Базовый горизонтальный слайдер плеера. Tag задаёт высоту дорожки: seek=6, volume=5. -->
    <Style x:Key="PlayerSliderStyle" TargetType="Slider">
        <Setter Property="Height" Value="26" />
        <Setter Property="Tag" Value="6" />
        <Setter Property="Background" Value="#332A2A3F" />
        <Setter Property="Foreground" Value="{StaticResource PrimaryBrush}" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Slider">
                    <Grid x:Name="SliderRoot"
                          MinHeight="{TemplateBinding Height}"
                          SnapsToDevicePixels="True">
                        <Track x:Name="PART_Track"
                               Height="{Binding Tag, RelativeSource={RelativeSource TemplatedParent}}"
                               VerticalAlignment="Center">
                            <Track.DecreaseRepeatButton>
                                <RepeatButton Command="Slider.DecreaseLarge"
                                              Background="{StaticResource PrimaryBrush}"
                                              Style="{StaticResource MusicBakhSliderDecreaseButtonStyle}" />
                            </Track.DecreaseRepeatButton>
                            <Track.Thumb>
                                <Thumb Style="{StaticResource MusicBakhSliderThumbStyle}" />
                            </Track.Thumb>
                            <Track.IncreaseRepeatButton>
                                <RepeatButton Command="Slider.IncreaseLarge"
                                              Background="#332A2A3F"
                                              Style="{StaticResource MusicBakhSliderIncreaseButtonStyle}" />
                            </Track.IncreaseRepeatButton>
                        </Track>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter TargetName="SliderRoot" Property="Opacity" Value="0.38" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style x:Key="SeekSliderStyle" TargetType="Slider" BasedOn="{StaticResource PlayerSliderStyle}">
        <Setter Property="Height" Value="28" />
        <Setter Property="Tag" Value="6" />
    </Style>

    <Style x:Key="VolumeSliderStyle" TargetType="Slider" BasedOn="{StaticResource PlayerSliderStyle}">
        <Setter Property="Height" Value="24" />
        <Setter Property="Tag" Value="5" />
    </Style>
</ResourceDictionary>
```

- [ ] **Step 2: Merge SliderStyles in App.xaml**

In `MusicLibrary/App.xaml`, insert `SliderStyles.xaml` after `ScrollBarStyles.xaml`:

```xml
<ResourceDictionary Source="Resources/Colors.xaml" />
<ResourceDictionary Source="Resources/Brushes.xaml" />
<ResourceDictionary Source="Resources/ScrollBarStyles.xaml" />
<ResourceDictionary Source="Resources/SliderStyles.xaml" />
<ResourceDictionary Source="Resources/ButtonStyles.xaml" />
<ResourceDictionary Source="Resources/ComboBoxStyles.xaml" />
<ResourceDictionary Source="Resources/ListStyles.xaml" />
<ResourceDictionary Source="Resources/PlayerIcons.xaml" />
<ResourceDictionary Source="Resources/TrackTemplates.xaml" />
```

- [ ] **Step 3: Build and verify XAML compiles**

Run:

```powershell
dotnet build .\MusicLibrary\MusicLibrary.csproj --no-restore
```

Expected:

```text
Сборка успешно завершена.
    Предупреждений: 0
    Ошибок: 0
```

- [ ] **Step 4: Commit slider styles**

Run:

```powershell
git add .\MusicLibrary\Resources\SliderStyles.xaml .\MusicLibrary\App.xaml
git commit -m "feat: add themed player sliders"
```

Expected:

```text
[codex/wpf-music-library ...] feat: add themed player sliders
```

## Task 3: Wire Release Polish Into MainWindow

**Files:**

- Modify: `MusicLibrary/MainWindow.xaml`

- [ ] **Step 1: Hide seek/time when the selected track is not playing**

In `MusicLibrary/MainWindow.xaml`, find the seek row:

```xml
<Grid Margin="0,0,0,12">
```

This is the grid that contains `SeekSlider`, `CurrentPositionText`, and `CurrentDurationText`. Replace the opening tag with:

```xml
<Grid Margin="0,0,0,12"
      Visibility="{Binding IsSelectedPlaying, Converter={StaticResource BooleanToVisibilityConverter}}">
```

The transport row and utility row stay visible. Do not add `Visibility` to the play/pause, skip, next, volume, mute, repeat, save, delete, or status controls.

- [ ] **Step 2: Apply the seek slider style**

In the same seek row, update `SeekSlider` by adding `Style="{StaticResource SeekSliderStyle}"`. The final `Slider` opening block must be:

```xml
<Slider x:Name="SeekSlider"
        Grid.Column="1"
        Style="{StaticResource SeekSliderStyle}"
        Minimum="0"
        Maximum="{Binding ProgressMaximum}"
        Value="{Binding ProgressValue, Mode=OneWay}"
        IsMoveToPointEnabled="True"
        IsEnabled="{Binding PlayingTrack, Converter={StaticResource NullToBooleanConverter}}"
        Thumb.DragStarted="OnSeekDragStarted"
        PreviewMouseUp="OnSeekPreviewMouseUp"
        Margin="8,0" />
```

- [ ] **Step 3: Apply the volume slider style**

Find `VolumeSlider` and add `Style="{StaticResource VolumeSliderStyle}"`. The final `Slider` opening block must be:

```xml
<Slider x:Name="VolumeSlider"
        Grid.Column="1"
        Style="{StaticResource VolumeSliderStyle}"
        Minimum="0"
        Maximum="1"
        Value="{Binding Volume, Mode=TwoWay}"
        IsMoveToPointEnabled="True"
        VerticalAlignment="Center" />
```

- [ ] **Step 4: Replace the repeat button content layout**

Find the current repeat button content:

```xml
<Button Grid.Column="4" Command="{Binding CycleRepeatModeCommand}" Style="{StaticResource IconButtonStyle}" ToolTip="Режим повтора" Margin="4,0,0,0">
    <Grid Width="22" Height="22">
        <Path Data="{StaticResource IconRepeatGeometry}" Style="{StaticResource RepeatIconStyle}" Width="20" Height="20" Stretch="Uniform" />
        <TextBlock Style="{StaticResource RepeatLabelStyle}" FontSize="8" FontWeight="Bold" HorizontalAlignment="Right" VerticalAlignment="Bottom" />
    </Grid>
</Button>
```

Replace it with:

```xml
<Button Grid.Column="4" Command="{Binding CycleRepeatModeCommand}" Style="{StaticResource IconButtonStyle}" ToolTip="Режим повтора" Margin="4,0,0,0">
    <Grid Width="28" Height="30">
        <Grid.RowDefinitions>
            <RowDefinition Height="18" />
            <RowDefinition Height="12" />
        </Grid.RowDefinitions>
        <Path Grid.Row="0"
              Data="{StaticResource IconRepeatGeometry}"
              Style="{StaticResource RepeatIconStyle}"
              Width="18"
              Height="18"
              Stretch="Uniform"
              HorizontalAlignment="Center" />
        <TextBlock Grid.Row="1"
                   Style="{StaticResource RepeatLabelStyle}"
                   FontSize="10"
                   FontWeight="Bold"
                   MinWidth="24"
                   TextAlignment="Center"
                   HorizontalAlignment="Center"
                   VerticalAlignment="Top" />
    </Grid>
</Button>
```

Do not set `Text` or `Foreground` locally on the repeat label because `RepeatLabelStyle` owns both.

- [ ] **Step 5: Build and run the automated suite**

Run:

```powershell
dotnet build .\MusicLibrary\MusicLibrary.csproj --no-restore
dotnet test .\MusicLibrary.sln --no-restore
```

Expected build:

```text
Сборка успешно завершена.
    Предупреждений: 0
    Ошибок: 0
```

Expected tests:

```text
Пройден!   : не пройдено     0
```

- [ ] **Step 6: Commit MainWindow polish wiring**

Run:

```powershell
git add .\MusicLibrary\MainWindow.xaml
git commit -m "fix: polish release player layout"
```

Expected:

```text
[codex/wpf-music-library ...] fix: polish release player layout
```

## Task 4: Release UI Verification

**Files:**

- Verify all changed files.

- [ ] **Step 1: Run full verification commands**

Run:

```powershell
dotnet build .\MusicLibrary.sln --no-restore
dotnet test .\MusicLibrary.sln --no-restore
dotnet build .\MusicLibrary.sln -c Release --no-restore
```

Expected for both builds:

```text
Сборка успешно завершена.
    Предупреждений: 0
    Ошибок: 0
```

Expected for tests:

```text
Пройден!   : не пройдено     0
```

- [ ] **Step 2: Run startup smoke**

Run:

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

- [ ] **Step 3: Complete manual visual smoke**

Run:

```powershell
dotnet run --project .\MusicLibrary\MusicLibrary.csproj
```

Manually verify these exact states:

```text
1. Library scrollbar is thin, dark, and gold-accented.
2. Center panel scrollbar is thin and themed when the window height is reduced.
3. History scrollbar is thin and themed.
4. Genre dropdown scrollbar is thin and themed when enough genres exist.
5. Start track A, then select track B: seek/time row is hidden and "играет: A" badge remains.
6. Select track A again: seek/time row returns and shows track A position.
7. Seek slider has gold fill and themed thumb.
8. Volume slider has gold fill and themed thumb.
9. Repeat Off/1/All label sits below the icon and does not overlap.
10. Hover over scrollbars and sliders: colors change without layout shift.
11. Maximize the app: no controls overlap in the center panel.
```

Close the app after the smoke check.

- [ ] **Step 4: Review git status and diff**

Run:

```powershell
git status --short
git diff --stat HEAD~3..HEAD -- MusicLibrary
```

Expected `git status --short`:

```text
?? .claude/
```

Expected changed file list in diff stat:

```text
MusicLibrary/App.xaml
MusicLibrary/MainWindow.xaml
MusicLibrary/Resources/ComboBoxStyles.xaml
MusicLibrary/Resources/ScrollBarStyles.xaml
MusicLibrary/Resources/SliderStyles.xaml
```

- [ ] **Step 5: Commit final polish only if manual smoke required a correction**

If manual smoke surfaced a concrete XAML correction, apply that correction and run:

```powershell
dotnet build .\MusicLibrary.sln --no-restore
dotnet test .\MusicLibrary.sln --no-restore
git add .\MusicLibrary
git commit -m "fix: finalize release UI polish"
```

If manual smoke required no correction, run:

```powershell
git status --short
```

Expected:

```text
?? .claude/
```

## Self-Review

- Spec coverage: scrollbars are covered by Task 1; sliders by Task 2 and Task 3; seek visibility by Task 3; repeat label by Task 3; visual verification by Task 4.
- Placeholder scan: no deferred implementation markers are present; every code step contains exact XAML or exact commands.
- Type consistency: resource keys are consistent across tasks: `ScrollBarStyles.xaml`, `SliderStyles.xaml`, `SeekSliderStyle`, `VolumeSliderStyle`, `MusicBakhScrollBarThumbStyle`, `MusicBakhSliderThumbStyle`.
- Scope check: no icon package, README, installer, repository cleanup, `.claude`, or `docs/superpowers` removal is included.
