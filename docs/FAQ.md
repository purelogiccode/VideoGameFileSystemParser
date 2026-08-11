# FAQ

Frequently asked questions about VideoGameFileSystemParser.

---

## General

### Where can I see the library in action? Where are the unit tests?

Check out [**CHDMounter**](https://github.com/drpetersonfernandes/CHDMounter) — the companion application where this library is **battle-tested in production**:

- It mounts CHD disc images as virtual drives (via WinFsp), exercising every parser and export mode in real-world scenarios.
- The library's **unit test suite** lives there in [`CHDMounter.Core.Tests`](https://github.com/drpetersonfernandes/CHDMounter/tree/master/CHDMounter.Core.Tests) — parser tests (including per-console integration tests), console registry tests, and more.
- Anyone can build it and mount their own CHDs to try the library hands-on, no code required.

### What image formats are supported?

- **CHD** (MAME Compressed Hunks of Data, V1–V5) — the primary format, via CHDSharp.
- **ISO** images and raw sector data are supported through the same `ChdContainer` API — the sector reader handles cooked 2048-byte and raw 2352-byte dumps, detects sector header offsets automatically, and can read track-aware layouts.

### Which consoles are supported?

31+ formats: PlayStation (PS1/PS2/PS3/PSP/auto), Xbox, Xbox 360, Dreamcast, Saturn, Genesis CD, 3DO, CD-i, Pippin (HFS/HFS+), PC Engine CD, PC-FX, PC-98, FM Towns, NeoGeo CD, Nuon, Pico, X68000, Amiga CD/CD32/CDTV, generic ISO 9660, raw passthrough, and virtual CUE/BIN/ISO/WAV exports. See [Supported Consoles](Supported-Consoles.md).

### What file systems does the library parse?

ISO 9660 (incl. High Sierra and CD-ROM XA interleaving), UDF, XDVDFS, HFS/HFS+, Opera FS, CD-i Green Book, plus tolerant/non-standard handling for PC Engine CD and PC-FX.

### Is the library cross-platform?

Yes — it targets `net8.0`, `net9.0`, and `net10.0` and has no platform-specific dependencies. It is a pure managed library (CHDSharp is also cross-platform).

---

## Usage

### `MountAndParse` returns `false` — what now?

1. Verify the CHD itself opens — check `Open(consoleType)` separately, and inspect `VolumeSize`/`HasDataTracks`.
2. Try a different `ConsoleType` — many discs parse under several types (e.g. PS1 discs under `Ps2`, DVD-based PS3 games under `Ps3` *and* `GenericIso9660`).
3. Try raw mode (`GenericIsoRaw2352`) to confirm the image is readable at all.
4. Multi-track images: make sure the right track is chosen — `Dreamcast` scans for IP.BIN, most others use the first data track.

### Why does my PC Engine CD game show `TRACKnn.iso` files?

Because the PC Engine CD has **no file system** — games read raw sectors directly by LBA. The parser detects the boot signature, tries ISO 9660 if present, and otherwise exposes each data track as a raw `TRACKnn.iso`. See [Supported Consoles → System observations](Supported-Consoles.md#pc-engine-cd-turbografx-cd).

### Why does my PC-FX game show `TRACKnn.iso` files?

Same story — the PC-FX executable bypasses ISO 9660 even when a directory tree exists on the disc. The tolerant `PcFxIsoParser` recovers a tree when the layout is standard; otherwise raw tracks are exposed. See [Supported Consoles → System observations](Supported-Consoles.md#nec-pc-fx).

### `ReadFile` returned fewer bytes than requested — is that an error?

Not necessarily. `ReadFile` returns the number of bytes actually read. A short read at the end of the file is normal. Check `entry.Size` and stop when the returned count is `0` or when your offset reaches `Size`.

### How do I read a whole file safely?

```csharp
var data = new byte[file.Size];
int read = container.ReadFile(file, 0, data, 0, data.Length);
if (read < file.Size) { /* truncated read — handle */ }
```

### Can I use the library asynchronously?

The container implements `IAsyncDisposable` (`await using`), and `ReadFile` is synchronous. For large files, read in chunks inside a background task. The parser itself has no async API — parsing is CPU + I/O bound and typically fast.

### How do I get the console type back after auto-detection?

`container.ConsoleType` is set by `MountAndParse` (e.g. `ConsoleType.PlayStation` resolves to the detected concrete type after a successful parse).

---

## Formats & compatibility

### What is the difference between `GenericIsoRaw2352` and `GenericIsoRaw2048`?

Both expose the whole disc as a single raw `image.iso` file. `2352` presents the image with 2352-byte unit sectors (raw CD frames), `2048` with 2048-byte cooked sectors. Choose based on what the downstream tool expects.

### Which CUE export mode should I use?

- 2352 modes → byte-exact raw dumps (CDRWIN-style), best for preservation and burning.
- 2048 modes → cooked data sectors, smaller files, compatible with tools that expect plain ISO data; audio stays 2352 inside BIN (correct per CUE spec).
- WAV modes → separate `.wav` per audio track, pregap skipped, starts at `INDEX 01`.

See [Virtual CUE/BIN/ISO/WAV Exports](Virtual-Exports.md).

### Do extracted WAV files include the pregap?

No. WAV exports skip the stored pregap silence and start at `INDEX 01 00:00:00` (fixed in v1.1.0).

### Can I extract a byte-exact BIN from the virtual export?

Yes — reading the virtual `.bin` entry yields the exact track bytes per the CUE sheet (audio tracks always 2352-byte sectors in BIN files). This is useful for re-burning or feeding into tools that don't accept CHD.

### Is `ConsoleType` enum ordering stable?

**No.** Numeric values are not a contract and changed between versions. Use `ConsoleType` members directly or `ConsoleTypeRegistry.Parse(alias)` for user input.

---

## Performance

### How fast is parsing?

Parsing reads only directory metadata (plus a boot-signature scan for Dreamcast/PC Engine), so mounting is quick even for large images. File reads go through the CHD hunk cache, so sequential reads are efficient.

### Why are random reads slow?

Random access into a compressed CHD may require decompressing a new hunk. Sequential reads amortize this. For random-heavy workloads, consider extracting the file(s) you need to disk first.

### Is the library thread-safe?

A `ChdContainer` is **not** thread-safe for concurrent reads — use one container per thread, or serialize access with a lock. Multiple containers on the same file are fine.

---

## Errors & troubleshooting

### I get "Path not found" from `TryFindFile`

Check the path format: leading `\`, backslashes as separators (`\GAME\DATA.BIN`). Forward slashes are accepted too. Directory names are case-sensitive as stored on the disc (ISO 9660 is typically uppercase).

### The parser succeeded but `Entries` is empty

Some images parse "successfully" but yield an empty tree (e.g. an ISO with a valid volume descriptor but no readable root). Try another console type or raw mode.

### CHD opens but `HasDataTracks` is false

The image is audio-only or the CHD metadata lacks data tracks. Virtual export modes still work — they will produce a CUE sheet with only AUDIO tracks.

---

*Previous: [Migration Guide](Migration-Guide.md) · Next: [Contributing](Contributing.md) · [Back to Home](Home.md)*
