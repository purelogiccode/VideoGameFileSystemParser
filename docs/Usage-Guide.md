# Usage Guide

This guide covers the full lifecycle of working with a disc image: opening, mounting, enumerating, reading, error handling, raw passthrough, and console-type selection.

---

## The object model in one minute

| Concept | Type | Purpose |
|---|---|---|
| Container | `ChdContainer` | Owns the CHD file and the parsed file system |
| Console type | `ConsoleType` (enum) | Tells the library *how* to interpret the image |
| Entry | `FileEntry` | A file or directory in the parsed tree |
| Extent | `FileExtent` | A contiguous `(LBA, size)` region of a file |
| Track | `TrackInfo` | Metadata for one physical disc track |
| Registry | `ConsoleTypeRegistry` | Display names + CLI aliases for console types |

All types live in `VideoGameFileSystemParser.Models` except `ChdContainer`, `SectorReader`, and `ParserFactory`, which live in `VideoGameFileSystemParser.Parsers`.

---

## Step 1 — Open the container

```csharp
using VideoGameFileSystemParser.Parsers;

using var container = new ChdContainer(@"C:\ROMs\game.chd");
```

`ChdContainer` implements `IDisposable` **and** `IAsyncDisposable`, so both `using` and `await using` work:

```csharp
await using var container = new ChdContainer(@"C:\ROMs\game.chd");
```

---

## Step 2 — Mount and parse

### `MountAndParse` (recommended)

```csharp
public bool MountAndParse(ConsoleType consoleType)
```

Opens the CHD, creates the correct parser for `consoleType`, parses the file system, and builds the entry tree.

```csharp
if (container.MountAndParse(ConsoleType.Ps2))
{
    // success — container.Entries is populated
}
else
{
    // the image could not be parsed as PS2
}
```

For the `GenericCue*` console types, `MountAndParse` does **not** run a file system parser — it builds a **virtual CUE export** (see [Virtual CUE/BIN/ISO/WAV Exports](Virtual-Exports.md)). For `GenericIsoRaw2352` / `GenericIsoRaw2048` it exposes the whole image as a single raw file.

### `Open` (low-level only)

```csharp
public bool Open(ConsoleType consoleType)
```

Opens the CHD and initializes the reader, filling the metadata properties (`UnitBytes`, `HunkBytes`, `VolumeSize`, `VolumeName`, `HasDataTracks`) **without** parsing any file system. Use it when you only need sector-level metadata.

### Return value semantics

Both methods return `false` when the CHD cannot be opened/parsed. They never throw for unparseable images — check the return value.

---

## Step 3 — Explore the file system

### All entries at once

```csharp
public IReadOnlyList<FileEntry> Entries { get; }
```

`Entries` is a flat, pre-computed list of **all** files and directories with full paths:

```csharp
foreach (var entry in container.Entries)
{
    Console.WriteLine($"{entry.FullPath}  ({entry.Size:N0} bytes)");
}
```

### Directory listing

```csharp
public IEnumerable<FileEntry> ListDirectory(string path)
```

Enumerates the children of a directory. Use `"\\"` or `"/"` for the root:

```csharp
foreach (var item in container.ListDirectory("\\"))
{
    Console.WriteLine($"{(item.IsDirectory ? "[DIR] " : "[FILE]")} {item.Name}");
}
```

### Find a file

```csharp
public FileEntry? FindFile(string path)
```

Paths use backslashes with a leading `\` (forward slashes are also accepted):

```csharp
var configFile = container.FindFile("\\SYSTEM.CNF");
if (configFile is { IsDirectory: false })
{
    Console.WriteLine($"Found {configFile.Name}, {configFile.Size:N0} bytes");
}
```

### Safe lookup with error messages

```csharp
public bool TryFindFile(string path, out FileEntry? entry, out string? error)
```

```csharp
if (container.TryFindFile("\\NONEXISTENT.TXT", out var entry, out var error))
{
    Console.WriteLine($"Found: {entry!.Name}");
}
else
{
    Console.WriteLine($"Error: {error}"); // "Error: Path not found: \NONEXISTENT.TXT"
}
```

---

## Step 4 — Read file data

```csharp
public int ReadFile(FileEntry entry, ulong offset, byte[] buffer, int bufOffset, int count)
```

Reads up to `count` bytes starting at `offset` inside the file into `buffer` at `bufOffset`. Returns the number of bytes actually read (0 at end of file).

```csharp
// Read an entire file
var data = new byte[configFile.Size];
int read = container.ReadFile(configFile, 0, data, 0, data.Length);

