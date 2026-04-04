param(
    [string]$InputDir = ".\examples",
    [string]$OutputDir = ".\examples\output",
    [string]$Formats = "bbs,image",
    [double]$Contrast = 0,
    [int]$PreColors = 0,
    [switch]$C64x2Colors,
    [switch]$Dither,
    [switch]$Recurse,
    [switch]$CleanOutput
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $projectDir
$toolProject = Join-Path $projectDir "Bbs.PetsciiTool.csproj"
$inputPath = Join-Path $projectDir $InputDir
$outputPath = Join-Path $projectDir $OutputDir

if (-not (Test-Path -LiteralPath $toolProject)) {
    throw "Nie znaleziono projektu: $toolProject"
}

if (-not (Test-Path -LiteralPath $inputPath)) {
    throw "Katalog wejsciowy nie istnieje: $inputPath"
}

if ($CleanOutput -and (Test-Path -LiteralPath $outputPath)) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

$searchScope = if ($Recurse) { "-Recurse" } else { "" }
$files = if ($Recurse) {
    Get-ChildItem -LiteralPath $inputPath -File -Recurse
} else {
    Get-ChildItem -LiteralPath $inputPath -File
}

$images = $files | Where-Object {
    $_.Extension -match '^\.(jpg|jpeg|png|gif)$' -and
    $_.Name -notlike '*_petscii.png' -and
    $_.Name -notlike '*_bbs.seq' -and
    $_.Name -notlike '*_screen.seq' -and
    $_.Name -notlike '*_color.seq' -and
    $_.Name -notlike '*_bgcolor.seq'
}

if (-not $images) {
    Write-Host "Brak plikow obrazow do konwersji w: $inputPath"
    exit 0
}

$env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet-home"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"

Write-Host "Konwersja $($images.Count) plik(ow) z '$inputPath' do '$outputPath'..."

foreach ($img in $images) {
    Write-Host "-> $($img.Name)"
    $args = @(
        "run", "--project", $toolProject, "--",
        $img.FullName,
        "/target=$outputPath",
        "/format=$Formats"
    )

    if ($Contrast -ne 0) {
        $contrastValue = $Contrast.ToString([System.Globalization.CultureInfo]::InvariantCulture)
        $args += "/contrast=$contrastValue"
    }

    if ($C64x2Colors) {
        $args += "/c64x2colors"
    }
    elseif ($PreColors -gt 0) {
        $args += "/precolors=$PreColors"
    }

    if ($Dither) {
        $args += "/dither"
    }

    dotnet @args
    if ($LASTEXITCODE -ne 0) {
        throw "Konwersja nieudana dla: $($img.FullName)"
    }
}

Write-Host "Gotowe. Wyniki zapisane w: $outputPath"
