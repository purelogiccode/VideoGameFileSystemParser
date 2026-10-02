# API Reference

Complete public API surface of **VideoGameFileSystemParser 1.3.0**. All signatures below are copied from the compiled source.

**Namespaces:**

- `VideoGameFileSystemParser` — global usings only (no public types)
- `VideoGameFileSystemParser.Models` — `ConsoleType`, `ConsoleInfo`, `ConsoleTypeInfo`, `ConsoleTypeRegistry`, `FileEntry`, `FileExtent`, `FsNode`, `FsExtent`, `FsNodeType`, `TrackInfo`
- `VideoGameFileSystemParser.Parsers` — `ChdContainer`, `ParserFactory`, `SectorReader`
- `VideoGameFileSystemParser.Interfaces` — `IConsoleParser`

---

## ChdContainer

**Namespace:** `VideoGameFileSystemParser.Parsers`
**Implements:** `IDisposable`, `IAsyncDisposable`

Opens and manages a CHD disc image, providing file system access via console-specific parsers or virtual CUE/BIN export for raw image access. This is the primary entry point of the library.

### Constructor

```csharp
public ChdContainer(string chdPath)
```

### Properties

| Property | Type | Description |
|---|---|---|
| `Entries` | `IReadOnlyList<FileEntry>` | Read-only list of all file and directory entries in the parsed tree. |
| `VolumeName` | `string` (get; private set) | Volume name, derived from the CHD file name. |
| `VolumeSize` | `ulong` (get; private set) | Total size of the disc image in bytes. |
| `HasDataTracks` | `bool` (get; private set) | Whether the CHD contains at least one data track. |
| `UnitBytes` | `uint` (get; private set) | Bytes per sector unit (e.g. 2048 or 2352). |
| `HunkBytes` | `uint` (get; private set) | Bytes per compressed hunk. |
| `ConsoleType` | `ConsoleType` (get; set) | The console type used for parsing this image. |

### Methods

#### Open

```csharp
public bool Open(ConsoleType consoleType)
```

Opens the CHD file and initializes the reader pool, filling the metadata properties — **without** parsing a file system. Call this if you only need low-level access or metadata.

| Parameter | Type | Description |
|---|---|---|
| `consoleType` | `ConsoleType` | The console type to configure the reader for. |

**Returns:** `true` if the CHD was opened successfully; otherwise `false`.

#### MountAndParse

```csharp
public bool MountAndParse(ConsoleType consoleType)
```

Opens the CHD, creates the appropriate parser (or virtual export builder), parses the file system, and builds the entry tree. The main method most users should call.

| Parameter | Type | Description |
|---|---|---|
| `consoleType` | `ConsoleType` | The console type to parse the image as. |

**Returns:** `true` if parsing succeeded; otherwise `false`.

Behavior by console type family:

- `GenericCue*` (8 modes) → builds a virtual CUE export tree (see [Virtual Exports](Virtual-Exports.md)).
- `GenericIsoRaw2352` / `GenericIsoRaw2048` → single raw `image.iso` passthrough entry.
- `PcEngineCd` / `PcFx` → parses tracks **and** additionally builds a virtual CUE/BIN (2352) export.
- All other types → `ParserFactory.CreateParser` + `Parse`.

#### BuildFromFsNode

```csharp
public void BuildFromFsNode(FsNode rootNode)
```

Builds the `Entries` tree from a parsed `FsNode` root. Primarily for library-internal use and advanced custom parsers implementing `IConsoleParser`.

#### FindFile

```csharp
public FileEntry? FindFile(string path)
```

Finds a file or directory entry by its full path (e.g. `\GAME\DATA.BIN`). Forward slashes are also accepted.

**Returns:** the matching `FileEntry`, or `null` if not found.

#### TryFindFile

```csharp
public bool TryFindFile(string path, out FileEntry? entry, out string? error)
```

Attempts to find an entry, providing an error message on failure.

**Returns:** `true` on success; `false` with `error` populated (e.g. `"Path not found: \NONEXISTENT.TXT"`).

