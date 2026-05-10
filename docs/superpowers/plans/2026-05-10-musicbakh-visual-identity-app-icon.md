# MusicBakh Visual Identity & App Icon Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add production MusicBakh brand assets and wire them into the WPF executable, window, taskbar, and header.

**Architecture:** Generate all visual assets from one deterministic local script so the icon/logo can be reproduced without Figma. Store source/export assets under `docs/assets/musicbakh`, copy runtime assets under `MusicLibrary/Assets/Brand`, then configure WPF through `ApplicationIcon`, `Window.Icon`, and a header image replacement.

**Tech Stack:** WPF XAML, SDK-style `.csproj`, `.ico`, PNG/SVG assets, PowerShell, Python 3.11 with Pillow.

---

## Context

Spec:

```text
docs/superpowers/specs/2026-05-10-musicbakh-visual-identity-app-icon-design.md
```

Figma reference:

```text
https://www.figma.com/design/rEQaDqABGp6PVk9X46MmsE
```

The Figma file is a visual reference only. Do not reproduce its board layout. The accepted design is:

```text
Premium Library + restrained Modern Audio
Primary logo: MB + audio wave
App icon: rounded dark square + inner disc + gold rim + small cyan/steel audio wave
```

Current project facts:

- `MusicLibrary/MusicLibrary.csproj` has no `ApplicationIcon`.
- `MusicLibrary/MainWindow.xaml` has `Title="MusicBakh"` and no `Icon`.
- Header currently uses an inline temporary `MB` text + `Polyline` wave inside a `54x54` rounded square.
- Local `python` is available.
- Local Pillow is available.
- `magick` and `inkscape` are not available.

Context7 WPF documentation reference:

- WPF `Window.Icon` can point to an `.ico` resource through the `Icon` attribute.
- WPF resources can be included from the project file with concrete `<Resource Include="Assets\Brand\musicbakh.ico" />` items.

## File Map

Create:

```text
docs/assets/musicbakh/generate_musicbakh_assets.ps1
docs/assets/musicbakh/musicbakh-app-icon.svg
docs/assets/musicbakh/musicbakh-primary-logo.svg
docs/assets/musicbakh/musicbakh-app-icon-256.png
docs/assets/musicbakh/musicbakh-primary-logo.png
docs/assets/musicbakh/musicbakh.ico
MusicLibrary/Assets/Brand/musicbakh.ico
MusicLibrary/Assets/Brand/musicbakh-logo.png
```

Modify:

```text
MusicLibrary/MusicLibrary.csproj
MusicLibrary/MainWindow.xaml
```

Do not modify:

```text
MusicLibrary/ViewModels/*
MusicLibrary/Services/*
MusicLibrary/Models/*
MusicLibrary/work_diff.md
```

`work_diff.md` is intentionally not part of this plan because the spec marks it as conditional and the implementation boundary excludes it.

## Task 1: Generate Brand Assets

**Files:**

- Create: `docs/assets/musicbakh/generate_musicbakh_assets.ps1`
- Generate: `docs/assets/musicbakh/musicbakh-app-icon.svg`
- Generate: `docs/assets/musicbakh/musicbakh-primary-logo.svg`
- Generate: `docs/assets/musicbakh/musicbakh-app-icon-256.png`
- Generate: `docs/assets/musicbakh/musicbakh-primary-logo.png`
- Generate: `docs/assets/musicbakh/musicbakh.ico`
- Generate: `MusicLibrary/Assets/Brand/musicbakh.ico`
- Generate: `MusicLibrary/Assets/Brand/musicbakh-logo.png`

- [ ] **Step 1: Create asset directories**

Run:

```powershell
New-Item -ItemType Directory -Force .\docs\assets\musicbakh | Out-Null
New-Item -ItemType Directory -Force .\MusicLibrary\Assets\Brand | Out-Null
```

Expected:

```text
No output.
```

- [ ] **Step 2: Create the deterministic asset generator**

Create `docs/assets/musicbakh/generate_musicbakh_assets.ps1` with exactly this content:

```powershell
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$docsDir = Join-Path $repoRoot 'docs\assets\musicbakh'
$runtimeDir = Join-Path $repoRoot 'MusicLibrary\Assets\Brand'

New-Item -ItemType Directory -Force $docsDir | Out-Null
New-Item -ItemType Directory -Force $runtimeDir | Out-Null

$pythonCommand = Get-Command python -ErrorAction Stop
& $pythonCommand.Source -c "import PIL" 2>$null
if ($LASTEXITCODE -ne 0) {
    throw 'Python Pillow package is required. Install Pillow for the current Python interpreter before running this script.'
}

$pythonScript = Join-Path $env:TEMP ("musicbakh_assets_{0}.py" -f $PID)

@'
from pathlib import Path
import shutil
import sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

repo_root = Path(sys.argv[1])
docs_dir = repo_root / "docs" / "assets" / "musicbakh"
runtime_dir = repo_root / "MusicLibrary" / "Assets" / "Brand"
docs_dir.mkdir(parents=True, exist_ok=True)
runtime_dir.mkdir(parents=True, exist_ok=True)

C = {
    "bg": "#16161F",
    "panel": "#2A2A3F",
    "black": "#0D0D13",
    "gold": "#D4A574",
    "dark_gold": "#B8864F",
    "text": "#F4ECE3",
    "muted": "#A69A8F",
    "cyan": "#6BC7D8",
}

def rgba(hex_color, alpha=255):
    hex_color = hex_color.lstrip("#")
    return tuple(int(hex_color[i:i + 2], 16) for i in (0, 2, 4)) + (alpha,)

def font(size, bold=False):
    candidates = [
        "C:/Windows/Fonts/georgiab.ttf" if bold else "C:/Windows/Fonts/georgia.ttf",
        "C:/Windows/Fonts/segoeuib.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf",
        "C:/Windows/Fonts/arialbd.ttf" if bold else "C:/Windows/Fonts/arial.ttf",
    ]
    for candidate in candidates:
        path = Path(candidate)
        if path.exists():
            return ImageFont.truetype(str(path), size)
    return ImageFont.load_default()

def draw_wave(draw, points, color, width):
    draw.line(points, fill=rgba(color), width=width, joint="curve")
    radius = max(1, width // 2)
    for x, y in points:
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=rgba(color))

def rounded_mask(size, radius):
    mask = Image.new("L", size, 0)
    d = ImageDraw.Draw(mask)
    d.rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), radius=radius, fill=255)
    return mask

def draw_icon(size):
    scale = 4
    canvas = Image.new("RGBA", (size * scale, size * scale), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)
    s = size * scale

    def v(n):
        return int(round(n * scale))

    tile_radius = v(size * 0.20)
    draw.rounded_rectangle((0, 0, s - 1, s - 1), radius=tile_radius, fill=rgba(C["bg"]))
    draw.rounded_rectangle((v(size * 0.035), v(size * 0.035), s - v(size * 0.035), s - v(size * 0.035)), radius=tile_radius, outline=rgba(C["dark_gold"]), width=max(1, v(size * 0.014)))

    glow = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.ellipse((v(size * 0.20), v(size * 0.20), v(size * 0.80), v(size * 0.80)), fill=rgba(C["gold"], 44))
    glow = glow.filter(ImageFilter.GaussianBlur(v(size * 0.055)))
    canvas.alpha_composite(glow)
    draw = ImageDraw.Draw(canvas)

    disc_box = (v(size * 0.19), v(size * 0.19), v(size * 0.81), v(size * 0.81))
    draw.ellipse(disc_box, fill=rgba(C["black"]), outline=rgba(C["gold"]), width=max(1, v(size * 0.018)))
    draw.ellipse((v(size * 0.34), v(size * 0.34), v(size * 0.66), v(size * 0.66)), outline=rgba(C["panel"]), width=max(1, v(size * 0.014)))
    draw.ellipse((v(size * 0.445), v(size * 0.445), v(size * 0.555), v(size * 0.555)), fill=rgba(C["panel"]), outline=rgba(C["gold"]), width=max(1, v(size * 0.009)))

    wave_glow = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    wgd = ImageDraw.Draw(wave_glow)
    wave = [
        (v(size * 0.27), v(size * 0.64)),
        (v(size * 0.35), v(size * 0.64)),
        (v(size * 0.40), v(size * 0.58)),
        (v(size * 0.46), v(size * 0.70)),
        (v(size * 0.53), v(size * 0.57)),
        (v(size * 0.61), v(size * 0.66)),
        (v(size * 0.72), v(size * 0.63)),
    ]
    draw_wave(wgd, wave, C["cyan"], max(2, v(size * 0.025)))
    wave_glow = wave_glow.filter(ImageFilter.GaussianBlur(max(1, v(size * 0.018))))
    canvas.alpha_composite(wave_glow)
    draw = ImageDraw.Draw(canvas)
    draw_wave(draw, wave, C["cyan"], max(1, v(size * 0.018)))

    return canvas.resize((size, size), Image.Resampling.LANCZOS)

def draw_header_mark():
    size = 108
    scale = 3
    canvas = Image.new("RGBA", (size * scale, size * scale), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)

    def v(n):
        return int(round(n * scale))

    draw.rounded_rectangle((0, 0, v(size) - 1, v(size) - 1), radius=v(24), fill=rgba(C["gold"]))
    draw.rounded_rectangle((v(7), v(7), v(size - 7), v(size - 7)), radius=v(19), fill=rgba(C["bg"]))
    draw.ellipse((v(20), v(18), v(88), v(86)), outline=rgba(C["dark_gold"]), width=v(2))

    mb_font = font(v(30), bold=True)
    text = "MB"
    try:
        bbox = draw.textbbox((0, 0), text, font=mb_font)
        tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
    except Exception:
        tw, th = v(44), v(28)
    draw.text(((v(size) - tw) / 2, v(30)), text, font=mb_font, fill=rgba(C["gold"]))

    wave = [(v(24), v(76)), (v(35), v(70)), (v(46), v(75)), (v(56), v(62)), (v(68), v(74)), (v(82), v(68))]
    draw_wave(draw, wave, C["cyan"], v(3))
    return canvas.resize((size, size), Image.Resampling.LANCZOS)

def draw_primary_logo():
    w, h = 900, 260
    canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)
    mark = draw_header_mark().resize((170, 170), Image.Resampling.LANCZOS)
    canvas.alpha_composite(mark, (38, 42))

    title_font = font(76, bold=True)
    body_font = font(26, bold=False)
    draw.text((250, 56), "MusicBakh", font=title_font, fill=rgba(C["text"]))
    draw.text((255, 140), "premium music library", font=body_font, fill=rgba(C["muted"]))
    draw_wave(draw, [(255, 195), (320, 195), (355, 172), (390, 218), (440, 176), (495, 205), (570, 194)], C["gold"], 7)
    return canvas

app_icon_svg = f'''<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <rect x="0" y="0" width="256" height="256" rx="52" fill="{C["bg"]}"/>
  <rect x="8" y="8" width="240" height="240" rx="46" fill="none" stroke="{C["dark_gold"]}" stroke-width="4"/>
  <circle cx="128" cy="128" r="78" fill="{C["black"]}" stroke="{C["gold"]}" stroke-width="5"/>
  <circle cx="128" cy="128" r="38" fill="none" stroke="{C["panel"]}" stroke-width="4"/>
  <circle cx="128" cy="128" r="14" fill="{C["panel"]}" stroke="{C["gold"]}" stroke-width="2"/>
  <path d="M70 164 L90 164 L103 148 L118 180 L136 146 L156 169 L184 160" fill="none" stroke="{C["cyan"]}" stroke-width="7" stroke-linecap="round" stroke-linejoin="round"/>
</svg>
'''

primary_logo_svg = f'''<svg xmlns="http://www.w3.org/2000/svg" width="900" height="260" viewBox="0 0 900 260">
  <rect x="38" y="42" width="170" height="170" rx="38" fill="{C["gold"]}"/>
  <rect x="50" y="54" width="146" height="146" rx="31" fill="{C["bg"]}"/>
  <circle cx="123" cy="123" r="56" fill="none" stroke="{C["dark_gold"]}" stroke-width="3"/>
  <text x="123" y="123" text-anchor="middle" dominant-baseline="middle" font-family="Georgia, serif" font-size="50" font-weight="700" fill="{C["gold"]}">MB</text>
  <path d="M78 162 L100 150 L122 160 L142 132 L166 158 L190 146" fill="none" stroke="{C["cyan"]}" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"/>
  <text x="250" y="112" font-family="Georgia, Inter, sans-serif" font-size="76" font-weight="700" fill="{C["text"]}">MusicBakh</text>
  <text x="255" y="166" font-family="Inter, Segoe UI, sans-serif" font-size="26" fill="{C["muted"]}">premium music library</text>
  <path d="M255 195 L320 195 L355 172 L390 218 L440 176 L495 205 L570 194" fill="none" stroke="{C["gold"]}" stroke-width="7" stroke-linecap="round" stroke-linejoin="round"/>
</svg>
'''

(docs_dir / "musicbakh-app-icon.svg").write_text(app_icon_svg, encoding="utf-8")
(docs_dir / "musicbakh-primary-logo.svg").write_text(primary_logo_svg, encoding="utf-8")

icon_256 = draw_icon(256)
icon_256.save(docs_dir / "musicbakh-app-icon-256.png")

primary_logo = draw_primary_logo()
primary_logo.save(docs_dir / "musicbakh-primary-logo.png")

header_mark = draw_header_mark()
header_mark.save(runtime_dir / "musicbakh-logo.png")

# Pack all required Windows icon sizes into one .ico so Windows does not downsample from a single large bitmap.
ico_path = docs_dir / "musicbakh.ico"
icon_256.save(ico_path, format="ICO", sizes=[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)])
shutil.copyfile(ico_path, runtime_dir / "musicbakh.ico")

with Image.open(ico_path) as ico:
    sizes = sorted(ico.ico.sizes(), reverse=True)

print("Generated MusicBakh assets")
print("ICO sizes:", sizes)
print("Docs:", docs_dir)
print("Runtime:", runtime_dir)
'@ | Set-Content -LiteralPath $pythonScript -Encoding UTF8

try {
    & $pythonCommand.Source $pythonScript $repoRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Asset generation failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item -LiteralPath $pythonScript -Force -ErrorAction SilentlyContinue
}
```

