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
            ConsoleType.GenericIsoRaw2352 or ConsoleType.GenericIsoRaw2048 => new GenericIsoRawParser(reader),
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
        return ConsoleTypeRegistry.All.Select(static entry => new ConsoleInfo(entry.Type, entry.DisplayName));
    }
}