using System.Text;
using VideoGameFileSystemParser.Interfaces;

namespace VideoGameFileSystemParser.Parsers.Systems;

/// <summary>
/// Parses Sega Dreamcast GD-ROM disc images. Prefers tracks containing the IP.BIN boot sector signature.
/// </summary>
public class DreamcastParser : IConsoleParser
{
    private const string IpBinSignature = "SEGA SEGAKATANA ";

    private readonly SectorReader _reader;

    /// <inheritdoc />
    public bool ForceMode { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DreamcastParser"/> class.
    /// </summary>
    /// <param name="reader">The sector reader to use for reading disc data.</param>
    public DreamcastParser(SectorReader reader)
    {
        _reader = reader;
    }

    /// <inheritdoc />
    public ConsoleType GetConsoleType()
    {
        return ConsoleType.Dreamcast;
    }

    /// <inheritdoc />
    public string GetConsoleName()
    {
        return "Dreamcast";
    }

    /// <inheritdoc />
    public bool Parse(FsNode rootNode)
    {
        var dataTracks = new List<TrackInfo>();
        for (var i = _reader.Tracks.Count - 1; i >= 0; i--)
        {
            if (_reader.Tracks[i].IsDataTrack)
                dataTracks.Add(_reader.Tracks[i]);
        }

        if (dataTracks.Count == 0)
            return false;

        foreach (var track in dataTracks.OrderByDescending(HasIpBin))
        {
            if (ParseTrack(rootNode, track))
                return true;
        }

        return false;
    }

    /// <inheritdoc />
    public bool ParseTrack(FsNode rootNode, TrackInfo track)
    {
        var temp = new FsNode();
        var parser = new Iso9660Parser(_reader);

        if (!parser.Parse(temp, track) || temp.Children.Count == 0)
            return false;

        rootNode.Name = temp.Name;
        rootNode.IsDirectory = true;
        rootNode.Lba = temp.Lba;
        rootNode.Size = temp.Size;
        rootNode.Extents.Clear();
        rootNode.Extents.AddRange(temp.Extents);
        rootNode.Children.Clear();
        rootNode.Children.AddRange(temp.Children);
        return true;
    }

    private bool HasIpBin(TrackInfo track)
    {
        _reader.Reset();
        _reader.SetTrack(track, true);

        var sec = new byte[2048];
        var ok = _reader.ReadSector(track.StartLba, sec) &&
                 Encoding.ASCII.GetString(sec, 0, IpBinSignature.Length) == IpBinSignature;

        _reader.Reset();
        return ok;
    }
}

/// <summary>
/// Parses Philips CD-i disc images using CDiFsParser, falling back to ISO 9660.
/// </summary>
public class CDiParser : IConsoleParser
{
    private readonly SectorReader _reader;

