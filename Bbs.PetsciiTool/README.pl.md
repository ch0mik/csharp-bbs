# Bbs.PetsciiTool

Narzędzie CLI do konwersji obrazów (`jpg/png/gif`) do formatów PETSCII.

Wersja angielska: zobacz `README.md`.

## Formaty wyjściowe

- `bbs` -> `*_bbs.seq` (strumień BBS)
- `image` -> `*_petscii.png` (podgląd PETSCII)
- `basic` -> `*.bas`
- `bin` -> `*_screen.seq`, `*_color.seq`, `*_bgcolor.seq`

## Szybki start (pojedynczy plik)

Z katalogu głównego repo:

```powershell
dotnet run --project .\Bbs.PetsciiTool -- `
  ".\Bbs.PetsciiTool\examples\8-bitz.jpg" `
  "/target=.\Bbs.PetsciiTool\examples\output" `
  "/format=bbs,image"
```

Opcjonalny preprocessing:

- `"/contrast=20"` zwieksza kontrast przed konwersja
- `"/precolors=32"` redukuje obraz zrodlowy do 32 kolorow przed konwersja
- `"/c64x2colors"` skrot dla `precolors=32`
- `"/dither"` wlacza dithering Floyd-Steinberg (najlepiej z `precolors`)

## Konwersja masowa (PowerShell)

Skrypt do konwersji masowej:

- `Convert-Batch.ps1`

Domyślnie konwertuje wszystkie obrazy z `.\examples` do `.\examples\output`.

### Przykład 1 (domyślnie)

```powershell
.\Bbs.PetsciiTool\Convert-Batch.ps1
```

### Przykład 2 (własne katalogi)

```powershell
.\Bbs.PetsciiTool\Convert-Batch.ps1 `
  -InputDir ".\examples" `
  -OutputDir ".\examples\output" `
  -Formats "bbs,image,basic"
```

### Przykład 3 (rekurencyjnie + czyszczenie output)

```powershell
.\Bbs.PetsciiTool\Convert-Batch.ps1 `
  -InputDir ".\examples" `
  -OutputDir ".\examples\output" `
  -Recurse `
  -CleanOutput
```

### Przyklad 4 (kontrast + 2x kolorow C64)

```powershell
.\Bbs.PetsciiTool\Convert-Batch.ps1 `
  -InputDir ".\examples" `
  -OutputDir ".\examples\output" `
  -Contrast 20 `
  -C64x2Colors
```

### Przyklad 5 (precolors + dithering)

```powershell
.\Bbs.PetsciiTool\Convert-Batch.ps1 `
  -InputDir ".\examples" `
  -OutputDir ".\examples\output" `
  -PreColors 32 `
  -Dither
```

## Parametry `Convert-Batch.ps1`

- `-InputDir` katalog wejściowy (domyślnie: `.\examples`)
- `-OutputDir` katalog wyjściowy (domyślnie: `.\examples\output`)
- `-Formats` formaty przekazywane do toola (domyślnie: `bbs,image`)
- `-Contrast` opcjonalny procent kontrastu przed konwersja (domyslnie: `0`)
- `-PreColors` opcjonalna liczba kolorow przed konwersja (domyslnie: `0` = wylaczone)
- `-C64x2Colors` skrot redukcji do 32 kolorow
- `-Dither` wlacza dithering Floyd-Steinberg
- `-Recurse` przeszukiwanie podkatalogów
- `-CleanOutput` usuwa katalog wyjściowy przed konwersją
