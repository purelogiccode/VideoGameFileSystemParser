using System.Collections.Frozen;

namespace VideoGameFileSystemParser.Models;

/// <summary>
/// Single source of truth for console type display names and CLI aliases.
/// </summary>
/// <remarks>
/// Host applications (e.g. CHDMounter) must resolve console types exclusively
/// through this registry — numeric indexes are not supported. The registry is
/// built from the supported-console table and is case-insensitive for aliases.
/// Multiple display entries may resolve to the same <see cref="ConsoleType"/>
/// (e.g. "PS3 ISO RAW 2352", "Xbox ISO RAW 2352" and "ISO RAW 2352"
/// all map to <see cref="ConsoleType.GenericIsoRaw2352"/>); alias lookup
/// always returns the canonical type.
/// </remarks>
public static class ConsoleTypeRegistry
{
    /// <summary>
    /// All supported console/format entries, ordered as displayed in the UI and help text.
    /// </summary>
    public static IReadOnlyList<ConsoleTypeInfo> All { get; } =
    [
        new(ConsoleType.ThreeDo, "3DO", ["3do"]),
        new(ConsoleType.AmigaCd, "Amiga CD", ["amigacd"]),
        new(ConsoleType.AmigaCd32, "Amiga CD32", ["amigacd32"]),
        new(ConsoleType.AmigaCdtv, "Amiga CDTV", ["amigacdtv"]),
        new(ConsoleType.CDi, "CD-i", ["cdi"]),
        new(ConsoleType.FmTowns, "FM Towns", ["fmtowns"]),
        new(ConsoleType.NeoGeoCd, "Neo Geo CD", ["neogeocd"]),
        new(ConsoleType.Nuon, "Nuon", ["nuon"]),
        new(ConsoleType.PcEngineCd, "PC Engine CD", ["pcengine"]),
        new(ConsoleType.Pc98, "PC-98", ["pc98"]),
        new(ConsoleType.PcFx, "PC-FX", ["pcfx"]),
        new(ConsoleType.Pico, "Pico", ["pico"]),
        new(ConsoleType.Pippin, "Pippin", ["pippin"]),
        new(ConsoleType.PlayStation, "PlayStation (Auto)", ["psauto"]),
        new(ConsoleType.Ps1, "PS1", ["ps1"]),
        new(ConsoleType.Ps2, "PS2", ["ps2"]),
        new(ConsoleType.Ps3, "PS3", ["ps3"]),
        new(ConsoleType.GenericIsoRaw2352, "PS3 ISO RAW 2352", ["isoraw2352"]),
        new(ConsoleType.Psp, "PSP", ["psp"]),
        new(ConsoleType.Dreamcast, "Sega Dreamcast", ["segadreamcast"]),
        new(ConsoleType.SegaGenesisCd, "Sega Genesis", ["segagenesis"]),
        new(ConsoleType.Saturn, "Sega Saturn", ["segasaturn"]),
        new(ConsoleType.X68000, "X68000", ["x68000"]),
        new(ConsoleType.Xbox, "Xbox", ["xbox"]),
        new(ConsoleType.Xbox360, "Xbox 360", ["xbox360"]),
        new(ConsoleType.GenericIsoRaw2352, "Xbox ISO RAW 2352", ["isoraw2352"]),

        new(ConsoleType.GenericIso9660, "ISO 9660", ["iso9660"]),

        new(ConsoleType.GenericIsoRaw2048, "ISO RAW 2048", ["isoraw2048"]),
        new(ConsoleType.GenericCueIso2048, "CUE/ISO RAW 2048", ["cueiso2048"]),
        new(ConsoleType.GenericCueIsoWav2048, "CUE/ISO/WAV RAW 2048", ["cueisowav2048"]),
        new(ConsoleType.GenericIsoRaw2352, "ISO RAW 2352", ["isoraw2352"]),
        new(ConsoleType.GenericCueIso2352, "CUE/ISO RAW 2352", ["cueiso2352"]),
        new(ConsoleType.GenericCueIsoWav2352, "CUE/ISO/WAV RAW 2352", ["cueisowav2352"]),

        new(ConsoleType.GenericCueBin2048, "CUE/BIN RAW 2048", ["cuebin2048"]),
        new(ConsoleType.GenericCueBinWav2048, "CUE/BIN/WAV RAW 2048", ["cuebinwav2048"]),
        new(ConsoleType.GenericCueBin2352, "CUE/BIN RAW 2352", ["cuebin2352"]),
        new(ConsoleType.GenericCueBinWav2352, "CUE/BIN/WAV RAW 2352", ["cuebinwav2352"])
    ];

    private static readonly FrozenDictionary<string, ConsoleType> AliasMap = BuildAliasMap();

    private static FrozenDictionary<string, ConsoleType> BuildAliasMap()
    {
        var map = new Dictionary<string, ConsoleType>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in All)
        {
            foreach (var alias in entry.Aliases)
            {
                map.TryAdd(alias, entry.Type);
            }
        }

        return map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves a console type from a CLI alias (case-insensitive).
    /// </summary>
    /// <param name="alias">The alias, e.g. "ps2", "cuebin2352".</param>
    /// <returns>The matching <see cref="ConsoleType"/>, or <see cref="ConsoleType.Unknown"/> if not recognized.</returns>
    public static ConsoleType Parse(string? alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return ConsoleType.Unknown;

        return AliasMap.GetValueOrDefault(alias.Trim(), ConsoleType.Unknown);
    }

    /// <summary>
    /// Returns the primary display name for a console type (the first registered entry).
    /// </summary>
    public static string GetDisplayName(ConsoleType type)
    {
        foreach (var entry in All)
        {
            if (entry.Type == type)
                return entry.DisplayName;
        }

        return type.ToString();
    }

    /// <summary>
    /// Returns all aliases registered for a console type.
    /// </summary>
    public static IReadOnlyList<string> GetAliases(ConsoleType type)
    {
        var aliases = new List<string>();
        foreach (var entry in All)
        {
            if (entry.Type == type)
                aliases.AddRange(entry.Aliases);
        }

        return aliases.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
