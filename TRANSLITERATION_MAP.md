# PETSCII Transliteration Map

This document describes how text is normalized for PETSCII output in the BBS.

Implementation location:
- `Bbs.Core/BbsThread.cs`
- method: `TransliterateForPetscii(...)`

## How Transliteration Works

1. Explicit replacements are applied first (letters that do not decompose cleanly, plus some typography).
2. Text is normalized to Unicode `FormD`.
3. Combining diacritical marks are removed.
4. Text is normalized back to `FormC`.

## Explicit Character Mappings

### Letters and ligatures

| Source | Target |
|---|---|
| `ß` | `ss` |
| `Æ` | `AE` |
| `æ` | `ae` |
| `Œ` | `OE` |
| `œ` | `oe` |
| `Ø` | `O` |
| `ø` | `o` |
| `Ł` | `L` |
| `ł` | `l` |
| `Đ` | `D` |
| `đ` | `d` |
| `Þ` | `Th` |
| `þ` | `th` |
| `Ð` | `D` |
| `ð` | `d` |
| `Ħ` | `H` |
| `ħ` | `h` |
| `ı` | `i` |
| `Ŋ` | `N` |
| `ŋ` | `n` |

### Typography and spacing

| Source | Target |
|---|---|
| `‘` | `'` |
| `’` | `'` |
| `‚` | `'` |
| `“` | `"` |
| `”` | `"` |
| `„` | `"` |
| `–` | `-` |
| `—` | `-` |
| `−` | `-` |
| `…` | `...` |
| `NBSP` (`U+00A0`) | regular space |

## General Diacritic Removal (all languages)

After explicit mappings, any character with combining diacritics is simplified to its base letter.

Examples:
- `á à â ä ã å` -> `a`
- `ç` -> `c`
- `é è ê ë` -> `e`
- `í ì î ï` -> `i`
- `ñ` -> `n`
- `ó ò ô ö õ` -> `o`
- `ú ù û ü` -> `u`
- `ý ÿ` -> `y`
- `ž` -> `z`

## Scope

The transliteration is applied in `BbsThread.Print(...)` and `BbsThread.Println(...)` for terminal type `petscii`.

Binary PETSCII payloads sent via `Write(byte[])` (for example gallery image data) are not transliterated.
