using VideoGameFileSystemParser.Interfaces;

namespace VideoGameFileSystemParser.Parsers.Systems;

/// <summary>
/// Parses original Xbox disc images using the XDVDFS file system.
/// </summary>
internal class XboxParser : IConsoleParser
{
    private readonly SectorReader _reader;

    /// <summary>
    /// Gets or sets whether to force parsing even when verification fails.
    /// </summary>
    public bool ForceMode { get; set; }

    /// <summary>
    /// Initializes a new instance of the XboxParser class.
    /// </summary>
    /// <param name="reader">The SectorReader to read sectors from.</param>
    internal XboxParser(SectorReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// Returns the ConsoleType that this parser handles.
    /// </summary>
    /// <returns>ConsoleType.Xbox</returns>
    public ConsoleType GetConsoleType()
    {
        return ConsoleType.Xbox;
    }

    /// <summary>
    /// Returns the human-readable console name.
    /// </summary>
    /// <returns>"Xbox"</returns>
    public string GetConsoleName()
    {
        return "Xbox";
    }

    /// <summary>
    /// Parses the first data track using the XDVDFS parser.
    /// </summary>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool Parse(FsNode rootNode)
    {
        return ParseTrack(rootNode, FindDataTrack());
    }

    /// <summary>
    /// Parses a specific track using the XDVDFS parser.
    /// </summary>
    /// <param name="track">The track to parse.</param>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool ParseTrack(FsNode rootNode, TrackInfo track)
    {
        var parser = new XdvdfsParser(_reader);
        parser.SetTrack(track);
        return parser.Parse(rootNode);
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
/// Parses Xbox 360 disc images using the XDVDFS file system.
/// </summary>
internal class Xbox360Parser : IConsoleParser
{
    private readonly SectorReader _reader;

    /// <summary>
    /// Gets or sets whether to force parsing even when verification fails.
    /// </summary>
    public bool ForceMode { get; set; }

    internal Xbox360Parser(SectorReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// Returns the ConsoleType that this parser handles.
    /// </summary>
    /// <returns>ConsoleType.Xbox</returns>
    public ConsoleType GetConsoleType()
    {
        return ConsoleType.Xbox360;
    }

    /// <summary>
    /// Returns the human-readable console name.
    /// </summary>
    /// <returns>"Xbox"</returns>
    public string GetConsoleName()
    {
        return "Xbox 360";
    }

    /// <summary>
    /// Parses the first data track using the XDVDFS parser.
    /// </summary>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool Parse(FsNode rootNode)
    {
        return ParseTrack(rootNode, FindDataTrack());
    }

    /// <summary>
    /// Parses a specific track using the XDVDFS parser.
    /// </summary>
    /// <param name="track">The track to parse.</param>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool ParseTrack(FsNode rootNode, TrackInfo track)
    {
        var parser = new XdvdfsParser(_reader);
        parser.SetTrack(track);
        return parser.Parse(rootNode);
    }

    private TrackInfo FindDataTrack()
    {
        foreach (var t in _reader.Tracks)
            if (t.IsDataTrack)
                return t;

        return _reader.Tracks.Count > 0 ? _reader.Tracks[0] : new TrackInfo();
    }
}


