using VideoGameFileSystemParser.Interfaces;

namespace VideoGameFileSystemParser.Parsers.Systems;

/// <summary>
/// Parses Sony PSP disc images using ISO 9660 on the first data track.
/// </summary>
public class PspParser : Iso9660Wrapper
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PspParser"/> class.
    /// </summary>
    /// <param name="reader">The sector reader to use for reading disc data.</param>
    public PspParser(SectorReader reader) : base(reader)
    {
    }

    /// <inheritdoc />
    public override ConsoleType GetConsoleType()
    {
        return ConsoleType.Psp;
    }

    /// <inheritdoc />
    public override string GetConsoleName()
    {
        return "PSP";
    }
}

/// <summary>
/// Parses NEC PC-FX disc images using the dedicated PcFxIsoParser.
/// </summary>
public class PcFxParser : IConsoleParser
{
    private readonly SectorReader _reader;
    private readonly PcFxIsoParser _isoParser;

    /// <summary>
    /// Initializes a new instance of the <see cref="PcFxParser"/> class.
    /// </summary>
    /// <param name="reader">The sector reader to use for reading disc data.</param>
    public PcFxParser(SectorReader reader)
    {
        _reader = reader;
        _isoParser = new PcFxIsoParser(reader);
    }

    /// <inheritdoc />
    public bool ForceMode { get; set; }

    /// <inheritdoc />
    public ConsoleType GetConsoleType()
    {
        return ConsoleType.PcFx;
    }

    /// <inheritdoc />
    public string GetConsoleName()
    {
        return "PC-FX";
    }

    /// <inheritdoc />
    public bool Parse(FsNode rootNode)
    {
        foreach (var t in _reader.Tracks)
        {
            if (t.IsDataTrack && _isoParser.Parse(rootNode, t))
                return true;
        }

        if (_isoParser.Parse(rootNode))
            return true;

        var dataTracks = _reader.Tracks.Where(static t => t.IsDataTrack).ToList();
        if (dataTracks.Count == 0)
            return false;

        rootNode.Name = "/";
        rootNode.IsDirectory = true;

        foreach (var track in dataTracks)
        {
            var size = (ulong)track.Frames * 2048;
            var node = new FsNode
            {
                Name = $"TRACK{track.Index:D2}.iso",
                Lba = track.StartLba,
                Size = size,
                IsDirectory = false
            };
            node.Extents.Add(new FsExtent { Lba = track.StartLba, Size = size });
            rootNode.Children.Add(node);
        }

        return rootNode.Children.Count > 0;
    }

    /// <inheritdoc />
    public bool ParseTrack(FsNode rootNode, TrackInfo track)
    {
        if (_isoParser.Parse(rootNode, track))
            return true;

        if (!track.IsDataTrack)
            return false;

        var size = (ulong)track.Frames * 2048;
        rootNode.Name = "/";
        rootNode.IsDirectory = true;

        var node = new FsNode
        {
            Name = $"TRACK{track.Index:D2}.iso",
            Lba = track.StartLba,
            Size = size,
            IsDirectory = false
        };
        node.Extents.Add(new FsExtent { Lba = track.StartLba, Size = size });
        rootNode.Children.Add(node);

        return true;
    }
}

/// <summary>
/// Parses Sega Genesis CD / Mega CD disc images using ISO 9660.
/// </summary>
public class SegaGenesisCdParser : Iso9660Wrapper
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SegaGenesisCdParser"/> class.
    /// </summary>
    /// <param name="reader">The sector reader to use for reading disc data.</param>
    public SegaGenesisCdParser(SectorReader reader) : base(reader)
    {
    }

    /// <inheritdoc />
    public override ConsoleType GetConsoleType()
    {
        return ConsoleType.SegaGenesisCd;
    }

    /// <inheritdoc />
    public override string GetConsoleName()
    {
        return "Sega Genesis CD";
    }
}

/// <summary>
/// Parses Sega Saturn disc images using ISO 9660.
/// </summary>
public class SegaSaturnParser : Iso9660Wrapper
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SegaSaturnParser"/> class.
    /// </summary>
    /// <param name="reader">The sector reader to use for reading disc data.</param>
    public SegaSaturnParser(SectorReader reader) : base(reader)
    {
    }

    /// <inheritdoc />
    public override ConsoleType GetConsoleType()
    {
        return ConsoleType.Saturn;
    }

    /// <inheritdoc />
    public override string GetConsoleName()
    {
        return "Saturn";
    }
}