- [ ] **Step 3: Run the generator**

Run:

```powershell
.\docs\assets\musicbakh\generate_musicbakh_assets.ps1
```

Expected output includes:

```text
Generated MusicBakh assets
ICO sizes: [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)]
```

- [ ] **Step 4: Verify generated file list**

Run:

```powershell
Get-ChildItem .\docs\assets\musicbakh, .\MusicLibrary\Assets\Brand | Select-Object FullName, Length
```

Expected list includes:

```text
docs\assets\musicbakh\generate_musicbakh_assets.ps1
docs\assets\musicbakh\musicbakh-app-icon.svg
docs\assets\musicbakh\musicbakh-primary-logo.svg
docs\assets\musicbakh\musicbakh-app-icon-256.png
docs\assets\musicbakh\musicbakh-primary-logo.png
docs\assets\musicbakh\musicbakh.ico
MusicLibrary\Assets\Brand\musicbakh.ico
MusicLibrary\Assets\Brand\musicbakh-logo.png
```

- [ ] **Step 5: Verify `.ico` sizes programmatically**

Run:

```powershell
python -c "from PIL import Image; im=Image.open(r'docs/assets/musicbakh/musicbakh.ico'); print(sorted(im.ico.sizes(), reverse=True))"
```

Expected:

```text
[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)]
```

