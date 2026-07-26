# Plan for Future Parsers

Roadmap for expanding VideoGameFileSystemParser beyond the current 6 filesystem formats (ISO 9660, UDF, XDVDFS, Opera FS, CD-i FS, HFS/HFS+).

---

## Current State

| Filesystem Format | Consoles Covered |
|---|---|
| ISO 9660 | PS1, PS2, Dreamcast, Saturn, Neo Geo CD, Amiga CD/CD32, FM Towns, PC-98, X68000, Pico, PC Engine CD, PC-FX, PSP |
| UDF | PS3, Nuon |
| XDVDFS | Xbox, Xbox 360 |
| Opera FS | 3DO |
| CD-i FS | CD-i |
| HFS/HFS+ | Pippin |

---

## Phase 1: Nintendo Optical Discs

### GameCube / Wii — GCM Filesystem

- **Console types**: `GameCube`, `Wii`
- **Format**: GCM (GameCube image) / ISO
- **Filesystem**: Nintendo proprietary FST (File System Table)
- **Key structures**:
  - Disc header at offset `0x00` (game ID, disc number, region)
  - Apploader at `0x2440`
  - FST offset/size at `0x0424` / `0x0428`
  - FST entries: 12 bytes each (flags, name offset, offset, length)
  - Bi-endian (big-endian by default, little-endian for certain ARM/IOS areas on Wii)
- **Partitions (Wii only)**:
  - Partition table at disc offset `0x40000`
  - Each partition has a ticket, TMD, and data
  - AES-128-CBC encrypted content
  - Key derivation from common key + ticket key
- **Magic/signature**: Game ID string at `0x00` (e.g. `GFZE01`), `0xC23D9BB0` hash at `0x01C0`
- **Dependencies**: None (pure binary parsing)

### Wii U — WUD/WUX + WFS

- **Console type**: `WiiU`
- **Formats**: WUD (Wii U Disc, 23GB raw), WUX (compressed WUD)
- **Filesystem**: WFS (Wii U File System) inside encrypted partitions
- **Key structures**:
  - WUD header: 24 bytes, contains disc key hash
  - Partition table at offset `0x18000`
  - Each partition: encrypted with AES-128-CBC
  - WFS: B-tree directory structure, 4KB blocks
  - Cluster size: typically 8192 bytes
- **Magic/signature**: Partition table header magic
- **Dependencies**: AES decryption (System.Security.Cryptography)

### Nintendo Switch — NCA/XCI + PFS0/HFS0

- **Console type**: `NintendoSwitch`
- **Formats**: NCA (Nintendo Content Archive), XCI (gamecard dump)
- **Filesystems**:
  - **PFS0/INI0**: Partition FS, simple header + file entry table
  - **HFS0**: Hash FS, includes hash tree verification
  - **RomFS**: Read-only filesystem embedded in NCA content
  - **NCA FS**: Section-level filesystem with AES-CTR encryption + NCA header RSA signatures
- **Key structures**:
  - NCA header: 0x400 bytes, RSA-2048 signature, key area (encrypted), FS headers
  - XCI header: gamecard header + HFS0 root + partition entries
  - PFS0: magic `PFS0`, 0x18 header, file entries with name table
  - HFS0: magic `HFS0`, 0x18 header, hash-verified file entries
- **Magic/signature**: `PFS0`, `HFS0`, NCA magic at offset `0x200`
- **Dependencies**: AES, RSA (System.Security.Cryptography)

---

## Phase 2: Sony Modern Platforms

### PS Vita — PFS (SCEI Proprietary)

- **Console type**: `PsVita`
- **Format**: SVPKG / VPK (Vita Package), PFS image
- **Filesystem**: PFS (PlayStation Filesystem)
- **Key structures**:
  - PFS header: magic `PFSIMG`, version, block size, inode count
  - Inodes: 0xB8 bytes each, direct/indirect extent pointers
  - Directory entries: variable-length, name + inode number
  - Superblock: at fixed offset, contains volume metadata
  - Indirect blocks: 512 entries pointing to data blocks
- **Magic/signature**: `PFSIMG` at superblock
- **Dependencies**: AES-128-CBC for encrypted packages

### PS4 — PFS + BDVN

