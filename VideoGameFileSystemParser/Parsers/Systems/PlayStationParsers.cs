using VideoGameFileSystemParser.Interfaces;

namespace VideoGameFileSystemParser.Parsers.Systems;

/// <summary>
/// Parses PlayStation 1 disc images using ISO 9660 on the first data track.
/// </summary>
public class PlayStation1Parser : IConsoleParser
{
    private readonly SectorReader _reader;

    /// <summary>
    /// Gets or sets whether to force parsing even when verification fails.
    /// </summary>
    public bool ForceMode { get; set; }

    /// <summary>
    /// Initializes a new instance of the PlayStation1Parser class.
    /// </summary>
    /// <param name="reader">The SectorReader to read sectors from.</param>
    public PlayStation1Parser(SectorReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// Returns the ConsoleType that this parser handles.
    /// </summary>
    /// <returns>ConsoleType.Ps1</returns>
    public ConsoleType GetConsoleType()
    {
        return ConsoleType.Ps1;
    }

    /// <summary>
    /// Returns the human-readable console name.
    /// </summary>
    /// <returns>"PS1"</returns>
    public string GetConsoleName()
    {
        return "PS1";
    }

    /// <summary>
    /// Parses the first data track using ISO 9660.
    /// </summary>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool Parse(FsNode rootNode)
    {
        return ParseTrack(rootNode, FindDataTrack());
    }

    /// <summary>
    /// Parses a specific track using ISO 9660.
    /// </summary>
    /// <param name="track">The track to parse.</param>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool ParseTrack(FsNode rootNode, TrackInfo track)
    {
        var parser = new Iso9660Parser(_reader);
        return parser.Parse(rootNode, track);
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
/// Auto-detect parser for PlayStation discs. Uses ISO 9660 on the first data track.
/// </summary>
internal class PlayStationAutoDetectParser : IConsoleParser
{
    private readonly SectorReader _reader;

    /// <summary>
    /// Gets or sets whether to force parsing even when verification fails.
    /// </summary>
    public bool ForceMode { get; set; }

    internal PlayStationAutoDetectParser(SectorReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// Returns the ConsoleType that this parser handles.
    /// </summary>
    /// <returns>ConsoleType.Ps1</returns>
    public ConsoleType GetConsoleType()
    {
        return ConsoleType.PlayStation;
    }

    /// <summary>
    /// Returns the human-readable console name.
    /// </summary>
    /// <returns>"PS1"</returns>
    public string GetConsoleName()
    {
        return "PlayStation (Auto)";
    }

    /// <summary>
    /// Parses the first data track using ISO 9660.
    /// </summary>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool Parse(FsNode rootNode)
    {
        return ParseTrack(rootNode, FindDataTrack());
    }

    /// <summary>
    /// Parses a specific track using ISO 9660.
    /// </summary>
    /// <param name="track">The track to parse.</param>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool ParseTrack(FsNode rootNode, TrackInfo track)
    {
        var parser = new Iso9660Parser(_reader);
        return parser.Parse(rootNode, track);
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
/// Parses PlayStation 2 disc images using ISO 9660 on the first data track.
/// </summary>
public class PlayStation2Parser : IConsoleParser
{
    private readonly SectorReader _reader;

    /// <summary>
    /// Gets or sets whether to force parsing even when verification fails.
    /// </summary>
    public bool ForceMode { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayStation2Parser"/> class.
    /// </summary>
    /// <param name="reader">The sector reader to use for reading disc data.</param>
    public PlayStation2Parser(SectorReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// Returns the ConsoleType that this parser handles.
    /// </summary>
    /// <returns>ConsoleType.Ps1</returns>
    public ConsoleType GetConsoleType()
    {
        return ConsoleType.Ps2;
    }

    /// <summary>
    /// Returns the human-readable console name.
    /// </summary>
    /// <returns>"PS1"</returns>
    public string GetConsoleName()
    {
        return "PS2";
    }

    /// <summary>
    /// Parses the first data track using ISO 9660.
    /// </summary>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool Parse(FsNode rootNode)
    {
        return ParseTrack(rootNode, FindDataTrack());
    }

    /// <summary>
    /// Parses a specific track using ISO 9660.
    /// </summary>
    /// <param name="track">The track to parse.</param>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool ParseTrack(FsNode rootNode, TrackInfo track)
    {
        var parser = new Iso9660Parser(_reader);
        return parser.Parse(rootNode, track);
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
/// Parses PlayStation 3 disc images using UDF, falling back to ISO 9660 if UDF fails.
/// </summary>
public class PlayStation3Parser : IConsoleParser
{
    private readonly SectorReader _reader;

    /// <summary>
    /// Gets or sets whether to force parsing even when verification fails.
    /// </summary>
    public bool ForceMode { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayStation3Parser"/> class.
    /// </summary>
    /// <param name="reader">The sector reader to use for reading disc data.</param>
    public PlayStation3Parser(SectorReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// Returns the ConsoleType that this parser handles.
    /// </summary>
    /// <returns>ConsoleType.Ps3</returns>
    public ConsoleType GetConsoleType()
    {
        return ConsoleType.Ps3;
    }

    /// <summary>
    /// Returns the human-readable console name.
    /// </summary>
    /// <returns>"PS3"</returns>
    public string GetConsoleName()
    {
        return "PS3";
    }

    /// <summary>
    /// Parses the first data track using ISO 9660.
    /// </summary>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool Parse(FsNode rootNode)
    {
        return ParseTrack(rootNode, FindDataTrack());
    }

    /// <summary>
    /// Parses a specific track using ISO 9660.
    /// </summary>
    /// <param name="track">The track to parse.</param>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool ParseTrack(FsNode rootNode, TrackInfo track)
    {
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