/// <summary>
/// Parses SNK NeoGeo CD disc images using ISO 9660.
/// </summary>
public class NeoGeoCdParser : Iso9660Wrapper
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NeoGeoCdParser"/> class.
    /// </summary>
    /// <param name="reader">The sector reader to use for reading disc data.</param>
    public NeoGeoCdParser(SectorReader reader) : base(reader)
    {
    }

    /// <inheritdoc />
    public override ConsoleType GetConsoleType()
    {
        return ConsoleType.NeoGeoCd;
    }

    /// <inheritdoc />
    public override string GetConsoleName()
    {
        return "NeoGeo CD";
    }
}

/// <summary>
/// Parses Commodore Amiga CD32 disc images using ISO 9660.
/// </summary>
public class AmigaCd32Parser : Iso9660Wrapper
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AmigaCd32Parser"/> class.
    /// </summary>
    /// <param name="reader">The sector reader to use for reading disc data.</param>
    public AmigaCd32Parser(SectorReader reader) : base(reader)
    {
    }

    /// <inheritdoc />
    public override ConsoleType GetConsoleType()
    {
        return ConsoleType.AmigaCd32;
    }

    /// <inheritdoc />
    public override string GetConsoleName()
    {
        return "Amiga CD32";
    }
}

/// <summary>
/// Parses Commodore Amiga CD disc images using ISO 9660.
/// </summary>
public class AmigaCdParser : Iso9660Wrapper
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AmigaCdParser"/> class.
    /// </summary>
    /// <param name="reader">The sector reader to use for reading disc data.</param>
    public AmigaCdParser(SectorReader reader) : base(reader)
    {
    }

    /// <inheritdoc />
    public override ConsoleType GetConsoleType()
    {
        return ConsoleType.AmigaCd;
    }

    /// <inheritdoc />
    public override string GetConsoleName()
    {
        return "Amiga CD";
    }
}

/// <summary>
/// Parses Sharp X68000 disc images using ISO 9660, falling back to UDF if ISO 9660 fails.
/// </summary>
internal class X68000Parser : IConsoleParser
{
    private readonly SectorReader _reader;

    internal X68000Parser(SectorReader reader)
    {
        _reader = reader;
    }

    /// <inheritdoc />
    public bool ForceMode { get; set; }

    /// <inheritdoc />
    public ConsoleType GetConsoleType()
    {
        return ConsoleType.X68000;
    }

    /// <inheritdoc />
    public string GetConsoleName()
    {
        return "X68000";
    }

    /// <inheritdoc />
    public bool Parse(FsNode rootNode)
    {
        return ParseTrack(rootNode, FindDataTrack());
    }

    /// <inheritdoc />
    public bool ParseTrack(FsNode rootNode, TrackInfo track)
    {
        var isoParser = new Iso9660Parser(_reader);
        if (isoParser.Parse(rootNode, track))
            return true;

        var udfParser = new UdfParser(_reader);
        return udfParser.Parse(rootNode, track);
    }

    private TrackInfo FindDataTrack()
    {
        foreach (var t in _reader.Tracks)
            if (t.IsDataTrack)
                return t;

        return _reader.Tracks.Count > 0 ? _reader.Tracks[0] : new TrackInfo();
    }
}

/// <summary>
/// Parses NEC PC-98 disc images using ISO 9660.
/// </summary>
public class Pc98Parser : Iso9660Wrapper
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Pc98Parser"/> class.
    /// </summary>
    /// <param name="reader">The sector reader to use for reading disc data.</param>
    public Pc98Parser(SectorReader reader) : base(reader)
    {
    }

    /// <inheritdoc />
    public override ConsoleType GetConsoleType()
    {
        return ConsoleType.Pc98;
    }

    /// <inheritdoc />
    public override string GetConsoleName()
    {
        return "PC-98";
    }
}

/// <summary>
/// Parses Fujitsu FM Towns disc images using ISO 9660.
/// </summary>
public class FmTownsParser : Iso9660Wrapper
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FmTownsParser"/> class.
    /// </summary>
    /// <param name="reader">The sector reader to use for reading disc data.</param>
    public FmTownsParser(SectorReader reader) : base(reader)
    {
    }

    /// <inheritdoc />
    public override ConsoleType GetConsoleType()
    {
        return ConsoleType.FmTowns;
    }

    /// <inheritdoc />
    public override string GetConsoleName()
    {
        return "FM Towns";
    }
}

