# Architecture

This page describes how the library is structured internally — the reader pipeline, parser design, and how a raw CHD becomes a browsable file tree.

---

## Layered overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        ChdContainer (public API)                 │
│   Open · MountAndParse · Entries · FindFile · ReadFile · Dispose │
└──────────────┬───────────────────────────────────────────────────┘
               │
        ┌──────▼───────┐        ┌──────────────────────────────┐
        │ ParserFactory │──────►│        IConsoleParser         │
        │ ConsoleType → │        │ Parse / ParseTrack / ForceMode│
        └──────┬───────┘        └──────────────┬───────────────┘
               │                               │
               │                    ┌──────────▼──────────┐
               │                    │   File system parsers │
               │                    │ ISO9660 · UDF · XDVDFS │
               │                    │ HFS · Opera · CD-i · …  │
               │                    └──────────┬──────────┘
               │                               │
        ┌──────▼───────────────────────────────▼───────┐
        │                 SectorReader                   │
        │  hunk cache · LBA mapping · byte-swap · unscramble│
        └──────────────────────┬─────────────────────────┘
                               │
        ┌──────────────────────▼─────────────────────────┐
        │              CHDSharp (ChdFile)                 │
        │          MAME CHD V1–V5 reader                  │
        └─────────────────────────────────────────────────┘
```

## Components

### 1. ChdContainer — the facade

`ChdContainer` owns the `ChdFile` handle and coordinates everything:

- **`Open(consoleType)`** — opens the CHD, builds the first `SectorReader`, and populates metadata (`UnitBytes`, `HunkBytes`, `VolumeSize`, `VolumeName`, `HasDataTracks`).
- **`MountAndParse(consoleType)`** — `Open` + dispatch:
  - `GenericCue*` → builds a **virtual export tree** directly (no parser).
  - `GenericIsoRaw2352/2048` → raw passthrough `image.iso`.
  - everything else → `ParserFactory.CreateParser` → `IConsoleParser.Parse` → `BuildFromFsNode`.
  - `PcEngineCd` / `PcFx` → also builds a virtual CUE/BIN export.
- **`BuildFromFsNode(FsNode)`** — flattens the parser's node tree into the `Entries` list of `FileEntry` with full paths.
- **`ReadFile(entry, offset, buffer, bufOffset, count)`** — resolves the entry's extents and streams bytes through the sector reader (or raw CHD reads for `IsRawPassthrough`).

### 2. SectorReader — the sector pipeline

`SectorReader` wraps a CHDSharp `ChdFile` and presents a uniform sector view:

- **Track table** — parses CHD track metadata (`TRACK:...` entries) into `TrackInfo` with start LBAs, types, pregaps/postgaps.
- **LBA mapping** — converts logical (disc) LBAs to CHD frame offsets, honoring per-track start offsets and the selected track (`SetTrack`).
- **Sector offset detection** — detects whether the dump carries 2352-byte raw sectors with a 16-byte header or cooked 2048-byte sectors; `SectorHeaderOffset` / `SyncOffset` record the result.
- **Byte-swapping** — audio track payloads are byte-swapped to little-endian PCM.
- **Descrambling** — applies the standard CD sector scramble table (`GetSectorScramble`) where required.
- **Hunk caching** — compressed hunks are read once and cached; repeated sector reads within a hunk are cheap.
- `ReadSector` returns a **cooked 2048-byte** sector; `ReadRawSector` returns the raw `UnitBytes` sector.

`UnitBytes` and `TotalBytes` are internal here; the public surface lives on `ChdContainer`.

### 3. ParserFactory + IConsoleParser — pluggable parsing

`ParserFactory.CreateParser(type, reader)` maps `ConsoleType` → parser instance. Every parser implements `IConsoleParser`:

```csharp
public interface IConsoleParser
{
    ConsoleType GetConsoleType();
    string GetConsoleName();
    bool Parse(FsNode rootNode);
    bool ParseTrack(FsNode rootNode, TrackInfo track);
    bool ForceMode { get; set; }
}
```

- `Parse` — parse the whole disc (picks the right track itself).
- `ParseTrack` — parse starting from an explicit track (used by multi-track logic and by higher-level parsers).
- `ForceMode` — bypass verification checks for damaged/atypical dumps.

### 4. The parsers

| Parser | Format | Strategy |
|---|---|---|
| `Iso9660Parser` | ISO 9660 (+ High Sierra, CD-ROM XA) | Volume descriptors → root directory → recursive records; XA interleaving via `FileNumber`. |
| `UdfParser` | UDF | Anchor → main volume descriptor sequence → partition → file sets. |
| `XdvdfsParser` | XDVDFS | Xbox DVD file system table scan. |
| `HfsParser` | HFS / HFS+ | Partition map → MDB/catalog B-tree → catalog records (handles both classic and plus variants). |
| `ThreeDoParser` | Opera FS | 3DO's Opera file system. |
| `CDiFsParser` | CD-i Green Book | CD-i file system with interleaved data. |
| `PcFxIsoParser` | Tolerant ISO 9660 | Byte-offset volume descriptor scanning, candidate root offsets, continue-on-error records. |
| `PcEngineCdParser` | Raw + minimal ISO | Boot signature scan → data-area detection → optional ISO → `TRACKnn.iso` fallback. |
| Console wrappers | -- | e.g. `PlayStationAutoDetectParser`, `DreamcastParser` (IP.BIN track preference), fallback chains (Pippin, PS3, Nuon, 3DO, CD-i). |

### 5. ConsoleTypeRegistry — configuration as data

`ConsoleTypeRegistry.All` is a static list of `ConsoleTypeInfo` (type + display name + CLI aliases). `ParserFactory.GetAllSupportedConsoles()` is derived from it, and CHDMounter-style host apps resolve user input through `ConsoleTypeRegistry.Parse(alias)`. Keeping this as data guarantees UI, CLI, and parser dispatch never drift apart.

---

## Data flow: from CHD to file bytes

```
CHD file
  └─ CHDSharp decompresses hunk ──► SectorReader (cache)
       └─ track-aware LBA mapping + offset detection
            └─ cooked/raw sector bytes
                 ├─ parser reads directories → FsNode tree
                 │    └─ ChdContainer.BuildFromFsNode → FileEntry list
                 └─ ReadFile: entry.Extents → sector reads → user buffer
