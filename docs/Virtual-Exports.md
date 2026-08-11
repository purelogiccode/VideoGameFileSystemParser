# Virtual CUE/BIN/ISO/WAV Exports

The library can present a CHD image as a **virtual CUE sheet** with BIN/ISO/WAV files — no conversion to disk required. Mount the image with a `GenericCue*` console type and the resulting `.cue`, `.bin`/`.iso`, and per-track `.wav` files appear as entries in the virtual file tree, readable through the normal `ReadFile` API.

---

## Export modes

| ConsoleType | Output | Data sector size | Audio |
|---|---|---|---|
| `GenericCueBin2352` | `.cue` + `.bin` | 2352 (raw) | inside `.bin` |
| `GenericCueBin2048` | `.cue` + `.bin` | 2048 (cooked) | inside `.bin` (still 2352-byte sectors) |
| `GenericCueIso2352` | `.cue` + `.iso` | 2352 | inside `.iso` |
| `GenericCueIso2048` | `.cue` + `.iso` | 2048 | inside `.iso` |
| `GenericCueBinWav2352` | `.cue` + `.bin` + `.wav` per audio track | 2352 | separate `.wav` files |
| `GenericCueBinWav2048` | `.cue` + `.bin` + `.wav` per audio track | 2048 | separate `.wav` files |
| `GenericCueIsoWav2352` | `.cue` + `.iso` + `.wav` per audio track | 2352 | separate `.wav` files |
| `GenericCueIsoWav2048` | `.cue` + `.iso` + `.wav` per audio track | 2048 | separate `.wav` files |

> **Special case:** `MountAndParse(ConsoleType.PcEngineCd)` and `ConsoleType.PcFx` additionally build a virtual CUE/BIN export (2352 mode) alongside the parsed track files, so the raw disc can still be re-assembled.

---

## Generated files

Mounting, for example, `game.chd` with `GenericCueBinWav2352` produces:

| Virtual file | Contents |
|---|---|
| `\game.cue` | CUE sheet with `FILE`/`TRACK`/`INDEX` statements for every track |
| `\game.bin` | Data tracks (raw 2352-byte sectors) |
| `\game_TrackNN.wav` | One WAV per audio track — `game_Track01.wav`, `game_Track02.wav`, ... (two-digit, zero-padded). 44-byte PCM header, 16-bit 44.1 kHz stereo |

Names derive from the CHD file name (e.g. `game.chd` → `game.cue`).

---

## Sector-size rules (important)

- **Data tracks** use the mode's sector size: 2048 bytes for the `*2048` variants, otherwise `min(UnitBytes, 2352)`.
- **Audio tracks inside BINARY files always use 2352-byte sectors**, even in the 2048-cooked variants — this keeps audio track boundaries exact on real CD hardware. (Only `GenericCueBin2048`-style cooked exports where the raw unit is below 2352 deviate.)
- **WAV exports** contain exactly the audio frames of the track: the stored pregap silence is skipped and the file starts at `INDEX 01 00:00:00`. Each WAV has a standard 44-byte RIFF/PCM header.

## CUE track modes

The CUE sheet writes `MODE1/2352`, `MODE2/2352`, `MODE1/2048`, `MODE2/2048`, etc. according to the **actual sector size used for that track** and the track type from the CHD metadata (MODE1/MODE2/CDI detection). Audio tracks are written as `AUDIO`.

---

## Why use virtual exports?

1. **No conversion** — a CHD stays a CHD on disk; the CUE/BIN/ISO/WAV materializes only when you read it.
2. **Emulator compatibility** — many emulators accept CUE/BIN or CUE/ISO but not CHD. Mount + read the virtual files into a RAM disk or extract them to disk.
3. **Selective extraction** — extract only the `.cue` + data file, or only a specific WAV track, without reading the whole image.
4. **PC Engine CD / PC-FX** — get a standard CUE/BIN of the disc even though no file system exists.

---

## Example: extract a CUE/BIN set to disk

```csharp
using VideoGameFileSystemParser.Models;
using VideoGameFileSystemParser.Parsers;

using var container = new ChdContainer(@"C:\ROMs\game.chd");
if (!container.MountAndParse(ConsoleType.GenericCueBin2352))
{
    Console.WriteLine("Mount failed.");
    return;
}

foreach (var entry in container.Entries)
{
    if (entry.IsDirectory) continue;

    var outPath = Path.Combine(@"C:\out", entry.Name);
    using var fs = File.Create(outPath);
    var buffer = new byte[256 * 1024];
    ulong offset = 0;
    while (offset < entry.Size)
    {
        int read = container.ReadFile(entry, offset, buffer, 0, buffer.Length);
        if (read <= 0) break;
        fs.Write(buffer, 0, read);
        offset += (ulong)read;
    }
    Console.WriteLine($"Extracted {entry.Name} ({entry.Size:N0} bytes)");
}
```

## Example: inspect the generated CUE sheet

```csharp
container.MountAndParse(ConsoleType.GenericCueBinWav2352);

var cueFile = container.FindFile("\\game.cue");
if (cueFile is not null)
{
    var cueData = new byte[cueFile.Size];
    container.ReadFile(cueFile, 0, cueData, 0, cueData.Length);
    Console.WriteLine(Encoding.ASCII.GetString(cueData));
}
```

---

## Behavior notes

- `MountAndParse` with a `GenericCue*` type never touches a file system parser — the tree consists purely of the virtual export files.
- Reading the `.cue`/`.bin`/`.iso`/`.wav` entries uses the same `ReadFile` API as real files; the bytes are generated on demand.
- The virtual files honor `TrackInfo` layout: track order, pregaps/postgaps, and per-track sector modes come straight from the CHD's track metadata.
- Extraction is byte-exact: a `.bin` extracted from a virtual export can be used with its `.cue` in any standards-compliant emulator or burning tool.

---

*Previous: [Supported Consoles](Supported-Consoles.md) · Next: [API Reference](API-Reference.md) · [Back to Home](Home.md)*
