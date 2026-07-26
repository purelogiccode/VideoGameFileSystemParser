using VideoGameFileSystemParser.Interfaces;
using VideoGameFileSystemParser.Parsers.Systems;

namespace VideoGameFileSystemParser.Parsers;

/// <summary>
/// Provides factory methods to create appropriate IConsoleParser instances for each console type.
/// </summary>
public static class ParserFactory
{
    /// <summary>
    /// Creates a parser instance for the specified console type.
    /// </summary>
    /// <returns>An IConsoleParser implementation, or null if unsupported.</returns>
    public static IConsoleParser? CreateParser(ConsoleType type, SectorReader reader)
    {
        return type switch
        {
            ConsoleType.AmigaCd or ConsoleType.AmigaCdtv => new AmigaCdParser(reader),
            ConsoleType.AmigaCd32 => new AmigaCd32Parser(reader),
            ConsoleType.CDi => new CDiParser(reader),
            ConsoleType.Dreamcast => new DreamcastParser(reader),
            ConsoleType.FmTowns => new FmTownsParser(reader),
            ConsoleType.GenericIso9660 => new GenericIso9660Parser(reader),
            ConsoleType.GenericIsoRaw => new GenericIsoRawParser(reader),
            ConsoleType.Nuon => new NuonParser(reader),
            ConsoleType.NeoGeoCd => new NeoGeoCdParser(reader),
            ConsoleType.PcEngineCd => new PcEngineCdParser(reader),
            ConsoleType.PcFx => new PcFxParser(reader),
            ConsoleType.Pc98 => new Pc98Parser(reader),
            ConsoleType.PlayStation => new PlayStationAutoDetectParser(reader),
            ConsoleType.Ps1 => new PlayStation1Parser(reader),
            ConsoleType.Ps2 => new PlayStation2Parser(reader),
            ConsoleType.Ps3 => new PlayStation3Parser(reader),
            ConsoleType.Psp => new PspParser(reader),
            ConsoleType.Saturn => new SegaSaturnParser(reader),
            ConsoleType.SegaGenesisCd => new SegaGenesisCdParser(reader),
            ConsoleType.ThreeDo => new ThreeDoConsoleParser(reader),
            ConsoleType.X68000 => new X68000Parser(reader),
            ConsoleType.Xbox => new XboxParser(reader),
            ConsoleType.Xbox360 => new Xbox360Parser(reader),
            ConsoleType.Pico => new PicoParser(reader),
            ConsoleType.Pippin => new PippinParser(reader),
            _ => null
        };
    }

    /// <summary>
    /// Returns the list of all supported console types with their display names.
    /// </summary>
    /// <returns>An enumerable of ConsoleInfo for all supported consoles.</returns>
    public static IEnumerable<ConsoleInfo> GetAllSupportedConsoles()
    {
        return
        [
            new ConsoleInfo(ConsoleType.ThreeDo, "3DO"),
            new ConsoleInfo(ConsoleType.AmigaCd, "Amiga CD"),
            new ConsoleInfo(ConsoleType.AmigaCd32, "Amiga CD32"),
            new ConsoleInfo(ConsoleType.AmigaCdtv, "Amiga CDTV"),
            new ConsoleInfo(ConsoleType.CDi, "CD-i"),
            new ConsoleInfo(ConsoleType.Dreamcast, "Dreamcast"),
            new ConsoleInfo(ConsoleType.SegaGenesisCd, "Genesis CD"),
            new ConsoleInfo(ConsoleType.FmTowns, "FM Towns"),
            new ConsoleInfo(ConsoleType.NeoGeoCd, "NeoGeo CD"),
            new ConsoleInfo(ConsoleType.Nuon, "Nuon"),
            new ConsoleInfo(ConsoleType.PcEngineCd, "PC Engine CD"),
            new ConsoleInfo(ConsoleType.PcFx, "PC-FX"),
            new ConsoleInfo(ConsoleType.Pc98, "PC-98"),
            new ConsoleInfo(ConsoleType.Pico, "Pico"),
            new ConsoleInfo(ConsoleType.Pippin, "Pippin"),
            new ConsoleInfo(ConsoleType.PlayStation, "PlayStation (Auto)"),
            new ConsoleInfo(ConsoleType.Ps1, "PS1"),
            new ConsoleInfo(ConsoleType.Ps2, "PS2"),
            new ConsoleInfo(ConsoleType.Ps3, "PS3"),
            new ConsoleInfo(ConsoleType.Psp, "PSP"),
            new ConsoleInfo(ConsoleType.Saturn, "Saturn"),
            new ConsoleInfo(ConsoleType.X68000, "X68000"),
            new ConsoleInfo(ConsoleType.Xbox, "Xbox"),
            new ConsoleInfo(ConsoleType.Xbox360, "Xbox 360"),
            new ConsoleInfo(ConsoleType.GenericIso9660, "Generic ISO 9660"),
            new ConsoleInfo(ConsoleType.GenericIsoRaw, "Generic ISO Raw"),
            new ConsoleInfo(ConsoleType.GenericCueBin2352Default, "CUE/BIN (2352 Default)"),
            new ConsoleInfo(ConsoleType.GenericCueBin2048, "CUE/BIN (2048 Cooked)"),
            new ConsoleInfo(ConsoleType.GenericCueIso, "CUE/ISO (2048)"),
            new ConsoleInfo(ConsoleType.GenericCueBinWav, "CUE/BIN/WAV"),
            new ConsoleInfo(ConsoleType.GenericCueIsoWav, "CUE/ISO/WAV")
        ];
    }
}