// Read a slice (1024 bytes at offset 512)
var partial = new byte[1024];
int partialRead = container.ReadFile(configFile, 512, partial, 0, partial.Length);
```

### How reads work under the hood

- Files are stored as lists of contiguous **extents** (`FileExtent { Lba, Size }`).
- `ReadFile` maps `offset` to the correct extent and sector, handles interleaved (CD-ROM XA) files via `FileNumber`, and reads through the CHD's hunk cache.
- **Raw passthrough entries** (`IsRawPassthrough == true`, e.g. `image.iso` in raw mode) bypass sector logic and read the CHD bytes directly — `Size` equals the full image size.
- Returns `0` if `entry` is a directory or the offset is past the end.

### Streaming large files

Read in chunks instead of allocating the whole file:

```csharp
var buffer = new byte[64 * 1024];
ulong offset = 0;
while (true)
{
    int n = container.ReadFile(file, offset, buffer, 0, buffer.Length);
    if (n <= 0) break;
    ProcessChunk(buffer, n);
    offset += (ulong)n;
}
```

---

## Step 5 — Console type selection

### Direct selection

Pass any `ConsoleType` — the library picks the parser automatically:

```csharp
container.MountAndParse(ConsoleType.Ps2);
container.MountAndParse(ConsoleType.Saturn);
container.MountAndParse(ConsoleType.Xbox);
```

### Auto-detection (PlayStation)

```csharp
if (container.MountAndParse(ConsoleType.PlayStation))
{
    Console.WriteLine($"Detected as: {container.ConsoleType}");
}
```

The PlayStation auto-detect parser inspects `SYSTEM.CNF` to identify the boot executable and confirm the system.

### Trying multiple candidates

```csharp
ConsoleType[] candidates = [ConsoleType.Ps1, ConsoleType.Ps2, ConsoleType.Saturn, ConsoleType.Dreamcast];

foreach (var type in candidates)
{
    using var test = new ChdContainer("game.chd");
    if (test.MountAndParse(type))
    {
        Console.WriteLine($"Parsed as {type} with {test.Entries.Count} entries.");
        break;
    }
}
```

### Resolving console types from user input / CLI aliases

Host applications (CLI tools, UI lists) should use `ConsoleTypeRegistry` — the single source of truth:

```csharp
using VideoGameFileSystemParser.Models;

// Parse a user-supplied alias (case-insensitive)
ConsoleType type = ConsoleTypeRegistry.Parse("ps2");          // ConsoleType.Ps2
ConsoleType unknown = ConsoleTypeRegistry.Parse("nope");      // ConsoleType.Unknown

// Enumerate everything with display names + aliases
foreach (var entry in ConsoleTypeRegistry.All)
{
    Console.WriteLine($"{entry.DisplayName,-24} {string.Join(", ", entry.Aliases)}");
}
```

> ⚠️ **Do not rely on numeric `ConsoleType` values** — the enum ordering is not a stable contract. Always resolve through `ConsoleTypeRegistry` or use the enum members directly.

---

## Step 6 — Raw passthrough mode

Mount as `GenericIsoRaw2352` (or `GenericIsoRaw2048` for 2048-byte units) to expose the entire disc as a single file named `image.iso`:

```csharp
container.MountAndParse(ConsoleType.GenericIsoRaw2352);

foreach (var entry in container.Entries)
{
    if (entry.IsRawPassthrough && !entry.IsDirectory)
    {
        Console.WriteLine($"Raw image: {entry.Name}, {entry.Size:N0} bytes");
        var sector = new byte[2048];
        int read = container.ReadFile(entry, 0, sector, 0, sector.Length);
        Console.WriteLine($"Read {read} bytes from sector 0");
    }
}
```

This is useful for dumping, hashing, or piping the image into tools that expect a plain ISO file.

---

## Step 7 — Virtual exports

Mounting as any `GenericCue*` type produces virtual `.cue` / `.bin` / `.iso` / `.wav` entries you can read and save to disk:

```csharp
container.MountAndParse(ConsoleType.GenericCueBin2352);

var cue = container.FindFile("\\game.cue");
if (cue is not null)
{
    var cueData = new byte[cue.Size];
    container.ReadFile(cue, 0, cueData, 0, cueData.Length);
    File.WriteAllBytes(@"C:\out\game.cue", cueData);
}
```

Full documentation of modes, sector sizes, and WAV behavior: [Virtual CUE/BIN/ISO/WAV Exports](Virtual-Exports.md).

---

## Error handling summary

| Situation | Behavior |
|---|---|
| CHD cannot be opened | `Open`/`MountAndParse` return `false` |
| Image not parseable as the requested type | `MountAndParse` returns `false` |
| Path not found | `FindFile` → `null`; `TryFindFile` → `false` + message |
| Read past end of file | `ReadFile` returns `0` / partial bytes |
| Reading a directory | `ReadFile` returns `0` |

The library is designed to be **exception-light**: parse failures are reported via return values. Dispose still releases the CHD handle safely.

---

*Previous: [Getting Started](Getting-Started.md) · Next: [Supported Consoles](Supported-Consoles.md) · [Back to Home](Home.md)*