- [ ] **Step 6: Commit generated assets**

Run:

```powershell
git add .\docs\assets\musicbakh .\MusicLibrary\Assets\Brand
git commit -m "feat: add MusicBakh brand assets"
```

Expected:

```text
[codex/wpf-music-library <commit>] feat: add MusicBakh brand assets
```

## Task 2: Wire Assets Into WPF

**Files:**

- Modify: `MusicLibrary/MusicLibrary.csproj`
- Modify: `MusicLibrary/MainWindow.xaml`

- [ ] **Step 1: Add executable icon configuration to the project**

In `MusicLibrary/MusicLibrary.csproj`, replace the first `<PropertyGroup>` with this full block:

```xml
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <LangVersion>14.0</LangVersion>
    <!-- Controls the executable and taskbar icon for Windows builds. -->
    <ApplicationIcon>Assets\Brand\musicbakh.ico</ApplicationIcon>
  </PropertyGroup>
```

- [ ] **Step 2: Add WPF resource items**

In `MusicLibrary/MusicLibrary.csproj`, add this `ItemGroup` after the existing `Music/Covers` `ItemGroup` and before the `PackageReference` `ItemGroup`:

```xml
  <ItemGroup>
    <None Remove="Assets\Brand\musicbakh.ico" />
    <None Remove="Assets\Brand\musicbakh-logo.png" />
    <Resource Include="Assets\Brand\musicbakh.ico" />
    <Resource Include="Assets\Brand\musicbakh-logo.png" />
  </ItemGroup>
```