- **Console type**: `Ps4`
- **Formats**: PKG (PlayStation Package), disc image
- **Filesystem**: Orbis PFS (modified from UDF/ISO concepts)
- **Key structures**:
  - PKG header: magic `7F 50 4B 47` (`\x7FPKG`), metadata offsets
  - PFS header: magic `PFSIMG`, inode table, data blocks
  - Superblock: block size (typically 128KB), inode count
  - Inodes: extent-based allocation (up to 12 direct extents)
  - Directory tree: B-tree indexed by name hash
  - BDVN: Blu-ray Volume Navigation descriptor
- **Magic/signature**: `\x7FPKG` for PKG, `PFSIMG` for PFS
- **Dependencies**: AES, HMAC-SHA256 for package verification

### PS5 — PFS v2 + BDVN v2

- **Console type**: `Ps5`
- **Formats**: PS5 PKG, disc image
- **Filesystem**: PFS v2 (evolution of PS4 PFS)
- **Key structures**:
  - Similar to PS4 PFS but with larger block sizes and updated inode format
  - BDVN v2 with enhanced partition support
  - Additional hash tree layers for integrity verification
- **Magic/signature**: Same as PS4 with version field differences
- **Dependencies**: AES, HMAC-SHA256, SHA-256

---

## Phase 3: Cartridge/Handheld Filesystems

### Nintendo DS / 3DS — NitroFS / RomFS / ExeFS

- **Console types**: `NintendoDS`, `Nintendo3DS`
- **Formats**: NDS ROM, 3DS ROM (CCI/CSU), CIA
- **Filesystems**:
  - **NitroFS**: NDS cartridge filesystem, FAT-like with root directory table
  - **RomFS**: Read-only FS with simple header + directory/file entries, used in 3DS
  - **ExeFS**: Executable filesystem containing code sections
  - **NCSD**: 3DS container format with multiple partitions
- **Key structures**:
  - NDS header: 0x200 bytes, ARM9/ARM7 binary offsets, FAT/FNT offsets
  - FNT (File Name Table): directory entries + name offsets
  - FAT (File Allocation Table): start/end offset pairs per file
  - RomFS: level headers, directory/file metadata entries
  - ExeFS: header with section names + offsets
- **Magic/signature**: NDS header fields, RomFS magic
- **Dependencies**: AES for 3DS encrypted content

### Game Boy / GBC / GBA — Cartridge Save FS

- **Console types**: `GameBoy`, `GameBoyColor`, `GameBoyAdvance`
- **Formats**: ROM (.gb, .gbc, .gba) + save files (.sav)
- **Filesystem**: SRAM/Flash/EEPROM save filesystem
- **Key structures**:
  - SRAM: direct memory-mapped, 32KB typical
  - Flash: 64K/128K sectors with command interface
  - EEPROM: 512B/8KB with serial protocol
  - Save header detection via magic strings (e.g., `EEPROM_V`, `SRAM_V`, `FLASH_V`, `FLASH5M`)
- **Magic/signature**: `EEPROM_V`, `SRAM_V`, `FLASH_V`, `FLASH5M` in ROM
- **Dependencies**: None

### WonderSwan / WonderSwan Color

- **Console type**: `WonderSwan`
- **Format**: WS ROM
- **Filesystem**: Linear ROM with optional EEPROM/Flash save
- **Key structures**: ROM header at 0x00, simple linear mapping
- **Magic/signature**: Header checksum validation
- **Dependencies**: None

---

## Phase 4: Arcade & Other Platforms

### Sega NAOMI / NAOMI 2 / Triforce

- **Console type**: `Naomi`
- **Format**: Custom GD-ROM variant / cartridge ROM
- **Filesystem**: Modified ISO 9660 with Sega extensions
- **Key structures**:
  - NAOMI header: custom header with board-specific metadata
  - Cartridge ROM: linear mapping with interleaved data
  - GD-ROM: similar to Dreamcast GD-ROM with additional security sectors
- **Magic/signature**: `SEGA SEGAKATANA` (same as Dreamcast)
- **Dependencies**: None beyond existing ISO 9660 parser

### Atari Jaguar CD

- **Console type**: `JaguarCD`
- **Format**: Custom CD format
- **Filesystem**: Proprietary with ISO 9660 compatibility layer
- **Key structures**:
  - Custom boot sector with Jaguar-specific metadata
  - TOC (Table of Contents) with track layout
  - Memory track filesystem for saved games (EEPROM-backed)
