# Migration Guide

This page explains how to upgrade between major versions of `VideoGameFileSystemParser` and what to watch out for.

---

## v1.1 → v1.2

**Breaking changes — read carefully.**

### Removed / renamed `ConsoleType` values

| v1.1.0 | v1.2.0+ |
|---|---|
| `ConsoleType.GenericIsoRaw` | `ConsoleType.GenericIsoRaw2352` (or `GenericIsoRaw2048`) |
| `ConsoleType.GenericCueBin2352Default` | `ConsoleType.GenericCueBin2352` |
| `ConsoleType.GenericCueIso` | `ConsoleType.GenericCueIso2352` or `GenericCueIso2048` |
| `ConsoleType.GenericCueBinWav` | `ConsoleType.GenericCueBinWav2352` or `GenericCueBinWav2048` |
| `ConsoleType.GenericCueIsoWav` | `ConsoleType.GenericCueIsoWav2352` or `GenericCueIsoWav2048` |

**Compile-time impact:** any switch/if over the old names fails to compile. The fix is a direct rename (see table). If your code stored numeric values or indices, see below.

### What changed

1. **Sector-size-aware generic formats.** Generic raw/export types are now unambiguous about sector size:
   - raw passthrough: 2352-byte units vs 2048-byte units;
   - CUE/ISO, CUE/BIN/WAV, CUE/ISO/WAV: 2352 vs 2048 variants.
2. **New `ConsoleTypeRegistry`.** Introduced as the single source of truth for display names and CLI aliases. `ParserFactory.GetAllSupportedConsoles()` is now derived from it.
3. **CUE export fixes.**
   - Audio tracks inside BIN files always use 2352-byte sectors, even in 2048-cooked exports.
   - CUE track mode strings now reflect the actual sector size per track (`MODE1/2352`, `MODE2/2048`, ...).

### Action required

1. Replace the renamed `ConsoleType` members (table above).
2. If you hard-coded **numeric enum values** anywhere (e.g. persisted an index in a config file), migrate to alias-based resolution:
   ```csharp
   // Before (v1.1): persisted int → cast back to ConsoleType
   // After (v1.2+):
   ConsoleType type = ConsoleTypeRegistry.Parse(configString); // "ps2", "cuebin2352", ...
   ```
3. If you enumerate consoles for a UI/CLI, prefer `ConsoleTypeRegistry.All` (display names + aliases) over your own hard-coded list.

---

## v1.0 → v1.1

**No breaking changes.**

- v1.1.0 fixed the virtual CUE/BIN/ISO/WAV export: pregap shift (tracks started 2 s late), missing ISO audio in CUE/ISO export, and truncated tracks / incorrect BIN/ISO/WAV sizes.
- WAV exports now skip the stored pregap silence and start at `INDEX 01 00:00:00`.
- If you extracted WAV files with v1.0.0, re-extract them with v1.1.0+ — the audio offsets were wrong before.

---

## Version support policy

| Version | Status |
|---|---|
| 1.2.x | Current |
| 1.1.x | Supported |
| 1.0.x | Legacy — upgrade recommended (export bugs) |

---

*Previous: [Architecture](Architecture.md) · Next: [FAQ](FAQ.md) · [Back to Home](Home.md)*
