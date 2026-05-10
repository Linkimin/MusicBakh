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