The resulting file structure must be:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <LangVersion>14.0</LangVersion>
    <!-- Controls the executable and taskbar icon for Windows builds. -->
    <ApplicationIcon>Assets\Brand\musicbakh.ico</ApplicationIcon>
  </PropertyGroup>

  <ItemGroup>
    <None Update="Music\**\*" CopyToOutputDirectory="PreserveNewest" />
    <None Update="Covers\**\*" CopyToOutputDirectory="PreserveNewest" />
    <None Update="Music\.gitkeep;Covers\.gitkeep" CopyToOutputDirectory="Never" />
  </ItemGroup>

  <ItemGroup>
    <None Remove="Assets\Brand\musicbakh.ico" />
    <None Remove="Assets\Brand\musicbakh-logo.png" />
    <Resource Include="Assets\Brand\musicbakh.ico" />
    <Resource Include="Assets\Brand\musicbakh-logo.png" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="TagLibSharp" Version="2.3.0" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Set the WPF window icon**

In `MusicLibrary/MainWindow.xaml`, update the opening `<Window>` tag by adding `Icon="Assets/Brand/musicbakh.ico"` immediately after `Title="MusicBakh"`:

```xml
<Window x:Class="MusicLibrary.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="MusicBakh"
        Icon="Assets/Brand/musicbakh.ico"
        Width="1180"
        Height="760"
        MinWidth="980"
        MinHeight="640"
        FontFamily="{StaticResource BodyFont}"
        Background="{StaticResource BackgroundBrush}">
```

- [ ] **Step 4: Replace the temporary inline header mark**

In `MusicLibrary/MainWindow.xaml`, find this current header mark:

```xml
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
```

Replace it with:

```xml
<Image Width="54"
       Height="54"
       Source="Assets/Brand/musicbakh-logo.png"
       Stretch="Uniform"
       RenderOptions.BitmapScalingMode="HighQuality" />
```

Do not change the adjacent `MusicBakh` title or `Музыкальная библиотека` subtitle.

- [ ] **Step 5: Confirm temporary header elements are gone**

Run:

```powershell
rg -n "Text=\"MB\"|Polyline Points=\"12,38|GoldGradientBrush" .\MusicLibrary\MainWindow.xaml
```

Expected:

```text
No output.
```

If output remains only from unrelated code, inspect it before proceeding. There should be no inline header `MB` or polyline wave in `MainWindow.xaml`.

- [ ] **Step 6: Build to verify XAML/project resources**

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

- [ ] **Step 7: Commit WPF wiring**

Run:

```powershell
git add .\MusicLibrary\MusicLibrary.csproj .\MusicLibrary\MainWindow.xaml
git commit -m "feat: wire MusicBakh app icon"
```

Expected:

```text
[codex/wpf-music-library <commit>] feat: wire MusicBakh app icon
```

## Task 3: Release Verification

**Files:**

- Verify all changed files.

- [ ] **Step 1: Run full automated verification**

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

- [ ] **Step 2: Verify release executable starts**

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

