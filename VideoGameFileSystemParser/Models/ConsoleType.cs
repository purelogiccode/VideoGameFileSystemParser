namespace VideoGameFileSystemParser.Models;

/// <summary>
/// Identifies the target console or disc image format.
/// </summary>
public enum ConsoleType
{
    /// <summary>Unknown or unset console type.</summary>
    Unknown,

    /// <summary>Amiga CD format.</summary>
    AmigaCd,

    /// <summary>Amiga CD32 format.</summary>
    AmigaCd32,

    /// <summary>Amiga CDTV format.</summary>
    AmigaCdtv,

    /// <summary>Philips CD-i format.</summary>
    CDi,

    /// <summary>Sega Dreamcast GD-ROM format.</summary>
    Dreamcast,

    /// <summary>Fujitsu FM Towns format.</summary>
    FmTowns,

    /// <summary>SNK NeoGeo CD format.</summary>
    NeoGeoCd,

    /// <summary>VM Labs Nuon DVD format.</summary>
    Nuon,

    /// <summary>NEC PC Engine CD format.</summary>
    PcEngineCd,

    /// <summary>NEC PC-FX format.</summary>
    PcFx,

    /// <summary>NEC PC-98 format.</summary>
    Pc98,

    /// <summary>Sega Pico format.</summary>
    Pico,

    /// <summary>Apple Bandai Pippin format.</summary>
    Pippin,

    /// <summary>PlayStation auto-detection mode.</summary>
    PlayStation,

    /// <summary>Sony PlayStation 1 format.</summary>
    Ps1,

    /// <summary>Sony PlayStation 2 format.</summary>
    Ps2,

    /// <summary>Sony PlayStation 3 format.</summary>
    Ps3,

    /// <summary>Sony PlayStation Portable format.</summary>
    Psp,

    /// <summary>Sega Saturn format.</summary>
    Saturn,

    /// <summary>Sega Genesis CD format.</summary>
    SegaGenesisCd,

    /// <summary>3DO Interactive Multiplayer format.</summary>
    ThreeDo,

    /// <summary>Sharp X68000 format.</summary>
    X68000,

    /// <summary>Microsoft Xbox format.</summary>
    Xbox,

    /// <summary>Microsoft Xbox 360 format.</summary>
    Xbox360,

    /// <summary>Generic ISO 9660 file system.</summary>
    GenericIso9660,

    /// <summary>Raw sector passthrough (no file system parsing), 2352-byte units.</summary>
    GenericIsoRaw2352,

    /// <summary>Raw sector passthrough (no file system parsing), 2048-byte units.</summary>
    GenericIsoRaw2048,

    /// <summary>Generic CUE/BIN image with 2352-byte sectors.</summary>
    GenericCueBin2352,

    /// <summary>Generic CUE/BIN image with 2048-byte sectors.</summary>
    GenericCueBin2048,

    /// <summary>Generic CUE/ISO image with 2352-byte sectors.</summary>
    GenericCueIso2352,

    /// <summary>Generic CUE/ISO image with 2048-byte sectors.</summary>
    GenericCueIso2048,

    /// <summary>Generic CUE/BIN with WAV audio tracks, 2352-byte sectors.</summary>
    GenericCueBinWav2352,

    /// <summary>Generic CUE/BIN with WAV audio tracks, 2048-byte sectors.</summary>
    GenericCueBinWav2048,

    /// <summary>Generic CUE/ISO with WAV audio tracks, 2352-byte sectors.</summary>
    GenericCueIsoWav2352,

    /// <summary>Generic CUE/ISO with WAV audio tracks, 2048-byte sectors.</summary>
    GenericCueIsoWav2048
}