/// <summary>
/// Parses Sega Pico disc images using ISO 9660.
/// </summary>
internal class PicoParser : Iso9660Wrapper
{
    internal PicoParser(SectorReader reader) : base(reader)
    {
    }

    /// <inheritdoc />
    public override ConsoleType GetConsoleType()
    {
        return ConsoleType.Pico;
    }

    /// <inheritdoc />
    public override string GetConsoleName()
    {
        return "Sega Pico";
    }
}

/// <summary>
/// Parses Apple Bandai Pippin disc images using HFS (Macintosh Hierarchical File System),
/// falling back to HFS+, UDF, and ISO 9660 if HFS parsing fails.
/// </summary>
internal class PippinParser : IConsoleParser
{
    private readonly SectorReader _reader;
    private HfsParser? _hfsParser;

    /// <inheritdoc />
    public bool ForceMode { get; set; }

    internal PippinParser(SectorReader reader)
    {
        _reader = reader;
    }

    /// <inheritdoc />
    public ConsoleType GetConsoleType()
    {
        return ConsoleType.Pippin;
    }

    /// <inheritdoc />
    public string GetConsoleName()
    {
        return "Pippin";
    }

    /// <inheritdoc />
    public bool Parse(FsNode rootNode)
    {
        foreach (var t in _reader.Tracks)
        {
            if (t.IsDataTrack && ParseTrack(rootNode, t))
                return true;
        }

        return ParseTrack(rootNode, FindDataTrack());
    }

    /// <inheritdoc />
    public bool ParseTrack(FsNode rootNode, TrackInfo track)
    {
        _hfsParser ??= new HfsParser(_reader);

        if (_hfsParser.Parse(rootNode, track))
            return true;

        var udfParser = new UdfParser(_reader);
        if (udfParser.Parse(rootNode, track))
            return true;

        var isoParser = new Iso9660Parser(_reader);
        return isoParser.Parse(rootNode, track);
    }

    private TrackInfo FindDataTrack()
    {
        foreach (var t in _reader.Tracks)
            if (t.IsDataTrack)
                return t;

        return _reader.Tracks.Count > 0 ? _reader.Tracks[0] : new TrackInfo();
    }
}

/// <summary>
/// Base class for console parsers that use ISO 9660 file system parsing.
/// </summary>
public abstract class Iso9660Wrapper : IConsoleParser
{
    /// <summary>
    /// The sector reader used by this parser.
    /// </summary>
    private SectorReader Reader { get; }

    /// <inheritdoc />
    public bool ForceMode { get; set; }

    /// <summary>
    /// Initializes a new instance of the Iso9660Wrapper class.
    /// </summary>
    /// <param name="reader">The SectorReader to read sectors from.</param>
    protected Iso9660Wrapper(SectorReader reader)
    {
        Reader = reader;
    }

    /// <summary>
    /// Returns the ConsoleType that this parser handles.
    /// </summary>
    /// <returns>The console type.</returns>
    public abstract ConsoleType GetConsoleType();

    /// <summary>
    /// Returns the human-readable console name.
    /// </summary>
    /// <returns>The display name.</returns>
    public abstract string GetConsoleName();

    /// <summary>
    /// Parses the first data track found in the reader using ISO 9660.
    /// </summary>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool Parse(FsNode rootNode)
    {
        var track = FindDataTrack();
        if (track == null)
            return false;

        return ParseTrack(rootNode, track);
    }

    /// <summary>
    /// Parses the specified track using ISO 9660.
    /// </summary>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <param name="track">The track to parse.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool ParseTrack(FsNode rootNode, TrackInfo track)
    {
        var parser = new Iso9660Parser(Reader);
        return parser.Parse(rootNode, track);
    }

    /// <summary>
    /// Finds the first data track in the reader.
    /// </summary>
    /// <returns>The first data TrackInfo, or null.</returns>
    private TrackInfo? FindDataTrack()
    {
        foreach (var t in Reader.Tracks)
            if (t.IsDataTrack)
                return t;

        return Reader.Tracks.FirstOrDefault();
    }
}
