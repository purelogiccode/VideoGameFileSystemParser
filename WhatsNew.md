# What's New in VideoGameFileSystemParser

This file tracks the highlights of each release. The latest version is **v1.2.0**.

---

## v1.2.0 — Sector-size-aware generic formats, ConsoleTypeRegistry & documentation

**Release date:** 2026

### New features

- **Sector-size-aware generic formats.** The generic raw / virtual-export console types are now split by sector size so each image type is unambiguous:
  - `GenericIsoRaw` → `GenericIsoRaw2352` and `GenericIsoRaw2048`
  - `GenericCueIso` → `GenericCueIso2352` and `GenericCueIso2048`
  - `GenericCueBinWav` → `GenericCueBinWav2352` and `GenericCueBinWav2048`
  - `GenericCueIsoWav` → `GenericCueIsoWav2352` and `GenericCueIsoWav2048`
  - `GenericCueBin2352Default` → renamed to `GenericCueBin2352`
- **New `ConsoleTypeRegistry`** (`VideoGameFileSystemParser.Models`) — the single source of truth for console type display names and CLI aliases:
  - `All` — ordered list of `ConsoleTypeInfo` entries (type, display name, aliases)
  - `Parse(alias)` — case-insensitive alias resolution (e.g. `"ps2"`, `"cuebin2352"`)
  - `GetDisplayName(type)` / `GetAliases(type)`
  - Multiple display entries can resolve to the same `ConsoleType` (e.g. "PS3 ISO RAW 2352", "Xbox ISO RAW 2352" and "ISO RAW 2352" all map to `GenericIsoRaw2352`)
- **`ParserFactory.GetAllSupportedConsoles()` is now registry-driven** — it returns the same list used by the UI/CLI, so the two can never drift apart.
- **Code modernization.** Consistent use of `var` across all parsers (HFS parser included). No API or behavior changes — this is a source-only cleanup.
- **Professional multi-page documentation.** The repository now ships a complete wiki-style documentation set under [`docs/`](docs/):
  - [Home](docs/Home.md), [Getting Started](docs/Getting-Started.md), [Usage Guide](docs/Usage-Guide.md)
  - [Supported Consoles](docs/Supported-Consoles.md), [API Reference](docs/API-Reference.md)
  - [Virtual CUE/BIN/ISO/WAV Exports](docs/Virtual-Exports.md), [Architecture](docs/Architecture.md)
  - [Migration Guide](docs/Migration-Guide.md), [FAQ](docs/FAQ.md), [Contributing](docs/Contributing.md)

### Fixes

- **Virtual CUE export for 2048-cooked variants:** audio tracks inside BIN files now always use 2352-byte sectors (previously they were written at the cooked 2048-byte size, producing unreadable audio tracks).
- **CUE track mode strings** now reflect the actual sector size used per track (e.g. `MODE1/2352`, `MODE2/2048`) instead of hard-coding 2048 for ISO exports.

### Breaking changes

- Removed: `ConsoleType.GenericIsoRaw`, `ConsoleType.GenericCueIso`, `ConsoleType.GenericCueBinWav`, `ConsoleType.GenericCueIsoWav`, `ConsoleType.GenericCueBin2352Default`.
  Replace with the corresponding sector-size-aware values above.
- `ConsoleType` member order changed (registry-ordering); do **not** rely on numeric enum values — resolve types through `ConsoleTypeRegistry` instead.

### Upgrade guide

| v1.1.0 | v1.2.0 |
|--------|--------|
| `ConsoleType.GenericIsoRaw` | `ConsoleType.GenericIsoRaw2352` (or `GenericIsoRaw2048`) |
| `ConsoleType.GenericCueBin2352Default` | `ConsoleType.GenericCueBin2352` |
| `ConsoleType.GenericCueIso` | `ConsoleType.GenericCueIso2352` or `GenericCueIso2048` |
| `ConsoleType.GenericCueBinWav` | `ConsoleType.GenericCueBinWav2352` or `GenericCueBinWav2048` |
| `ConsoleType.GenericCueIsoWav` | `ConsoleType.GenericCueIsoWav2352` or `GenericCueIsoWav2048` |

---

## v1.1.0 — Virtual CUE/BIN/ISO/WAV export fixes

**Release date:** 2026

### Fixes

- Fixed pregap shift in virtual CUE/BIN/ISO/WAV export — tracks started 2 seconds late, bled into each other, and the last track was unreadable.
- Fixed missing ISO audio in CUE/ISO exports.
- Fixed truncated tracks and incorrect BIN/ISO/WAV sizes.
- WAV exports now skip the stored pregap silence and start at `INDEX 01 00:00:00`.

---

## v1.0.0 — Initial release

**Release date:** 2026

### Features

- Initial release with support for **31 console formats**:
  - 3DO, Amiga CD / CD32 / CDTV, CD-i, Dreamcast, FM Towns, NeoGeo CD, Nuon, PC Engine CD, PC-FX, PC-98, Pico, Pippin (HFS/HFS+), PlayStation (auto), PS1, PS2, PS3, PSP, Saturn, Genesis CD, X68000, Xbox, Xbox 360, Generic ISO 9660
  - Generic raw sector passthrough and virtual CUE/BIN, CUE/ISO, CUE/BIN/WAV, CUE/ISO/WAV exports
- CHD (V1–V5), ISO, and raw sector image support via `ChdContainer` + `SectorReader`.
- File system parsers: ISO 9660 (with CD-ROM XA), UDF, XDVDFS, Opera FS, CD-i Green Book, HFS/HFS+, PC Engine CD, PC-FX.
- `ParserFactory` with `CreateParser` and `GetAllSupportedConsoles`.
