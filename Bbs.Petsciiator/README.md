# Bbs.Petsciiator

`Bbs.Petsciiator` is a reusable PETSCII image converter library.

## Features

- Convert from `byte[]`, `Stream`, or image `URL`
- Supports PNG, JPEG, and GIF input (decoded by ImageSharp)
- Outputs PETSCII byte sequences ready for terminal rendering

## Basic Usage

```csharp
using Bbs.Petsciiator;

using var converter = new PetsciiatorConverter();
byte[] output = await converter.ConvertAsync(imageBytes);
```

## URL Usage

```csharp
using Bbs.Petsciiator;

using var converter = new PetsciiatorConverter();
byte[] output = await converter.ConvertFromUrlAsync("https://example.com/image.png");
```