#### ListDirectory

```csharp
public IEnumerable<FileEntry> ListDirectory(string path)
```

Enumerates the child entries of a directory. Use `"\\"` or `"/"` for the root.

#### ReadFile

```csharp
public int ReadFile(FileEntry entry, ulong offset, byte[] buffer, int bufOffset, int count)
```

Reads data from a file entry at the specified offset into the provided buffer. Handles extent mapping, interleaved (CD-ROM XA) files, embedded data, and raw passthrough entries.

| Parameter | Type | Description |
|---|---|---|
| `entry` | `FileEntry` | The file entry to read from. |
| `offset` | `ulong` | Byte offset within the file to start reading. |
| `buffer` | `byte[]` | Destination buffer. |
| `bufOffset` | `int` | Offset within the destination buffer to begin writing. |
| `count` | `int` | Maximum number of bytes to read. |

**Returns:** the number of bytes actually read (`0` at end of file or for directories).

#### Dispose

```csharp
public void Dispose()
```

Releases all resources, including all readers and the underlying CHD file.

#### DisposeAsync

```csharp
public ValueTask DisposeAsync()
```

Asynchronously disposes the container. Supports `await using` syntax.

---

## ParserFactory

**Namespace:** `VideoGameFileSystemParser.Parsers`
**Type:** `public static class`

Provides factory methods to create console-specific parsers and enumerate supported consoles.

### CreateParser

```csharp
public static IConsoleParser? CreateParser(ConsoleType type, SectorReader reader)
```

Creates a parser instance for the specified console type.

| Parameter | Type | Description |
|---|---|---|
| `type` | `ConsoleType` | The console type to create a parser for. |
| `reader` | `SectorReader` | The sector reader to use for disc access. |

**Returns:** an `IConsoleParser` implementation, or `null` if the console type is not supported (e.g. the `Generic*` virtual/raw types).

### GetAllSupportedConsoles

```csharp
public static IEnumerable<ConsoleInfo> GetAllSupportedConsoles()
```

Returns the list of all supported console types with their display names, derived from `ConsoleTypeRegistry.All`.

---

## IConsoleParser

**Namespace:** `VideoGameFileSystemParser.Interfaces`

Defines the contract for a console-specific file system parser. Implement this to add custom console support.

### Methods

| Method | Returns | Description |
|---|---|---|
| `GetConsoleType()` | `ConsoleType` | Returns the console type this parser handles. |
| `GetConsoleName()` | `string` | Returns the human-readable console name. |
| `Parse(FsNode rootNode)` | `bool` | Parses the file system and populates the root node. Returns `true` on success. |
| `ParseTrack(FsNode rootNode, TrackInfo track)` | `bool` | Parses the file system from a specific track. Returns `true` on success. |

### Properties

| Property | Type | Description |
|---|---|---|
| `ForceMode` | `bool` (get; set) | When `true`, forces parsing even when verification fails. |

---

## ConsoleType

**Namespace:** `VideoGameFileSystemParser.Models`
**Type:** `public enum`

Identifies the target console or disc image format.

| Value | Description |
|---|---|
| `Unknown` | Unknown or unset. |
| `AmigaCd` | Amiga CD format. |
| `AmigaCd32` | Amiga CD32 format. |
| `AmigaCdtv` | Amiga CDTV format. |
| `CDi` | Philips CD-i format. |
| `Dreamcast` | Sega Dreamcast GD-ROM format. |
| `FmTowns` | Fujitsu FM Towns format. |
| `NeoGeoCd` | SNK NeoGeo CD format. |
| `Nuon` | VM Labs Nuon DVD format. |
| `PcEngineCd` | NEC PC Engine CD format. |
| `PcFx` | NEC PC-FX format. |
| `Pc98` | NEC PC-98 format. |
| `Pico` | Sega Pico format. |
| `Pippin` | Apple Bandai Pippin format. |
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
| `GenericIso9660` | Generic ISO 9660 file system. |
| `GenericIsoRaw2352` | Raw sector passthrough (no file system parsing), 2352-byte units. |
| `GenericIsoRaw2048` | Raw sector passthrough (no file system parsing), 2048-byte units. |
| `GenericCueBin2352` | Generic CUE/BIN image with 2352-byte sectors. |
| `GenericCueBin2048` | Generic CUE/BIN image with 2048-byte sectors. |
| `GenericCueIso2352` | Generic CUE/ISO image with 2352-byte sectors. |
| `GenericCueIso2048` | Generic CUE/ISO image with 2048-byte sectors. |
| `GenericCueBinWav2352` | Generic CUE/BIN with WAV audio tracks, 2352-byte sectors. |
| `GenericCueBinWav2048` | Generic CUE/BIN with WAV audio tracks, 2048-byte sectors. |
| `GenericCueIsoWav2352` | Generic CUE/ISO with WAV audio tracks, 2352-byte sectors. |
| `GenericCueIsoWav2048` | Generic CUE/ISO with WAV audio tracks, 2048-byte sectors. |