```

## Virtual export generation

For `GenericCue*` types, `MountAndParse` synthesizes a root `FsNode` with:

1. `<name>.cue` — generated CUE sheet text (FILE/TRACK/INDEX lines).
2. `<name>.bin` or `<name>.iso` — data track bytes, sector size per mode (2048 cooked vs 2352 raw; audio in BIN always 2352).
3. `<name>_TrackNN.wav` (WAV modes) — per-audio-track PCM with 44-byte RIFF header, pregap skipped, starting at `INDEX 01`.

The generated files are served lazily by `ReadFile` — nothing is materialized on disk. Track modes (`MODE1/2048`, `MODE2/2352`, `AUDIO`...) are derived from the CHD metadata and the actual sector size used.

## Interleaved (CD-ROM XA) data

PlayStation discs can interleave files inside one sector stream. The ISO 9660 parser marks such files with `IsInterleaved` + `FileNumber`; `ReadFile` uses the XA subheader file number to select the right interleave units when assembling file bytes.

## Error handling philosophy

- Parse failures are **return values**, not exceptions — `MountAndParse` returns `false`, `TryFindFile` returns `false` + message, `ReadFile` returns partial/0.
- Exceptions are reserved for truly exceptional conditions (invalid arguments, I/O failures on the CHD itself).
- `Dispose` is safe to call regardless of mount state.

---

*Previous: [API Reference](API-Reference.md) · Next: [Migration Guide](Migration-Guide.md) · [Back to Home](Home.md)*
