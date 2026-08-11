# VideoGameFileSystemParser — Wiki

> **A cross-platform .NET library for parsing video game console disc image file systems.**
>
> Supports **CHD**, **ISO**, and **raw sector** images across **31+ console formats** — PlayStation, Xbox, Dreamcast, CD-i, 3DO, Pippin (HFS/HFS+), PC Engine CD, PC-FX, and more — through a single high-level API.

| | |
|---|---|
| **NuGet** | `VideoGameFileSystemParser` — [nuget.org/packages/VideoGameFileSystemParser](https://www.nuget.org/packages/VideoGameFileSystemParser) |
| **Version** | 1.2.0 |
| **Targets** | .NET 8.0 · .NET 9.0 · .NET 10.0 |
| **License** | MIT |
| **Source** | [github.com/drpetersonfernandes/VideoGameFileSystemParser](https://github.com/drpetersonfernandes/VideoGameFileSystemParser) |
| **Dependency** | [CHDSharp](https://www.nuget.org/packages/CHDSharp) 1.2.0 |

---

## What is it?

`VideoGameFileSystemParser` opens console disc images (primarily **CHD**, with ISO and raw-sector support) and exposes the disc's file system as a browsable, readable virtual tree — no matter whether the disc uses ISO 9660, UDF, XDVDFS, Opera FS, CD-i Green Book, HFS/HFS+, or a non-standard layout.

It also ships **virtual export** modes that present a CHD as a CUE sheet with BIN/ISO/WAV files, so emulators and tools can consume the image without converting it to disk first.

## Feature highlights

- **One API for every format** — `ChdContainer` + `ConsoleType` covers all supported systems.
- **CHD V1–V5** via CHDSharp, plus ISO/raw image support through the sector reader.
- **8 virtual export modes** — CUE/BIN, CUE/ISO, CUE/BIN/WAV, CUE/ISO/WAV, each in 2352-byte and 2048-byte variants.
- **Raw passthrough** — expose an entire disc as a single `image.iso` file.
- **Auto-detection** — `ConsoleType.PlayStation` and signature-based track selection (Dreamcast IP.BIN).
- **`ConsoleTypeRegistry`** — display names and CLI aliases, the single source of truth for host applications.
- **Non-standard system handling** — PC Engine CD and PC-FX boot-signature scanning, tolerant ISO 9660, raw `TRACKnn.iso` fallbacks.
- **CD-ROM XA** interleaved data support (PlayStation).
- **Async disposal** (`IAsyncDisposable`).

## Documentation map

| Page | Contents |
|---|---|
| [Getting Started](Getting-Started.md) | Installation, first project, quick start |
| [Usage Guide](Usage-Guide.md) | Deep dive: opening, mounting, reading, errors, raw mode |
| [Supported Consoles](Supported-Consoles.md) | Full console reference, parser chains, system observations |
| [Virtual CUE/BIN/ISO/WAV Exports](Virtual-Exports.md) | Export modes, generated files, sector-size rules |
| [API Reference](API-Reference.md) | Complete public API with signatures |
| [Architecture](Architecture.md) | Internal design: readers, parsers, pipelines |
| [Migration Guide](Migration-Guide.md) | Upgrading between versions, breaking changes |
| [FAQ](FAQ.md) | Frequently asked questions |
| [Contributing](Contributing.md) | Build, test, and contribution guidelines |

## See it in action — CHDMounter

This library is **battle-tested in production** by [**CHDMounter**](https://github.com/drpetersonfernandes/CHDMounter) — a Windows application that mounts CHD disc images as virtual drives, powered by this parsing engine.

- **Unit tests:** the library's test suite lives in the CHDMounter repository ([`CHDMounter.Core.Tests`](https://github.com/drpetersonfernandes/CHDMounter/tree/master/CHDMounter.Core.Tests)) — parser tests, per-console integration tests, `ConsoleTypeRegistry` tests, and more.
- **Try it yourself:** build CHDMounter, mount any CHD, and browse the parsed file system as a real drive — no code required.

## Release history

- **v1.2.0** — Sector-size-aware generic formats; `ConsoleTypeRegistry`; CUE export fixes; code modernization; professional multi-page documentation. **Breaking.**
- **v1.1.0** — Virtual CUE/BIN/ISO/WAV export fixes (pregap shift, missing ISO audio, truncated tracks).
- **v1.0.0** — Initial release, 31 console formats.

Full details: [WhatsNew.md](../WhatsNew.md) (repo root).

## Quick example

```csharp
using VideoGameFileSystemParser.Models;
using VideoGameFileSystemParser.Parsers;

using var container = new ChdContainer(@"C:\ROMs\game.chd");

if (container.MountAndParse(ConsoleType.Ps2))
{
    Console.WriteLine($"Volume: {container.VolumeName} ({container.VolumeSize:N0} bytes)");

    foreach (var entry in container.Entries)
    {
        Console.WriteLine($"  {(entry.IsDirectory ? "[DIR] " : "[FILE]")} {entry.FullPath} ({entry.Size:N0} bytes)");
    }
}
```

---

*Documentation wiki for the VideoGameFileSystemParser library. Maintained as part of the repository — see [Contributing](Contributing.md).*