- **Magic/signature**: `ATARI` in boot header
- **Dependencies**: None

### Neo Geo Pocket / Neo Geo Pocket Color

- **Console type**: `NeoGeoPocket`
- **Format**: NGP ROM
- **Filesystem**: Linear ROM with optional flash save
- **Key structures**: ROM header with entry point and metadata
- **Magic/signature**: Header validation
- **Dependencies**: None

### Bandai Wonderswan (extended)

- **Console type**: `WonderSwanColor`
- **Format**: WS/WSC ROM
- **Filesystem**: Linear with internal EEPROM mapping
- **Key structures**: Header with splash screen, mapper type, save type
- **Dependencies**: None

---

## Phase 5: Deeper Parsing for Existing Consoles

### Dreamcast — GD-ROM Full Layout

- **Enhancement to**: `Dreamcast` parser
- **Add**: GD-ROM high-density area parsing, IP.BIN full extraction, multi-session support
- **Current state**: ISO 9660 with IP.BIN signature detection only
- **Goal**: Full GD-ROM TOC parsing, track separation, LBA remapping

### Saturn — Multi-Session CD

- **Enhancement to**: `Saturn` parser
- **Add**: Multi-session disc handling, boot.bin parsing, multiplayer/multi-disc support
- **Current state**: Basic ISO 9660 wrapper
- **Goal**: Session-aware parsing with proper track handling

### PSP — Digital Content Format

- **Enhancement to**: `Psp` parser
- **Add**: PBP/PRX container format, EBOOT.BIN parsing, PSAR extraction
- **Current state**: UMD ISO only
- **Goal**: Full PSP digital content support

### PS1/PS2 — CD-ROM XA Deep Parsing

- **Enhancement to**: `PlayStation1Parser` / `PlayStation2Parser`
- **Add**: XA subheader interpretation, interleaved audio+data track handling, SYSTEM.CNF parsing
- **Current state**: ISO 9660 wrapper with basic XA mode detection
- **Goal**: Full CD-ROM XA semantics including form1/form2 sectors

---

## Implementation Notes

### Architecture Pattern

Each new parser should follow the existing pattern:

```
Parsers/
  Systems/
    NewConsoleParser.cs    # IConsoleParser implementation
  NewFilesystemParser.cs   # Core filesystem parsing logic
```

1. Create filesystem parser class (handles raw parsing)
2. Create console parser class (implements `IConsoleParser`, handles console-specific detection)
3. Add enum value to `Models/ConsoleType.cs`
4. Register in `Parsers/ParserFactory.cs`
5. Update `ConsoleInfo.cs` display name

### Cryptography Dependencies

Phases 2-3 (Nintendo/Sony modern) require cryptographic operations:

```csharp
using System.Security.Cryptography; // AES, RSA, HMAC-SHA256, SHA-256
```

Consider:
- Keeping crypto in a separate optional assembly to avoid bloating the core parser
- Supporting both .NET 8+ (where `System.Security.Cryptography` is built-in) and older targets
- Key management: do NOT ship keys in the library; accept them via API

### Testing Strategy

Each parser should have:
- Unit tests with known-good disc images (small synthetic test images)
- Edge case handling (corrupted headers, truncated files, non-standard layouts)
- Fallback behavior (return gracefully on unrecognized images)

### Priority Order

| Priority | Scope | Effort |
|---|---|---|
| **P0** | GameCube/Wii GCM parser | Medium — well-documented, no crypto |
| **P1** | PS Vita PFS parser | Medium — documented in vitasdk |
| **P2** | Nintendo Switch NCA/PFS0 | Medium-High — well-documented, needs crypto |
| **P3** | PS4 PKG/PFS parser | High — needs crypto, partially documented |
| **P4** | Wii U WUD/WFS parser | High — needs crypto, less documented |
| **P5** | 3DS RomFS/ExeFS | Medium — well-documented, needs crypto |
| **P6** | NDS NitroFS | Low — simple, well-documented, no crypto |
| **P7** | PS5 PFS v2 | High — minimal documentation |
| **P8** | NAOMI / Jaguar CD / NGP | Low-Medium — niche platforms |