> ⚠️ Enum numeric values are **not** a stable contract — resolve types through `ConsoleTypeRegistry` in host applications.

---

## ConsoleTypeRegistry

**Namespace:** `VideoGameFileSystemParser.Models`
**Type:** `public static class`

Single source of truth for console type display names and CLI aliases. Host applications (e.g. CHDMounter) must resolve console types exclusively through this registry — numeric indexes are not supported. Alias lookup is case-insensitive and always returns the canonical type; multiple display entries may resolve to the same `ConsoleType`.

### Members

| Member | Returns | Description |
|---|---|---|
| `All` | `IReadOnlyList<ConsoleTypeInfo>` | All supported console/format entries, ordered as displayed in the UI and help text (31 entries). |
| `Parse(string? alias)` | `ConsoleType` | Resolves a console type from a CLI alias (case-insensitive), e.g. `"ps2"`, `"cuebin2352"`. Returns `ConsoleType.Unknown` if not recognized. |
| `GetDisplayName(ConsoleType type)` | `string` | Returns the primary display name for a console type (the first registered entry); falls back to `type.ToString()`. |
| `GetAliases(ConsoleType type)` | `IReadOnlyList<string>` | Returns all aliases registered for a console type, deduplicated case-insensitively. |

```csharp
ConsoleType type = ConsoleTypeRegistry.Parse("isoraw2352"); // ConsoleType.GenericIsoRaw2352
```

---

## ConsoleInfo

**Namespace:** `VideoGameFileSystemParser.Models`

```csharp
public sealed record ConsoleInfo(ConsoleType Type, string Name);
```

| Property | Type | Description |
|---|---|---|
| `Type` | `ConsoleType` | The console type identifier. |
| `Name` | `string` | The human-readable name (e.g. "PlayStation 2", "Xbox"). |

---

## ConsoleTypeInfo

**Namespace:** `VideoGameFileSystemParser.Models`

```csharp
public sealed record ConsoleTypeInfo(ConsoleType Type, string DisplayName, IReadOnlyList<string> Aliases);
```

| Property | Type | Description |
|---|---|---|
| `Type` | `ConsoleType` | The console type identifier. |
| `DisplayName` | `string` | The human-readable name shown in the UI (e.g. "CUE/BIN RAW 2352"). |
| `Aliases` | `IReadOnlyList<string>` | The command-line aliases that resolve to this console type (case-insensitive). |

---

## FileEntry

**Namespace:** `VideoGameFileSystemParser.Models`

Represents a file or directory entry in the virtual file system. All properties are get/set.

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | The file or directory name (without path). |
| `FullPath` | `string` | The full path with leading backslash (e.g. `\GAME\DATA.BIN`). |
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

## FileExtent

**Namespace:** `VideoGameFileSystemParser.Models`
**Type:** `public struct`

Represents a contiguous data extent with a starting LBA and size.

| Property | Type | Description |
|---|---|---|
| `Lba` | `uint` | The starting logical block address. |
| `Size` | `ulong` | The size in bytes. |

---

## FsNode

**Namespace:** `VideoGameFileSystemParser.Models`