- [ ] **Step 3: Verify release executable icon is embedded**

Run:

```powershell
$exe = Join-Path (Get-Location) 'MusicLibrary\bin\Release\net10.0-windows\MusicLibrary.exe'
[System.Reflection.Assembly]::LoadWithPartialName('System.Drawing') | Out-Null
$icon = [System.Drawing.Icon]::ExtractAssociatedIcon($exe)
[pscustomobject]@{
    HasIcon = $null -ne $icon
    Width = if ($icon) { $icon.Width } else { 0 }
    Height = if ($icon) { $icon.Height } else { 0 }
} | Format-List
if ($icon) { $icon.Dispose() }
```

Expected:

```text
HasIcon : True
Width   : 32
Height  : 32
```

Windows may report `32x32` for the extracted associated icon even when the `.ico` contains larger sizes. The multi-size source `.ico` was already verified in Task 1.

- [ ] **Step 4: Run manual visual verification**

Run:

```powershell
dotnet run --project .\MusicLibrary\MusicLibrary.csproj
```

Manually verify:

```text
1. Title bar icon is MusicBakh icon, not default WPF icon.
2. Taskbar icon is MusicBakh icon while the app is running.
3. Header logo appears aligned with the MusicBakh title.
4. Header layout remains stable at 1180x760.
5. Header layout remains stable at minimum window size.
6. The logo does not blur noticeably at 54x54.
7. The icon reads as dark/gold premium audio with cyan only as a small signal accent.
```

Close the app after verification.

- [ ] **Step 5: Review git status and changed files**

Run:

```powershell
git status --short
git diff --stat HEAD~2..HEAD -- MusicLibrary docs/assets
```

Expected `git status --short`:

```text
?? .claude/
```

Expected changed file list includes:

```text
MusicLibrary/MusicLibrary.csproj
MusicLibrary/MainWindow.xaml
MusicLibrary/Assets/Brand/musicbakh.ico
MusicLibrary/Assets/Brand/musicbakh-logo.png
docs/assets/musicbakh/generate_musicbakh_assets.ps1
docs/assets/musicbakh/musicbakh-app-icon.svg
docs/assets/musicbakh/musicbakh-primary-logo.svg
docs/assets/musicbakh/musicbakh-app-icon-256.png
docs/assets/musicbakh/musicbakh-primary-logo.png
docs/assets/musicbakh/musicbakh.ico
```

- [ ] **Step 6: Commit final visual corrections only if manual smoke required them**

If manual visual verification surfaces a concrete sizing/alignment problem, apply the smallest XAML or asset-generator correction, rerun:

```powershell
.\docs\assets\musicbakh\generate_musicbakh_assets.ps1
dotnet build .\MusicLibrary.sln --no-restore
dotnet test .\MusicLibrary.sln --no-restore
dotnet build .\MusicLibrary.sln -c Release --no-restore
```

Then commit:

```powershell
git add .\docs\assets\musicbakh .\MusicLibrary\Assets\Brand .\MusicLibrary\MusicLibrary.csproj .\MusicLibrary\MainWindow.xaml
git commit -m "fix: finalize MusicBakh icon polish"
```

If no correction is needed, run:

```powershell
git status --short
```

Expected:

```text
?? .claude/
```

## Self-Review

- Spec coverage: asset deliverables are covered by Task 1; WPF `ApplicationIcon`, `Window.Icon`, and header logo are covered by Task 2; automated/startup/manual verification is covered by Task 3.
- Scope check: installer, README, repository cleanup, playback behavior, and `work_diff.md` are excluded as required.
- Failure patterns: the plan verifies multi-size `.ico`, avoids text inside the app icon, keeps cyan as a small accent, sets both project and window icon, uses ASCII paths, and treats Figma as reference only.
- Placeholder scan: no deferred implementation markers are used.
- Type/path consistency: `musicbakh.ico`, `musicbakh-logo.png`, `docs/assets/musicbakh`, `MusicLibrary/Assets/Brand`, `ApplicationIcon`, and `Icon="Assets/Brand/musicbakh.ico"` are used consistently.
