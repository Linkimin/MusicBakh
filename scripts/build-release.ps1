# Сборка релиза MusicBakh: single-file self-contained win-x64 + установщик Inno Setup.
# Использование: pwsh scripts/build-release.ps1 -Version 1.0.0
[CmdletBinding()]
param(
    [string]$Version = "",
    [string]$Configuration = "Release",
    [switch]$PublishOnly,
    [string]$InnoSetupCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"
# Чтобы кириллический вывод не ломался в консолях, которые не дефолтятся в UTF-8.
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$Root = Split-Path $PSScriptRoot -Parent
$Csproj = Join-Path $Root "MusicLibrary\MusicLibrary.csproj"
$IssScript = Join-Path $Root "installer\MusicBakh.iss"
$PublishDir = Join-Path $Root "publish\win-x64"
$ReleaseDir = Join-Path $Root "release"

if (-not (Test-Path $Csproj)) {
    throw "Не найден $Csproj"
}
if (-not (Test-Path $IssScript)) {
    throw "Не найден $IssScript"
}

Write-Host "==> Очистка publish/" -ForegroundColor Cyan
if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$projectXml = Get-Content -LiteralPath $Csproj -Raw
    $Version = [string]($projectXml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Версия должна иметь формат major.minor.patch: $Version" }
$resolvedPublishDir = [IO.Path]::GetFullPath($PublishDir)
$expectedPublishDir = [IO.Path]::GetFullPath((Join-Path $Root 'publish\win-x64'))
if ($resolvedPublishDir -ne $expectedPublishDir -or -not $resolvedPublishDir.StartsWith([IO.Path]::GetFullPath($Root) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Недопустимый каталог очистки: $resolvedPublishDir"
}
if (Test-Path $PublishDir) {
    if ((Get-Item -LiteralPath $PublishDir).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Каталог publish не должен быть ссылкой.' }
    Remove-Item -LiteralPath $resolvedPublishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $ReleaseDir -Force | Out-Null

Write-Host "==> dotnet publish (single-file, self-contained, win-x64)" -ForegroundColor Cyan
& dotnet publish $Csproj `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=embedded `
    -p:PublishReadyToRun=false `
    -p:SatelliteResourceLanguages="en%3Bru" `
    -p:Version=$Version `
    -p:AssemblyVersion="$Version.0" `
    -p:FileVersion="$Version.0" `
    -o $PublishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish завершился с кодом $LASTEXITCODE"
}

$ExePath = Join-Path $PublishDir "MusicBakh.exe"
if (-not (Test-Path $ExePath)) {
    throw "Не найден $ExePath после publish"
}
$ExeSizeMB = [math]::Round((Get-Item $ExePath).Length / 1MB, 1)
Write-Host ("    MusicBakh.exe = {0} MB" -f $ExeSizeMB) -ForegroundColor Green
foreach ($assetDirectory in @('Music','Covers')) {
    if (-not (Get-ChildItem -LiteralPath (Join-Path $PublishDir $assetDirectory) -File -ErrorAction SilentlyContinue)) {
        throw "В публикации отсутствуют ресурсы $assetDirectory"
    }
}
if ($PublishOnly) { return }

Write-Host "==> Inno Setup" -ForegroundColor Cyan
if (-not (Test-Path $InnoSetupCompiler)) {
    throw @"
Не найден компилятор Inno Setup: $InnoSetupCompiler
Установите Inno Setup 6 (https://jrsoftware.org/isinfo.php) или передайте путь параметром -InnoSetupCompiler.
"@
}

& $InnoSetupCompiler "/DMyAppVersion=$Version" $IssScript
if ($LASTEXITCODE -ne 0) {
    throw "ISCC завершился с кодом $LASTEXITCODE"
}

$SetupPath = Join-Path $ReleaseDir "MusicBakh-Setup-$Version.exe"
if (-not (Test-Path $SetupPath)) {
    throw "Inno Setup отработал, но $SetupPath не появился"
}
$SetupSizeMB = [math]::Round((Get-Item $SetupPath).Length / 1MB, 1)
Write-Host ("==> Готово: {0} ({1} MB)" -f $SetupPath, $SetupSizeMB) -ForegroundColor Green