Represents a node in the parsed file system tree. Used internally by parsers and exposed for advanced scenarios (custom `IConsoleParser` implementations). All properties are get/set.

| Property | Type | Description |
|---|---|---|
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

## FsExtent

**Namespace:** `VideoGameFileSystemParser.Models`
**Type:** `public struct`

Represents a contiguous data extent within the file system tree.

| Property | Type | Description |
|---|---|---|
| `Lba` | `uint` | The starting logical block address. |
| `Size` | `ulong` | The size in bytes. |

---

## FsNodeType

**Namespace:** `VideoGameFileSystemParser.Models`
**Type:** `public enum`

Identifies the type of a file system node.

| Value | Description |
|---|---|
| `File = 0` | A regular file. |
| `Directory = 4` | A directory. |
| `Symlink = 12` | A symbolic link. |

---

## TrackInfo

**Namespace:** `VideoGameFileSystemParser.Models`

Represents metadata for a single disc image track. All properties are get/set.

| Property | Type | Description |
|---|---|---|
| `Index` | `int` | The one-based track index within the disc. |
| `StartLba` | `uint` | The LBA of the first sector of the track. |
| `ChdOffset` | `uint` | The frame offset within the CHD hunk stream. |
| `Frames` | `uint` | The number of frames in this track. |
| `TrackType` | `string` | The track type string (e.g. `MODE1/2352`, `AUDIO`). |
| `IsDataTrack` | `bool` | Whether this track contains data (vs. audio). |
| `Pregap` | `uint` | The number of pregap frames before this track. |
| `Postgap` | `uint` | The number of postgap frames after this track. |
| `Metadata` | `string` | The raw metadata string from the CHD for this track. |

---

## SectorReader

**Namespace:** `VideoGameFileSystemParser.Parsers`
**Implements:** `IDisposable`

Provides low-level sector read access to a CHD file, handling hunk caching, sector offset detection, byte-swapping for audio tracks, and descrambling. Used internally by parsers; most members are `internal`.

### Constructor

```csharp
public SectorReader(ChdFile chd, uint unitBytes)
```

(`ChdFile` is the CHDSharp type.)

### Public members

| Member | Type / Signature | Description |
|---|---|---|
| `SectorHeaderOffset` | `uint` (get; private set) | Detected sector header offset (0 or 16). |
| `SyncOffset` | `uint` (get; private set) | Detected sync pattern offset. |
| `HunkBytes` | `uint` (get) | Bytes per compressed hunk (`_chd.HunkBytes`). |
| `Tracks` | `List<TrackInfo>` (get) | Track list parsed from the CHD metadata. |
| `SetTrack` | `void SetTrack(TrackInfo? track, bool locked = false)` | Selects the active track for LBA mapping. |
| `Reset` | `void Reset()` | Resets track selection to the default. |
| `ReadSector` | `bool ReadSector(uint lba, byte[] outBuffer, int outOffset = 0)` | Reads one 2048-byte cooked sector. |
| `ReadSector` | `byte[]? ReadSector(uint lba)` | Reads one 2048-byte cooked sector (allocation). |
| `ReadRawSector` | `bool ReadRawSector(uint lba, out byte[] rawSector)` | Reads one raw sector of `UnitBytes` length. |
| `GetSectorScramble` | `static ReadOnlySpan<byte> GetSectorScramble()` | The sector descrambling table. |
| `GetSectorDataOffset` | `static uint GetSectorDataOffset(TrackInfo? track)` | Data offset within a sector: 16 (MODE1), 24 (MODE2/CDI), 0 (audio). |
| `Dispose` | `void Dispose()` | Releases resources, including the cached hunk buffer. |

> **Note:** `UnitBytes` and `TotalBytes` are `internal` on `SectorReader` — use the public `UnitBytes` / `VolumeSize` properties on `ChdContainer` instead.

---

*Previous: [Virtual CUE/BIN/ISO/WAV Exports](Virtual-Exports.md) · Next: [Architecture](Architecture.md) · [Back to Home](Home.md)*
