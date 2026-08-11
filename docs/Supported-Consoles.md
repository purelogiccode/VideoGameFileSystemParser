# Supported Consoles

The library covers **31+ console and disc formats** through a mix of standard file system parsers, fallback chains, signature scanning, and virtual exports.

---

## Console reference table

| Console | ConsoleType | File System | Primary Parser | Fallback Parser(s) | File Tree | Notes |
|---|---|---|---|---|---|---|
| 3DO | `ThreeDo` | Opera FS | `ThreeDoParser` | ISO 9660 | Yes | Custom Opera FS; ISO fallback for some discs |
| Amiga CD | `AmigaCd` | ISO 9660 | `Iso9660Parser` | -- | Yes | Standard ISO 9660 |
| Amiga CD32 | `AmigaCd32` | ISO 9660 | `Iso9660Parser` | -- | Yes | Standard ISO 9660 |
| Amiga CDTV | `AmigaCdtv` | ISO 9660 | `Iso9660Parser` | -- | Yes | Standard ISO 9660 |
| Apple Bandai Pippin | `Pippin` | HFS / HFS+ | `HfsParser` | UDF, ISO 9660 | Yes | Macintosh Hierarchical FS; tries HFS first, then HFS+, UDF, ISO |
| CD-i | `CDi` | CD-i Green Book | `CDiFsParser` | ISO 9660 | Yes | Custom CD-i FS with interleaved data support |
| Dreamcast | `Dreamcast` | ISO 9660 + IP.BIN | `Iso9660Parser` | -- | Yes | Prefers track with `SEGA SEGAKATANA` boot signature |
| FM Towns | `FmTowns` | ISO 9660 | `Iso9660Parser` | -- | Yes | Standard ISO 9660 |
| Generic ISO 9660 | `GenericIso9660` | ISO 9660 | `Iso9660Parser` | -- | Yes | Standard ISO 9660 / High Sierra |
| Generic ISO Raw 2352 | `GenericIsoRaw2352` | Raw sectors | `GenericIsoRawParser` | -- | No | Exposes entire image as `image.iso`, 2352-byte units |
| Generic ISO Raw 2048 | `GenericIsoRaw2048` | Raw sectors | `GenericIsoRawParser` | -- | No | Exposes entire image as `image.iso`, 2048-byte units |
| NeoGeo CD | `NeoGeoCd` | ISO 9660 | `Iso9660Parser` | -- | Yes | Standard ISO 9660 |
| Nuon | `Nuon` | UDF / ISO 9660 | `UdfParser` | ISO 9660 | Yes | VM Labs Nuon DVD; tries UDF first |
| PC Engine CD | `PcEngineCd` | **Non-standard** | `PcEngineCdParser` | ISO 9660, raw track | **Partial** | See [PC Engine CD observations](#pc-engine-cd-turbografx-cd) |
| PC-FX | `PcFx` | **Non-standard** | `PcFxIsoParser` | ISO 9660, raw track | **Partial** | See [NEC PC-FX observations](#nec-pc-fx) |
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
| CUE/BIN (2352) | `GenericCueBin2352` | Virtual export | -- | -- | Virtual | Virtual CUE sheet + BIN with 2352-byte raw sectors |
| CUE/BIN (2048) | `GenericCueBin2048` | Virtual export | -- | -- | Virtual | Virtual CUE sheet + BIN with 2048-byte cooked sectors |
| CUE/ISO (2352) | `GenericCueIso2352` | Virtual export | -- | -- | Virtual | Virtual CUE sheet + ISO with 2352-byte sectors |
| CUE/ISO (2048) | `GenericCueIso2048` | Virtual export | -- | -- | Virtual | Virtual CUE sheet + ISO with 2048-byte sectors |
| CUE/BIN/WAV (2352) | `GenericCueBinWav2352` | Virtual export | -- | -- | Virtual | Virtual CUE + BIN data (2352) + separate WAV audio tracks |
| CUE/BIN/WAV (2048) | `GenericCueBinWav2048` | Virtual export | -- | -- | Virtual | Virtual CUE + BIN data (2048) + separate WAV audio tracks |
| CUE/ISO/WAV (2352) | `GenericCueIsoWav2352` | Virtual export | -- | -- | Virtual | Virtual CUE + ISO data (2352) + separate WAV audio tracks |
| CUE/ISO/WAV (2048) | `GenericCueIsoWav2048` | Virtual export | -- | -- | Virtual | Virtual CUE + ISO data (2048) + separate WAV audio tracks |

---

## Parsing logic per console

### Standard ISO 9660 parsers

These consoles use a straightforward `Iso9660Parser` on the first data track found:

| Console | Parser Chain |
|---|---|
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

### Parsers with fallback chains

| Console | Step 1 | Step 2 | Step 3 | Step 4 |
|---|---|---|---|---|
| **PlayStation 3** | `UdfParser` | `Iso9660Parser` | -- | -- |
| **Nuon** | `UdfParser` | `Iso9660Parser` | -- | -- |
| **Sharp X68000** | `Iso9660Parser` | `UdfParser` | -- | -- |
| **Apple Bandai Pippin** | `HfsParser` (HFS) | `HfsParser` (HFS+) | `UdfParser` | `Iso9660Parser` |
| **CD-i** | `CDiFsParser` (CD-i Green Book) | `Iso9660Parser` | -- | -- |
| **3DO** | `ThreeDoParser` (Opera FS) | `Iso9660Parser` | -- | -- |

### Special parsers

| Console | Logic |
|---|---|
| **Dreamcast** | Scans all data tracks for the `SEGA SEGAKATANA` IP.BIN boot signature. Parses the track containing the signature with `Iso9660Parser`, preferring tracks with IP.BIN over tracks without. |
| **Xbox / Xbox 360** | Uses `XdvdfsParser` (Xbox DVD File System) on the first data track. |
| **Generic ISO Raw** | No parsing. Exposes the entire disc image as a single file named `image.iso` with raw passthrough. |
| **Generic CUE/BIN/ISO/WAV** | No file system parsing. Builds a virtual CUE sheet and exposes virtual `.cue`, `.bin`/`.iso`, and `.wav` files for direct extraction. |

### Non-standard parsers (PC Engine CD, PC-FX)

These consoles do **not** use a standard file system — see the observations below for details.

| Console | Step 1 | Step 2 | Step 3 |
|---|---|---|---|
| **PC Engine CD** | Locate boot signature (`PC Engine CD-ROM SYSTEM`, `GAMES EXPRESS CD CARD`, or `PC ENGINE`) | Attempt `Iso9660Parser` with adjusted data area start | Expose raw data tracks as `TRACKnn.iso` files |
| **PC-FX** | Attempt `PcFxIsoParser` (tolerant ISO 9660 with byte-offset VD scanning) per data track | Attempt standard `Iso9660Parser` | Expose raw data tracks as `TRACKnn.iso` files |

---

## System observations

### PC Engine CD (TurboGrafx-CD)

**The NEC PC Engine CD does not use a standard file system.**

Most PC Engine CD / TurboGrafx-CD games store game data as raw binary streams directly across the data sectors of the disc. Instead of a file system with directories and named files, the game code uses the CD-ROM BIOS hardware to seek directly to specific Logical Block Addresses (LBA) and read sectors into RAM.

**How this library handles it:**

1. **Boot signature detection** — the parser scans the data track for one of three known boot signatures:
   - `PC Engine CD-ROM SYSTEM` (standard CD-ROM system cards)
   - `GAMES EXPRESS CD CARD` (Games Express titles)
   - `PC ENGINE` (alternative identifier)
2. **Data area start detection** — identifies where the actual data begins by:
   - checking for pregap metadata (`PGTYPE:V` in track metadata)
   - scanning for the first non-zero sector (up to 600 sectors)
   - testing multiple candidate offsets (track start, pregap-adjusted start, first non-zero sector)
3. **ISO 9660 attempt** — if a boot signature is found, the parser attempts an ISO 9660 parse starting at the detected data area. Some CD-ROM System Card discs and later Super CD-ROM² titles *do* include a minimal ISO 9660 structure.
4. **Raw track fallback** — if ISO 9660 parsing fails, each data track is exposed as a raw file named `TRACKnn.iso`, allowing direct sector-level access via `ReadFile()`.

**Practical implications:**

- Most PC Engine CD games will show as raw track files, not a browsable directory tree.
- To extract specific game assets, you need game-specific tools or emulator debuggers to determine which LBAs contain which data.
- Audio tracks (Red Book CDDA) are handled separately by the CD controller and are not part of the data file system.

### NEC PC-FX

**The NEC PC-FX uses a non-standard executable format, even when ISO 9660 is present on the disc.**

The PC-FX features a V810 RISC 32-bit processor and uses CD-ROM media. While some PC-FX discs include ISO 9660 structures, the actual game executable and data bypass the file system entirely.

**How this library handles it:**

1. **Tolerant ISO 9660 parsing (`PcFxIsoParser`)** — a specialized ISO 9660 parser that:
   - scans for Volume Descriptor signatures (`CD001` or `CDROM`) at both standard byte offsets and arbitrary positions within raw sectors (handles dumps where CD sync headers were not fully stripped)
   - tests multiple candidate LBA offsets for the root directory (track start, LBA 150, LBA 0, and a scan of up to 512 sectors)
   - uses continue-on-error directory record parsing (skips invalid records instead of aborting), matching the behavior of C++ DiscImageCreator tools
2. **Standard ISO 9660 fallback** — if the tolerant parser fails, a standard `Iso9660Parser` is tried.
3. **Raw track fallback** — if neither ISO parser succeeds, data tracks are exposed as `TRACKnn.iso` files.

**Practical implications:**

- Some PC-FX discs will parse with a full directory tree (those with standard ISO 9660 layout for supplementary content like movie previews or bitmap images).
- The actual PC-FX game executable (V810 binary) is loaded directly by the PC-FX BIOS using raw LBA addressing, bypassing any file system.
- Game assets are typically packed into proprietary archive structures with offset tables embedded in the main executable. Extracting individual assets requires game-specific unpacker tools.
- Audio tracks are standard Red Book CDDA and handled by the CD controller directly.

### Why these systems are different

Most other consoles in this library (PlayStation, Saturn, Dreamcast, Xbox, etc.) use a standard file system (ISO 9660, UDF, XDVDFS, etc.) that provides a hierarchical directory tree of named files. The PC Engine CD and PC-FX are exceptions where:

- the **boot code** is a raw binary at a known sector offset, not a file in a directory;
- **game assets** are addressed by direct sector number (LBA), not by file path;
- **custom archive formats** are used when files are grouped together, requiring game-specific knowledge to unpack.

This is why tools like [Aaru Data Preservation Suite](https://github.com/aaru-dps/Aaru) list these formats under "identification and information only" — they can read the binary header and identify the disc, but cannot extract a traditional folder structure.

---

## Display names and CLI aliases

`ConsoleTypeRegistry.All` is the authoritative list used by UIs and CLIs. Abbreviated reference:

| Display name | Aliases |
|---|---|
| 3DO | `3do` |
| Amiga CD / CD32 / CDTV | `amigacd`, `amigacd32`, `amigacdtv` |
| CD-i | `cdi` |
| FM Towns | `fmtowns` |
| Neo Geo CD | `neogeocd` |
| Nuon | `nuon` |
| PC Engine CD | `pcengine` |
| PC-98 | `pc98` |
| PC-FX | `pcfx` |
| Pico / Pippin | `pico`, `pippin` |
| PlayStation (Auto) | `psauto` |
| PS1 / PS2 / PS3 / PSP | `ps1`, `ps2`, `ps3`, `psp` |
| ISO RAW 2352 (PS3/Xbox/Generic) | `isoraw2352` |
| ISO RAW 2048 | `isoraw2048` |
| Sega Dreamcast / Genesis / Saturn | `segadreamcast`, `segagenesis`, `segasaturn` |
| X68000 | `x68000` |
| Xbox / Xbox 360 | `xbox`, `xbox360` |
| ISO 9660 | `iso9660` |
| CUE/ISO RAW 2352 / 2048 | `cueiso2352`, `cueiso2048` |
| CUE/ISO/WAV RAW 2352 / 2048 | `cueisowav2352`, `cueisowav2048` |
| CUE/BIN RAW 2352 / 2048 | `cuebin2352`, `cuebin2048` |
| CUE/BIN/WAV RAW 2352 / 2048 | `cuebinwav2352`, `cuebinwav2048` |

> Several display names map to the same `ConsoleType` (e.g. "PS3 ISO RAW 2352", "Xbox ISO RAW 2352" and "ISO RAW 2352" all resolve to `GenericIsoRaw2352`). Alias lookup always returns the canonical type.

---

*Previous: [Usage Guide](Usage-Guide.md) · Next: [Virtual CUE/BIN/ISO/WAV Exports](Virtual-Exports.md) · [Back to Home](Home.md)*
