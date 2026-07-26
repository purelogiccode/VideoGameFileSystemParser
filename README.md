# VideoGameFileSystemParser

A **cross-platform .NET library** for parsing video game console disc image file systems. Supports CHD, ISO, and raw sector data across 31 console formats including PlayStation, Xbox, Dreamcast, CD-i, 3DO, Pippin (HFS), and more.

[![NuGet](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-blue)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/badge/NuGet-VideoGameFileSystemParser-blue)](https://www.nuget.org/)
[![License](https://img.shields.io/badge/License-MIT-green)](https://github.com/purelogiccode/CSharp_SimpleChdDrive/blob/main/LICENSE)

---

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Examples](#examples)
  - [Open a CHD and List Files](#example-1-open-a-chd-and-list-files)
  - [Read a File from a Disc Image](#example-2-read-a-file-from-a-disc-image)
  - [Auto-Detect Console Type](#example-3-auto-detect-console-type)
  - [Export Virtual CUE/BIN](#example-4-export-virtual-cuebin)
  - [Browse All Supported Consoles](#example-5-browse-all-supported-consoles)
  - [Error Handling with TryFindFile](#example-6-error-handling-with-tryfindfile)
  - [Read Raw Sectors](#example-7-read-raw-sectors)
  - [Async Usage](#example-8-async-usage)
- [Supported Consoles](#supported-consoles)
  - [Console Reference Table](#console-reference-table)
  - [Parsing Logic per Console](#parsing-logic-per-console)
  - [System Observations](#system-observations)
- [API Reference](#api-reference)
  - [ChdContainer](#chdcontainer)
  - [ParserFactory](#parserfactory)
  - [IConsoleParser](#iconsoleparser)
  - [FileEntry](#fileentry)
  - [FileExtent](#fileextent)
  - [FsNode](#fsnode)
  - [FsExtent](#fsextent)
  - [FsNodeType](#fsnodetype)
  - [ConsoleType](#consoletype)
  - [ConsoleInfo](#consoleinfo)
  - [TrackInfo](#trackinfo)
  - [SectorReader](#sectorreader)
- [Architecture](#architecture)
- [Dependencies](#dependencies)
- [License](#license)

---

## Installation

Install via the .NET CLI:

```bash
dotnet add package VideoGameFileSystemParser
```

Or via the NuGet Package Manager:

```
Install-Package VideoGameFileSystemParser
```

Or add directly to your `.csproj`:

```xml
<PackageReference Include="VideoGameFileSystemParser" Version="1.0.0" />
```

**Target Frameworks:** `net8.0`, `net9.0`, `net10.0`

---

## Quick Start

```csharp
using VideoGameFileSystemParser;
using VideoGameFileSystemParser.Models;
using VideoGameFileSystemParser.Parsers;

// Open a CHD file
using var container = new ChdContainer("game.chd");

// Parse as PlayStation 2
if (container.MountAndParse(ConsoleType.Ps2))
{
    Console.WriteLine($"Volume: {container.VolumeName}");
    Console.WriteLine($"Size: {container.VolumeSize:N0} bytes");

    // List root directory
    foreach (var entry in container.ListDirectory("\\"))
    {
        Console.WriteLine($"  {(entry.IsDirectory ? "[DIR] " : "[FILE]")} {entry.Name}  ({entry.Size:N0} bytes)");
    }

    // Read a file
    var file = container.FindFile("\\SYSTEM.CNF");
    if (file is { IsDirectory: false })
    {
        var buffer = new byte[file.Size];
        int bytesRead = container.ReadFile(file, 0, buffer, 0, buffer.Length);
        Console.WriteLine($"Read {bytesRead} bytes from {file.Name}");
    }
}
```

---

## Examples

### Example 1: Open a CHD and List Files

```csharp
using VideoGameFileSystemParser.Models;
using VideoGameFileSystemParser.Parsers;

using var container = new ChdContainer(@"C:\ROMs\my_game.chd");

if (!container.MountAndParse(ConsoleType.Ps1))
{
    Console.WriteLine("Failed to parse disc image.");
    return;
}

Console.WriteLine($"Volume: {container.VolumeName}");
Console.WriteLine($"Total Size: {container.VolumeSize:N0} bytes");
Console.WriteLine($"Sector Size: {container.UnitBytes} bytes");
Console.WriteLine($"Hunk Size: {container.HunkBytes} bytes");
Console.WriteLine();

// Recursively list all entries
foreach (var entry in container.Entries)
{
    var indent = new string(' ', entry.FullPath.Count(c => c == '\\') * 2);
    var marker = entry.IsDirectory ? "[DIR] " : "[FILE]";
    Console.WriteLine($"{indent}{marker} {entry.Name}");
}
```

### Example 2: Read a File from a Disc Image

```csharp
using VideoGameFileSystemParser.Models;
using VideoGameFileSystemParser.Parsers;

using var container = new ChdContainer("game.chd");
container.MountAndParse(ConsoleType.Ps2);

// Find a specific file
var configFile = container.FindFile("\\SYSTEM.CNF");
if (configFile is null)
{
    Console.WriteLine("File not found.");
    return;
}

// Read the entire file into a byte array
var data = new byte[configFile.Size];
int bytesRead = container.ReadFile(configFile, 0, data, 0, data.Length);

// Convert to text (if applicable)
string text = System.Text.Encoding.ASCII.GetString(data, 0, bytesRead);
Console.WriteLine(text);

// Read with offset (first 1024 bytes starting at offset 512)
var partial = new byte[1024];
int partialRead = container.ReadFile(configFile, 512, partial, 0, partial.Length);
```

### Example 3: Auto-Detect Console Type

```csharp
using VideoGameFileSystemParser.Models;
using VideoGameFileSystemParser.Parsers;

using var container = new ChdContainer("unknown_game.chd");

// Use PlayStation auto-detection (checks SYSTEM.CNF for boot executable)
if (container.MountAndParse(ConsoleType.PlayStation))
{
    Console.WriteLine($"Auto-detected as: {container.ConsoleType}");
    Console.WriteLine($"Parsed {container.Entries.Count} entries.");
}

// Try multiple console types
ConsoleType[] candidates = [ConsoleType.Ps1, ConsoleType.Ps2, ConsoleType.Saturn, ConsoleType.Dreamcast];

foreach (var type in candidates)
{
    using var test = new ChdContainer("game.chd");
    if (test.MountAndParse(type))
    {
        Console.WriteLine($"Successfully parsed as {type} with {test.Entries.Count} entries.");
        break;
    }
}
```

### Example 4: Export Virtual CUE/BIN

```csharp
using VideoGameFileSystemParser.Models;
using VideoGameFileSystemParser.Parsers;

using var container = new ChdContainer("game.chd");

// Mount as CUE/BIN with 2352-byte sectors
if (container.MountAndParse(ConsoleType.GenericCueBin2352Default))
{
    Console.WriteLine("Virtual CUE/BIN files:");
    foreach (var entry in container.Entries)
    {
        Console.WriteLine($"  {entry.Name} ({entry.Size:N0} bytes)");
    }

    // Read the .cue file content
    var cueFile = container.FindFile("\\game.cue");
    if (cueFile is not null)
    {
        var cueData = new byte[cueFile.Size];
        container.ReadFile(cueFile, 0, cueData, 0, cueData.Length);
        Console.WriteLine(System.Text.Encoding.ASCII.GetString(cueData));
    }
}

// Other export modes:
// ConsoleType.GenericCueBin2048   - CUE/BIN with 2048-byte cooked sectors
// ConsoleType.GenericCueIso       - CUE/ISO with 2048-byte sectors
// ConsoleType.GenericCueBinWav    - CUE/BIN with separate WAV audio tracks
// ConsoleType.GenericCueIsoWav    - CUE/ISO with separate WAV audio tracks
```

### Example 5: Browse All Supported Consoles

```csharp
using VideoGameFileSystemParser.Parsers;

// List all supported console types
foreach (var console in ParserFactory.GetAllSupportedConsoles())
{
    Console.WriteLine($"  {console.Type,-30} {console.Name}");
}

// Output:
//   ThreeDo                        3DO
//   AmigaCd                        Amiga CD
//   AmigaCd32                      Amiga CD32
//   AmigaCdtv                      Amiga CDTV
//   CDi                            CD-i
//   Dreamcast                      Dreamcast
//   ...
```

### Example 6: Error Handling with TryFindFile

```csharp
using VideoGameFileSystemParser.Models;
using VideoGameFileSystemParser.Parsers;

using var container = new ChdContainer("game.chd");
container.MountAndParse(ConsoleType.Ps2);

// Safe file lookup with error message
if (container.TryFindFile("\\NONEXISTENT.TXT", out var entry, out var error))
{
    Console.WriteLine($"Found: {entry!.Name}");
}
else
{
    Console.WriteLine($"Error: {error}");
    // Output: "Error: Path not found: \NONEXISTENT.TXT"
}

// Iterate directories safely
foreach (var item in container.ListDirectory("\\"))
{
    if (item.IsDirectory)
    {
        Console.WriteLine($"Directory: {item.Name}");
        foreach (var child in container.ListDirectory(item.FullPath))
        {
            Console.WriteLine($"  {(child.IsDirectory ? "[DIR]" : "[FILE]")} {child.Name}");
        }
    }
}
```

### Example 7: Read Raw Sectors

```csharp
using VideoGameFileSystemParser.Models;
using VideoGameFileSystemParser.Parsers;

using var container = new ChdContainer("game.chd");
container.MountAndParse(ConsoleType.GenericIsoRaw);

// In raw mode, the entire disc image is exposed as a single file
foreach (var entry in container.Entries)
{
    if (entry.IsRawPassthrough && !entry.IsDirectory)
    {
        Console.WriteLine($"Raw image: {entry.Name}, {entry.Size:N0} bytes");

        // Read the first 2048 bytes (one sector)
        var sector = new byte[2048];
        int read = container.ReadFile(entry, 0, sector, 0, sector.Length);
        Console.WriteLine($"Read {read} raw bytes from sector 0");
    }
}
```

### Example 8: Async Usage

```csharp
using VideoGameFileSystemParser.Models;
using VideoGameFileSystemParser.Parsers;

await using var container = new ChdContainer("game.chd");
container.MountAndParse(ConsoleType.Psp);

// Use async dispose (container implements IAsyncDisposable)
foreach (var entry in container.ListDirectory("\\"))
{
    Console.WriteLine(entry.Name);
}
// Automatically disposed when exiting scope
```

---

## Supported Consoles

### Console Reference Table

| Console | ConsoleType | File System | Primary Parser | Fallback Parser(s) | File Tree | Notes |
|---------|-------------|-------------|----------------|---------------------|-----------|-------|
| 3DO | `ThreeDo` | Opera FS | `ThreeDoParser` | ISO 9660 | Yes | Custom Opera FS; ISO fallback for some discs |
| Amiga CD | `AmigaCd` | ISO 9660 | `Iso9660Parser` | -- | Yes | Standard ISO 9660 |
| Amiga CD32 | `AmigaCd32` | ISO 9660 | `Iso9660Parser` | -- | Yes | Standard ISO 9660 |
| Amiga CDTV | `AmigaCdtv` | ISO 9660 | `Iso9660Parser` | -- | Yes | Standard ISO 9660 |
| Apple Bandai Pippin | `Pippin` | HFS / HFS+ | `HfsParser` | UDF, ISO 9660 | Yes | Macintosh Hierarchical FS; tries HFS first, then HFS+, UDF, ISO |
| CD-i | `CDi` | CD-i Green Book | `CDiFsParser` | ISO 9660 | Yes | Custom CD-i FS with interleaved data support |
| Dreamcast | `Dreamcast` | ISO 9660 + IP.BIN | `Iso9660Parser` | -- | Yes | Prefers track with `SEGA SEGAKATANA` boot signature |
| FM Towns | `FmTowns` | ISO 9660 | `Iso9660Parser` | -- | Yes | Standard ISO 9660 |
| Generic ISO 9660 | `GenericIso9660` | ISO 9660 | `Iso9660Parser` | -- | Yes | Standard ISO 9660 / High Sierra |
| Generic ISO Raw | `GenericIsoRaw` | Raw sectors | `GenericIsoRawParser` | -- | No | Exposes entire image as `image.iso` |
| NeoGeo CD | `NeoGeoCd` | ISO 9660 | `Iso9660Parser` | -- | Yes | Standard ISO 9660 |
| Nuon | `Nuon` | UDF / ISO 9660 | `UdfParser` | ISO 9660 | Yes | VM Labs Nuon DVD; tries UDF first |
| PC Engine CD | `PcEngineCd` | **Non-standard** | `PcEngineCdParser` | ISO 9660, raw track | **Partial** | See [PC Engine CD observations](#pc-engine-cd-turbografx-cd) |
| PC-FX | `PcFx` | **Non-standard** | `PcFxIsoParser` | ISO 9660, raw track | **Partial** | See [PC-FX observations](#nec-pc-fx) |
| PC-98 | `Pc98` | ISO 9660 | `Iso9660Parser` | -- | Yes | Standard ISO 9660 |
| Pico | `Pico` | ISO 9660 | `Iso9660Parser` | -- | Yes | Standard ISO 9660 |
| PlayStation (Auto) | `PlayStation` | ISO 9660 | `Iso9660Parser` | -- | Yes | Auto-detect mode using ISO 9660 |
| PlayStation 1 | `Ps1` | ISO 9660 | `Iso9660Parser` | -- | Yes | Standard ISO 9660; CD-ROM XA aware |
| PlayStation 2 | `Ps2` | ISO 9660 | `Iso9660Parser` | -- | Yes | Standard ISO 9660; CD-ROM XA aware |
| PlayStation 3 | `Ps3` | UDF / ISO 9660 | `UdfParser` | ISO 9660 | Yes | Blu-ray uses UDF; DVD fallback to ISO |
| PSP | `Psp` | ISO 9660 (UMD) | `Iso9660Parser` | -- | Yes | UMD discs use standard ISO 9660 |
| Sega Genesis CD | `SegaGenesisCd` | ISO 9660 | `Iso9660Parser` | -- | Yes | Standard ISO 9660 |
| Sega Saturn | `Saturn` | ISO 9660 | `Iso9660Parser` | -- | Yes | Standard ISO 9660 |
| Sharp X68000 | `X68000` | ISO 9660 / UDF | `Iso9660Parser` | UDF | Yes | Tries ISO 9660 first, falls back to UDF |
| Xbox | `Xbox` | XDVDFS | `XdvdfsParser` | -- | Yes | Xbox DVD File System |
| Xbox 360 | `Xbox360` | XDVDFS | `XdvdfsParser` | -- | Yes | Xbox 360 DVD File System |
| CUE/BIN (2352) | `GenericCueBin2352Default` | Virtual export | -- | -- | Virtual | Virtual CUE sheet + BIN with 2352-byte raw sectors |
| CUE/BIN (2048) | `GenericCueBin2048` | Virtual export | -- | -- | Virtual | Virtual CUE sheet + BIN with 2048-byte cooked sectors |
| CUE/ISO | `GenericCueIso` | Virtual export | -- | -- | Virtual | Virtual CUE sheet + ISO with 2048-byte sectors |
| CUE/BIN/WAV | `GenericCueBinWav` | Virtual export | -- | -- | Virtual | Virtual CUE + BIN data + separate WAV audio tracks |
| CUE/ISO/WAV | `GenericCueIsoWav` | Virtual export | -- | -- | Virtual | Virtual CUE + ISO data + separate WAV audio tracks |

---

### Parsing Logic per Console

Each console parser follows a specific strategy to locate and parse the file system. Below is the exact resolution order for each.

#### Standard ISO 9660 Parsers

These consoles use a straightforward ISO 9660 parser on the first data track found:

| Console | Parser Chain |
|---------|-------------|
| Amiga CD | `Iso9660Parser` on first data track |
| Amiga CD32 | `Iso9660Parser` on first data track |
| Amiga CDTV | `Iso9660Parser` on first data track |
| FM Towns | `Iso9660Parser` on first data track |
| Generic ISO 9660 | `Iso9660Parser` on first data track |
| NeoGeo CD | `Iso9660Parser` on first data track |
| PC-98 | `Iso9660Parser` on first data track |
| Pico (Sega) | `Iso9660Parser` on first data track |
| PlayStation (Auto) | `Iso9660Parser` on first data track |
| PlayStation 1 | `Iso9660Parser` on first data track |
| PlayStation 2 | `Iso9660Parser` on first data track |
| PSP | `Iso9660Parser` on first data track |
| Sega Genesis CD | `Iso9660Parser` on first data track |
| Sega Saturn | `Iso9660Parser` on first data track |

#### Parsers with Fallback Chains

| Console | Step 1 | Step 2 | Step 3 | Step 4 |
|---------|--------|--------|--------|--------|
| **PlayStation 3** | `UdfParser` | `Iso9660Parser` | -- | -- |
| **Nuon** | `UdfParser` | `Iso9660Parser` | -- | -- |
| **Sharp X68000** | `Iso9660Parser` | `UdfParser` | -- | -- |
| **Apple Bandai Pippin** | `HfsParser` (HFS) | `HfsParser` (HFS+) | `UdfParser` | `Iso9660Parser` |
| **CD-i** | `CDiFsParser` (CD-i Green Book) | `Iso9660Parser` | -- | -- |
| **3DO** | `ThreeDoParser` (Opera FS) | `Iso9660Parser` | -- | -- |

#### Special Parsers

| Console | Logic |
|---------|-------|
| **Dreamcast** | Scans all data tracks for the `SEGA SEGAKATANA` IP.BIN boot signature. Parses the track containing the signature using `Iso9660Parser`. Prefers tracks with IP.BIN over tracks without. |
| **Xbox / Xbox 360** | Uses `XdvdfsParser` (Xbox DVD File System) on the first data track. |
| **Generic ISO Raw** | No parsing. Exposes the entire disc image as a single file named `image.iso` with raw passthrough. |
| **Generic CUE/BIN/ISO/WAV** | No file system parsing. Builds a virtual CUE sheet and exposes virtual `.cue`, `.bin`/`.iso`, and `.wav` files for direct extraction. |

#### Non-Standard Parsers (PC Engine CD, PC-FX)

These consoles do **not** use a standard file system. See [System Observations](#system-observations) for full details.

| Console | Step 1 | Step 2 | Step 3 |
|---------|--------|--------|--------|
| **PC Engine CD** | Locate boot signature (`PC Engine CD-ROM SYSTEM`, `GAMES EXPRESS CD CARD`, or `PC ENGINE`) | Attempt `Iso9660Parser` with adjusted data area start | Expose raw data tracks as `TRACKnn.iso` files |
| **PC-FX** | Attempt `PcFxIsoParser` (tolerant ISO 9660 with byte-offset VD scanning) per data track | Attempt standard `Iso9660Parser` | Expose raw data tracks as `TRACKnn.iso` files |

---

### System Observations

#### PC Engine CD (TurboGrafx-CD)

**The NEC PC Engine CD does not use a standard file system.**

Most PC Engine CD / TurboGrafx-CD games store game data as raw binary streams directly across the data sectors of the disc. Instead of a file system with directories and named files, the game code uses the CD-ROM BIOS hardware to seek directly to specific Logical Block Addresses (LBA) and read sectors into RAM.

**How this library handles it:**

1. **Boot signature detection:** The parser scans the data track for one of three known boot signatures:
   - `PC Engine CD-ROM SYSTEM` (standard CD-ROM system cards)
   - `GAMES EXPRESS CD CARD` (Games Express titles)
   - `PC ENGINE` (alternative identifier)

2. **Data area start detection:** The parser identifies where the actual data begins by:
   - Checking for pregap metadata (`PGTYPE:V` in track metadata)
   - Scanning for the first non-zero sector (up to 600 sectors)
   - Testing multiple candidate offsets (track start, pregap-adjusted start, first non-zero sector)

3. **ISO 9660 attempt:** If a boot signature is found, the parser attempts to parse an ISO 9660 file system starting at the detected data area. Some CD-ROM System Card discs and later Super CD-ROM² titles *do* include a minimal ISO 9660 structure.

4. **Raw track fallback:** If ISO 9660 parsing fails, each data track is exposed as a raw file named `TRACKnn.iso`, allowing direct sector-level access via `ReadFile()`.

**Practical implications:**
- Most PC Engine CD games will show as raw track files, not a browsable directory tree.
- To extract specific game assets, you need game-specific tools or emulator debuggers to determine which LBAs contain which data.
- Audio tracks (Red Book CDDA) are handled separately by the CD controller and are not part of the data file system.

#### NEC PC-FX

**The NEC PC-FX uses a non-standard executable format, even when ISO 9660 is present on the disc.**

The PC-FX features a V810 RISC 32-bit processor and uses CD-ROM media. While some PC-FX discs include ISO 9660 structures, the actual game executable and data bypass the file system entirely.

**How this library handles it:**

1. **Tolerant ISO 9660 parsing (`PcFxIsoParser`):** The PC-FX parser uses a specialized ISO 9660 parser that:
   - Scans for Volume Descriptor signatures (`CD001` or `CDROM`) at both standard byte offsets and arbitrary positions within raw sectors (handles dumps where CD sync headers were not fully stripped)
   - Tests multiple candidate LBA offsets for the root directory (track start, LBA 150, LBA 0, and a scan of up to 512 sectors)
   - Uses continue-on-error directory record parsing (skips invalid records instead of aborting), matching the behavior of C++ DiscImageCreator tools

2. **Standard ISO 9660 fallback:** If the tolerant parser fails, a standard `Iso9660Parser` is tried.

3. **Raw track fallback:** If neither ISO parser succeeds, data tracks are exposed as `TRACKnn.iso` files.

**Practical implications:**
- Some PC-FX discs will parse with a full directory tree (those with standard ISO 9660 layout for supplementary content like movie previews or bitmap images).
- The actual PC-FX game executable (V810 binary) is loaded directly by the PC-FX BIOS using raw LBA addressing, bypassing any file system.
- Game assets are typically packed into proprietary archive structures with offset tables embedded in the main executable. Extracting individual assets requires game-specific unpacker tools.
- Audio tracks are standard Red Book CDDA and handled by the CD controller directly.

#### Why These Systems Are Different

Most other consoles in this library (PlayStation, Saturn, Dreamcast, Xbox, etc.) use a standard file system (ISO 9660, UDF, XDVDFS, etc.) that provides a hierarchical directory tree of named files. The PC Engine CD and PC-FX are exceptions where:

- The **boot code** is a raw binary at a known sector offset, not a file in a directory.
- **Game assets** are addressed by direct sector number (LBA), not by file path.
- **Custom archive formats** are used when files are grouped together, requiring game-specific knowledge to unpack.
- This is why tools like [Aaru Data Preservation Suite](https://github.com/aaru-dps/Aaru) list these formats under "identification and information only" -- they can read the binary header and identify the disc, but cannot extract a traditional folder structure.

---

## API Reference

### ChdContainer

**Namespace:** `VideoGameFileSystemParser.Parsers`
**Implements:** `IDisposable`, `IAsyncDisposable`

The primary entry point for opening CHD disc images and accessing their file systems.

#### Constructor

```csharp
public ChdContainer(string chdPath)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `chdPath` | `string` | File system path to the CHD disc image. |

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Entries` | `IReadOnlyList<FileEntry>` | All file and directory entries in the container. |
| `VolumeName` | `string` | The volume name (derived from the CHD file name). |
| `VolumeSize` | `ulong` | Total size of the disc image in bytes. |
| `HasDataTracks` | `bool` | Whether the CHD contains at least one data track. |
| `UnitBytes` | `uint` | Bytes per sector unit (e.g., 2048 or 2352). |
| `HunkBytes` | `uint` | Bytes per compressed hunk. |
| `ConsoleType` | `ConsoleType` | The console type used for parsing this image. |

#### Methods

##### Open

```csharp
public bool Open(ConsoleType consoleType)
```

Opens the CHD file and initializes the reader pool. Call this if you only need low-level access without parsing a file system.

| Parameter | Type | Description |
|-----------|------|-------------|
| `consoleType` | `ConsoleType` | The console type to configure the reader for. |

**Returns:** `true` if the CHD was opened successfully; otherwise `false`.

##### MountAndParse

```csharp
public bool MountAndParse(ConsoleType consoleType)
```

Opens the CHD, creates the appropriate parser, parses the file system, and builds the entry tree. This is the main method most users should call.

| Parameter | Type | Description |
|-----------|------|-------------|
| `consoleType` | `ConsoleType` | The console type to parse the image as. |

**Returns:** `true` if parsing succeeded; otherwise `false`.

##### FindFile

```csharp
public FileEntry? FindFile(string path)
```

Finds a file or directory entry by its full path.

| Parameter | Type | Description |
|-----------|------|-------------|
| `path` | `string` | The full path to search for (e.g., `\GAME\DATA.BIN`). Forward slashes are also accepted. |

**Returns:** The matching `FileEntry`, or `null` if not found.

##### TryFindFile

```csharp
public bool TryFindFile(string path, out FileEntry? entry, out string? error)
```

Attempts to find a file or directory entry by its full path, providing an error message on failure.

| Parameter | Type | Description |
|-----------|------|-------------|
| `path` | `string` | The full path to search for. |
| `entry` | `out FileEntry?` | When successful, the matching entry. |
| `error` | `out string?` | When not found, a description of why. |

**Returns:** `true` if the entry was found; otherwise `false`.

##### ListDirectory

```csharp
public IEnumerable<FileEntry> ListDirectory(string path)
```

Enumerates the child entries of a directory specified by path.

| Parameter | Type | Description |
|-----------|------|-------------|
| `path` | `string` | The full path of the directory (use `\\` or `/` for root). |

**Returns:** An `IEnumerable<FileEntry>` of items in the directory.

##### ReadFile

```csharp
public int ReadFile(FileEntry entry, ulong offset, byte[] buffer, int bufOffset, int count)
```

Reads data from a file entry at the specified offset into the provided buffer.

| Parameter | Type | Description |
|-----------|------|-------------|
| `entry` | `FileEntry` | The file entry to read from. |
| `offset` | `ulong` | The byte offset within the file to start reading from. |
| `buffer` | `byte[]` | The destination buffer. |
| `bufOffset` | `int` | The offset within the destination buffer to begin writing. |
| `count` | `int` | The maximum number of bytes to read. |

**Returns:** The number of bytes actually read.

##### Dispose

```csharp
public void Dispose()
```

Releases all resources used by the container, including all readers and the underlying CHD file.

##### DisposeAsync

```csharp
public ValueTask DisposeAsync()
```

Asynchronously disposes the container. Supports `await using` syntax.

---

### ParserFactory

**Namespace:** `VideoGameFileSystemParser.Parsers`

Provides factory methods to create console-specific parsers and enumerate supported consoles.

#### Methods

##### CreateParser

```csharp
public static IConsoleParser? CreateParser(ConsoleType type, SectorReader reader)
```

Creates a parser instance for the specified console type.

| Parameter | Type | Description |
|-----------|------|-------------|
| `type` | `ConsoleType` | The console type to create a parser for. |
| `reader` | `SectorReader` | The sector reader to use for disc access. |

**Returns:** An `IConsoleParser` implementation, or `null` if the console type is not supported.

##### GetAllSupportedConsoles

```csharp
public static IEnumerable<ConsoleInfo> GetAllSupportedConsoles()
```

Returns the list of all supported console types with their display names.

**Returns:** An `IEnumerable<ConsoleInfo>` for all supported consoles.

---

### IConsoleParser

**Namespace:** `VideoGameFileSystemParser.Interfaces`

Defines the contract for a console-specific file system parser.

#### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetConsoleType()` | `ConsoleType` | Returns the console type this parser handles. |
| `GetConsoleName()` | `string` | Returns the human-readable console name. |
| `Parse(FsNode rootNode)` | `bool` | Parses the file system and populates the root node. Returns `true` on success. |
| `ParseTrack(FsNode rootNode, TrackInfo track)` | `bool` | Parses the file system from a specific track. Returns `true` on success. |

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `ForceMode` | `bool` | When `true`, forces parsing even when verification fails. |

---

### FileEntry

**Namespace:** `VideoGameFileSystemParser.Models`

Represents a file or directory entry in the virtual file system.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | The file or directory name (without path). |
| `FullPath` | `string` | The full path with leading backslash (e.g., `\GAME\DATA.BIN`). |
| `Lba` | `uint` | The logical block address of the first extent. |
| `Size` | `ulong` | The total size in bytes. |
| `Offset` | `uint` | The byte offset within the sector for embedded data. |
| `IsDirectory` | `bool` | Whether this entry is a directory. |
| `ModifiedTime` | `DateTime` | The last modified date and time. |
| `FileNumber` | `byte` | The file number used for interleaved (XA) data access. |
| `IsInterleaved` | `bool` | Whether the data is interleaved across multiple files. |
| `IsRawPassthrough` | `bool` | Whether to read as raw bytes directly from the CHD. |
| `IsEmbedded` | `bool` | Whether the data is embedded within a file entry sector. |
| `Extents` | `List<FileExtent>` | The list of contiguous data extents that make up this file's data. |

---

### FileExtent

**Namespace:** `VideoGameFileSystemParser.Models`

Represents a contiguous data extent with a starting LBA and size.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Lba` | `uint` | The starting logical block address. |
| `Size` | `ulong` | The size in bytes. |

---

### FsNode

**Namespace:** `VideoGameFileSystemParser.Models`

Represents a node in the parsed file system tree. Used internally by parsers and exposed for advanced scenarios.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | The file or directory name. |
| `Lba` | `uint` | The LBA of the first extent. |
| `Size` | `ulong` | The total data size in bytes. |
| `FileNumber` | `byte` | The file number for interleaved (XA) access. |
| `IsInterleaved` | `bool` | Whether data is interleaved. |
| `IsDirectory` | `bool` | Whether this node is a directory. |
| `IsMultiExtent` | `bool` | Whether this node spans multiple extents. |
| `IsRawPassthrough` | `bool` | Whether to read data as raw bytes. |
| `IsEmbedded` | `bool` | Whether data is embedded within a file entry sector. |
| `EmbeddedOffset` | `uint` | The byte offset within the sector for embedded data. |
| `ModifiedTime` | `DateTime?` | The last modification timestamp, if available. |
| `CreatedTime` | `DateTime?` | The creation timestamp, if available. |
| `AccessedTime` | `DateTime?` | The last access timestamp, if available. |
| `UnixMode` | `uint?` | The POSIX file mode bits, if available. |
| `Uid` | `uint?` | The POSIX user ID, if available. |
| `Gid` | `uint?` | The POSIX group ID, if available. |
| `Inode` | `uint?` | The inode number, if available. |
| `LinkCount` | `uint?` | The number of hard links, if available. |
| `NodeType` | `FsNodeType` | The type of this node (file, directory, or symlink). |
| `SymlinkTarget` | `string?` | The target path, if this node is a symlink. |
| `Extents` | `List<FsExtent>` | The list of contiguous data extents. |
| `Children` | `List<FsNode>` | The child nodes (for directory nodes). |

---

### FsExtent

**Namespace:** `VideoGameFileSystemParser.Models`

Represents a contiguous data extent within the file system tree.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Lba` | `uint` | The starting logical block address. |
| `Size` | `ulong` | The size in bytes. |

---

### FsNodeType

**Namespace:** `VideoGameFileSystemParser.Models`

Identifies the type of a file system node.

| Value | Description |
|-------|-------------|
| `File = 0` | A regular file. |
| `Directory = 4` | A directory. |
| `Symlink = 12` | A symbolic link. |

---

### ConsoleType

**Namespace:** `VideoGameFileSystemParser.Models`

Identifies the target console or disc image format.

| Value | Description |
|-------|-------------|
| `Unknown` | Unknown or unset. |
| `AmigaCd` | Amiga CD format. |
| `AmigaCd32` | Amiga CD32 format. |
| `AmigaCdtv` | Amiga CDTV format. |
| `CDi` | Philips CD-i format. |
| `Dreamcast` | Sega Dreamcast GD-ROM format. |
| `FmTowns` | Fujitsu FM Towns format. |
| `GenericCueBin2352Default` | CUE/BIN with 2352-byte sectors. |
| `GenericCueBin2048` | CUE/BIN with 2048-byte sectors. |
| `GenericCueBinWav` | CUE/BIN with WAV audio tracks. |
| `GenericCueIso` | CUE/ISO image. |
| `GenericCueIsoWav` | CUE/ISO with WAV audio tracks. |
| `GenericIso9660` | Generic ISO 9660 file system. |
| `GenericIsoRaw` | Raw sector passthrough. |
| `Nuon` | VM Labs Nuon DVD format. |
| `NeoGeoCd` | SNK NeoGeo CD format. |
| `PcEngineCd` | NEC PC Engine CD format. |
| `PcFx` | NEC PC-FX format. |
| `Pc98` | NEC PC-98 format. |
| `PlayStation` | PlayStation auto-detection mode. |
| `Ps1` | Sony PlayStation 1 format. |
| `Ps2` | Sony PlayStation 2 format. |
| `Ps3` | Sony PlayStation 3 format. |
| `Psp` | Sony PlayStation Portable format. |
| `Saturn` | Sega Saturn format. |
| `SegaGenesisCd` | Sega Genesis CD format. |
| `ThreeDo` | 3DO Interactive Multiplayer format. |
| `X68000` | Sharp X68000 format. |
| `Xbox` | Microsoft Xbox format. |
| `Xbox360` | Microsoft Xbox 360 format. |
| `Pico` | Sega Pico format. |
| `Pippin` | Apple Bandai Pippin format. |

---

### ConsoleInfo

**Namespace:** `VideoGameFileSystemParser.Models`

Associates a `ConsoleType` with its human-readable display name.

```csharp
public sealed record ConsoleInfo(ConsoleType Type, string Name);
```

| Property | Type | Description |
|----------|------|-------------|
| `Type` | `ConsoleType` | The console type identifier. |
| `Name` | `string` | The human-readable name (e.g., "PlayStation 2", "Xbox"). |

---

### TrackInfo

**Namespace:** `VideoGameFileSystemParser.Models`

Represents metadata for a single disc image track.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Index` | `int` | The one-based track index within the disc. |
| `StartLba` | `uint` | The LBA of the first sector of the track. |
| `ChdOffset` | `uint` | The frame offset within the CHD hunk stream. |
| `Frames` | `uint` | The number of frames in this track. |
| `TrackType` | `string` | The track type string (e.g., `MODE1/2352`, `AUDIO`). |
| `IsDataTrack` | `bool` | Whether this track contains data (vs. audio). |
| `Pregap` | `uint` | The number of pregap frames before this track. |
| `Postgap` | `uint` | The number of postgap frames after this track. |
| `Metadata` | `string` | The raw metadata string from the CHD for this track. |

---

### SectorReader

**Namespace:** `VideoGameFileSystemParser.Parsers`

Provides low-level sector access with track-aware LBA mapping and hunk caching. Used internally by parsers.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `HunkBytes` | `uint` | The number of bytes per compressed hunk. |

#### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Dispose()` | `void` | Releases all resources, including the cached hunk buffer. |

> **Note:** Most `SectorReader` members are `internal`. Expose custom parsing logic through `IConsoleParser` implementations instead.

---

## Architecture

```
ChdContainer (high-level API)
    ├── ChdFile (CHDSharp — CHD V1-V5 reader)
    ├── SectorReader (raw/cooked sector access, hunk caching)
    │   └── TrackInfo (multi-track CHD layout)
    ├── ParserFactory (ConsoleType → IConsoleParser)
    │   └── IConsoleParser (Parse / ParseTrack)
    │       ├── Iso9660Parser  ─→  PSP, Saturn, NeoGeo, Amiga, FM Towns, X68000, PC-98, Pico...
    │       ├── UdfParser      ─→  PS3, Nuon
    │       ├── XdvdfsParser   ─→  Xbox, Xbox 360
    │       ├── ThreeDoParser  ─→  3DO
    │       ├── CDiFsParser    ─→  CD-i
    │       ├── HfsParser      ─→  Pippin (HFS/HFS+)
    │       ├── PcFxIsoParser  ─→  PC-FX (tolerant ISO 9660)
    │       └── PlayStationParsers (CD-ROM XA)
    └── Virtual exports: CUE/BIN, CUE/ISO, CUE/BIN/WAV, CUE/ISO/WAV, SingleFile ISO
```

---

## Dependencies

| Package | Version | Description |
|---------|---------|-------------|
| [CHDSharp](https://www.nuget.org/packages/CHDSharp) | 1.2.0 | MAME CHD format reader |
| [Microsoft.SourceLink.GitHub](https://www.nuget.org/packages/Microsoft.SourceLink.GitHub) | 10.0.301 | Source link for NuGet debugging |

---

## License

MIT -- see [LICENSE.txt](LICENSE.txt) for details.

**Author:** [Peterson Fernandes (drpetersonfernandes)](https://github.com/purelogiccode)
