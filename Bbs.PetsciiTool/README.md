# Bbs.PetsciiTool

CLI tool for converting images (`jpg/png/gif`) into PETSCII output formats.

Polish version: see `README.pl.md`.

## Output formats

- `bbs` -> `*_bbs.seq` (BBS stream)
- `image` -> `*_petscii.png` (PETSCII preview)
- `basic` -> `*.bas`
- `bin` -> `*_screen.seq`, `*_color.seq`, `*_bgcolor.seq`

## Quick start (single file)

From repo root:

```powershell
dotnet run --project .\Bbs.PetsciiTool -- `
  ".\Bbs.PetsciiTool\examples\8-bitz.jpg" `
  "/target=.\Bbs.PetsciiTool\examples\output" `
  "/format=bbs,image"
```

Optional preprocessing:

- `"/contrast=20"` increases contrast before PETSCII conversion
- `"/precolors=32"` reduces source image to 32 colors before conversion
- `"/c64x2colors"` shortcut for `precolors=32`
- `"/dither"` enables Floyd-Steinberg dithering (best with `precolors`)

## Batch conversion (PowerShell)

The batch script is located here:

- `Convert-Batch.ps1`

By default it converts all images from `.\examples` to `.\examples\output`.

### Example 1 (defaults)

```powershell
.\Bbs.PetsciiTool\Convert-Batch.ps1
```

### Example 2 (custom folders)

```powershell
.\Bbs.PetsciiTool\Convert-Batch.ps1 `
  -InputDir ".\examples" `
  -OutputDir ".\examples\output" `
  -Formats "bbs,image,basic"
```

### Example 3 (recursive + clean output)

```powershell
.\Bbs.PetsciiTool\Convert-Batch.ps1 `
  -InputDir ".\examples" `
  -OutputDir ".\examples\output" `
  -Recurse `
  -CleanOutput
```

### Example 4 (contrast + 2x C64 colors)

```powershell
.\Bbs.PetsciiTool\Convert-Batch.ps1 `
  -InputDir ".\examples" `
  -OutputDir ".\examples\output" `
  -Contrast 20 `
  -C64x2Colors
```

### Example 5 (precolors + dithering)

```powershell
.\Bbs.PetsciiTool\Convert-Batch.ps1 `
  -InputDir ".\examples" `
  -OutputDir ".\examples\output" `
  -PreColors 32 `
  -Dither
```

## `Convert-Batch.ps1` parameters

- `-InputDir` input directory (default: `.\examples`)
- `-OutputDir` output directory (default: `.\examples\output`)
- `-Formats` formats passed to the tool (default: `bbs,image`)
- `-Contrast` optional contrast percent before conversion (default: `0`)
- `-PreColors` optional pre-reduced color count before conversion (default: `0` = off)
- `-C64x2Colors` shortcut for pre-reduction to 32 colors
- `-Dither` enable Floyd-Steinberg dithering
- `-Recurse` scan subdirectories
- `-CleanOutput` remove output directory before conversion