    /// <inheritdoc />
    public bool ForceMode { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CDiParser"/> class.
    /// </summary>
    /// <param name="reader">The sector reader to use for reading disc data.</param>
    public CDiParser(SectorReader reader)
    {
        _reader = reader;
    }

    /// <inheritdoc />
    public ConsoleType GetConsoleType()
    {
        return ConsoleType.CDi;
    }

    /// <inheritdoc />
    public string GetConsoleName()
    {
        return "CD-i";
    }

    /// <inheritdoc />
    public bool Parse(FsNode rootNode)
    {
        return ParseTrack(rootNode, FindDataTrack());
    }

    /// <inheritdoc />
    public bool ParseTrack(FsNode rootNode, TrackInfo track)
    {
        var parser = new CDiFsParser(_reader);
        if (parser.Parse(rootNode, track))
            return true;

        var isoParser = new Iso9660Parser(_reader);
        if (isoParser.Parse(rootNode, track))
            return true;

        return false;
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
/// Parses 3DO Interactive Multiplayer disc images using the Opera file system parser.
/// </summary>
public class ThreeDoConsoleParser : IConsoleParser
{
    private readonly SectorReader _reader;

    /// <inheritdoc />
    public bool ForceMode { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThreeDoConsoleParser"/> class.
    /// </summary>
    /// <param name="reader">The sector reader to use for reading disc data.</param>
    public ThreeDoConsoleParser(SectorReader reader)
    {
        _reader = reader;
    }

    /// <inheritdoc />
    public ConsoleType GetConsoleType()
    {
        return ConsoleType.ThreeDo;
    }

    /// <inheritdoc />
    public string GetConsoleName()
    {
        return "3DO";
    }

    /// <inheritdoc />
    public bool Parse(FsNode rootNode)
    {
        return ParseTrack(rootNode, FindDataTrack());
    }

    /// <inheritdoc />
    public bool ParseTrack(FsNode rootNode, TrackInfo track)
    {
        var parser = new ThreeDoParser(_reader);
        if (parser.Parse(rootNode, track))
            return true;

        var isoParser = new Iso9660Parser(_reader);
        if (isoParser.Parse(rootNode, track))
            return true;

        return false;
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
/// Provides raw sector passthrough access, exposing the image as "image.iso".
/// </summary>
internal class GenericIsoRawParser : IConsoleParser
{
    private readonly SectorReader _reader;

    /// <inheritdoc />
    public bool ForceMode { get; set; }

    public GenericIsoRawParser(SectorReader reader)
    {
        _reader = reader;
    }

    /// <inheritdoc />
    public ConsoleType GetConsoleType()
    {
        return ConsoleType.GenericIsoRaw;
    }

    /// <inheritdoc />
    public string GetConsoleName()
    {
        return "Generic ISO Raw";
    }

    /// <inheritdoc />
    public bool Parse(FsNode rootNode)
    {
        rootNode.Name = "/";
        rootNode.IsDirectory = true;
        rootNode.Lba = 0;
        rootNode.Children.Add(new FsNode
        {
            Name = "image.iso",
            Lba = 0,
            Size = _reader.TotalBytes,
            IsDirectory = false,
            IsRawPassthrough = true
        });
        return true;
    }

    /// <inheritdoc />
    public bool ParseTrack(FsNode rootNode, TrackInfo track)
    {
        return Parse(rootNode);
    }
}

/// <summary>
/// Generic ISO 9660 parser for standard data discs without console-specific handling.
/// </summary>
public class GenericIso9660Parser : IConsoleParser
{
    private readonly SectorReader _reader;

    /// <inheritdoc />
    public bool ForceMode { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericIso9660Parser"/> class.
    /// </summary>
    /// <param name="reader">The sector reader to use for reading disc data.</param>
    public GenericIso9660Parser(SectorReader reader)
    {
        _reader = reader;
    }

    /// <inheritdoc />
    public ConsoleType GetConsoleType()
    {
        return ConsoleType.GenericIso9660;
    }

    /// <inheritdoc />
    public string GetConsoleName()
    {
        return "Generic ISO 9660";
    }

    /// <inheritdoc />
    public bool Parse(FsNode rootNode)
    {
        var track = FindDataTrack();
        if (track == null)
            return false;

        return ParseTrack(rootNode, track);
    }

    /// <inheritdoc />
    public bool ParseTrack(FsNode rootNode, TrackInfo track)
    {
        var parser = new Iso9660Parser(_reader);
        return parser.Parse(rootNode, track);
    }

    private TrackInfo? FindDataTrack()
    {
        foreach (var t in _reader.Tracks)
            if (t.IsDataTrack)
                return t;

        return _reader.Tracks.FirstOrDefault();
    }
}

/// <summary>
/// Parses VM Labs Nuon DVD-ROM disc images using UDF, falling back to ISO 9660 if UDF fails.
/// </summary>
internal class NuonParser : IConsoleParser
{
    private readonly SectorReader _reader;

    /// <inheritdoc />
    public bool ForceMode { get; set; }

    internal NuonParser(SectorReader reader)
    {
        _reader = reader;
    }

    /// <inheritdoc />
    public ConsoleType GetConsoleType()
    {
        return ConsoleType.Nuon;
    }

    /// <inheritdoc />
    public string GetConsoleName()
    {
        return "Nuon";
    }

    /// <inheritdoc />
    public bool Parse(FsNode rootNode)
    {
        return ParseTrack(rootNode, FindDataTrack());
    }

    /// <inheritdoc />
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
