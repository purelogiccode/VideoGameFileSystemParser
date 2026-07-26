using System.Text;
using VideoGameFileSystemParser.Interfaces;

namespace VideoGameFileSystemParser.Parsers.Systems;

/// <summary>
/// Parses NEC PC Engine CD/TurboGrafx-CD disc images. Locates the boot signature, attempts ISO 9660, falls back to raw track exposure.
/// </summary>
public class PcEngineCdParser : IConsoleParser
{
    private const string Signature = "PC Engine CD-ROM SYSTEM";
    private const string GamesExpressSignature = "GAMES EXPRESS CD CARD";
    private const string PcEngineString = "PC ENGINE";
    private const uint ZeroScanLimit = 600;

    private readonly SectorReader _reader;

    /// <summary>
    /// Gets or sets whether to force parsing even when the boot signature is not found.
    /// </summary>
    public bool ForceMode { get; set; }

    /// <summary>
    /// Initializes a new instance of the PcEngineCdParser class.
    /// </summary>
    /// <param name="reader">The SectorReader to read sectors from.</param>
    public PcEngineCdParser(SectorReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// Returns the ConsoleType that this parser handles.
    /// </summary>
    /// <returns>ConsoleType.PcEngineCd</returns>
    public ConsoleType GetConsoleType()
    {
        return ConsoleType.PcEngineCd;
    }

    /// <summary>
    /// Returns the human-readable console name.
    /// </summary>
    /// <returns>"PC Engine CD"</returns>
    public string GetConsoleName()
    {
        return "PC Engine CD";
    }

    /// <summary>
    /// Parses all data tracks. Attempts ISO 9660 first, falls back to raw track files.
    /// </summary>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool Parse(FsNode rootNode)
    {
        var dataTracks = _reader.Tracks.Where(static t => t.IsDataTrack).ToList();
        if (dataTracks.Count == 0)
            return false;

        var dataStarts = new Dictionary<int, uint>();

        foreach (var track in dataTracks)
        {
            var dataStart = FindDataAreaStart(track, out _);
            dataStarts[track.Index] = dataStart;
        }

        var bootTrack = dataTracks[0];
        if (TryParseIso9660(rootNode, bootTrack, dataStarts[bootTrack.Index]))
            return true;

        rootNode.Name = "/";
        rootNode.IsDirectory = true;

        foreach (var track in dataTracks)
        {
            var dataStart = dataStarts[track.Index];
            var skipped = dataStart - track.StartLba;
            if (skipped >= track.Frames)
                continue;

            var frames = track.Frames - skipped;
            var size = (ulong)frames * 2048;

            var node = new FsNode
            {
                Name = $"TRACK{track.Index:D2}.iso",
                Lba = dataStart,
                Size = size,
                IsDirectory = false
            };
            node.Extents.Add(new FsExtent { Lba = dataStart, Size = size });
            rootNode.Children.Add(node);
        }

        return rootNode.Children.Count > 0;
    }

    /// <summary>
    /// Parses a specific track using ISO 9660, falling back to raw track file.
    /// </summary>
    /// <param name="track">The track to parse.</param>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool ParseTrack(FsNode rootNode, TrackInfo track)
    {
        var dataStart = FindDataAreaStart(track, out var hasSignature);
        if (!hasSignature && !ForceMode)
            return false;

        if (TryParseIso9660(rootNode, track, dataStart))
            return true;

        var skipped = dataStart - track.StartLba;
        if (skipped >= track.Frames)
            return false;

        var frames = track.Frames - skipped;
        var size = (ulong)frames * 2048;

        rootNode.Name = "/";
        rootNode.IsDirectory = true;

        var node = new FsNode
        {
            Name = $"TRACK{track.Index:D2}.iso",
            Lba = dataStart,
            Size = size,
            IsDirectory = false
        };
        node.Extents.Add(new FsExtent { Lba = dataStart, Size = size });
        rootNode.Children.Add(node);

        return true;
    }

    private uint FindDataAreaStart(TrackInfo track, out bool hasSignature)
    {
        hasSignature = false;

        var candidates = new List<uint>();
        if (track.Pregap > 0 && track.Pregap < track.Frames &&
            track.Metadata.Contains("PGTYPE:V", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(track.StartLba + track.Pregap);
        }

        candidates.Add(track.StartLba);

        var firstNonZero = FindFirstNonZeroSector(track);
        if (firstNonZero.HasValue && !candidates.Contains(firstNonZero.Value))
            candidates.Add(firstNonZero.Value);

        var sector = new byte[2048];
        _reader.Reset();
        _reader.SetTrack(track, true);

        try
        {
            foreach (var candidate in candidates)
            {
                foreach (var offset in new uint[] { 1, 0 })
                {
                    var lba = candidate + offset;
                    if (lba >= track.StartLba + track.Frames)
                        continue;

                    if (!_reader.ReadSector(lba, sector))
                        continue;

                    if (HasBootSignature(sector))
                    {
                        hasSignature = true;
                        return candidate;
                    }
                }
            }

            return candidates[0];
        }
        finally
        {
            _reader.Reset();
        }
    }

    private uint? FindFirstNonZeroSector(TrackInfo track)
    {
        var sector = new byte[2048];
        _reader.Reset();
        _reader.SetTrack(track, true);

        try
        {
            var limit = Math.Min(ZeroScanLimit, track.Frames);
            for (uint rel = 0; rel < limit; rel++)
            {
                if (!_reader.ReadSector(track.StartLba + rel, sector))
                    continue;

                foreach (var t in sector)
                {
                    if (t != 0)
                        return track.StartLba + rel;
                }
            }

            return null;
        }
        finally
        {
            _reader.Reset();
        }
    }

    private static bool HasBootSignature(byte[] sector)
    {
        if (sector.Length < 64)
            return false;

        var descriptor = Encoding.ASCII.GetString(sector, 0x20, Signature.Length);
        if (descriptor == Signature)
            return true;

        var gamesExpress = Encoding.ASCII.GetString(sector, 0, Math.Min(sector.Length, 128));
        if (gamesExpress.Contains(GamesExpressSignature, StringComparison.Ordinal))
            return true;

        var sectorText = Encoding.ASCII.GetString(sector, 0, sector.Length);
        return sectorText.Contains(PcEngineString, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryParseIso9660(FsNode rootNode, TrackInfo track, uint dataStart)
    {
        var skipped = dataStart - track.StartLba;
        if (skipped >= track.Frames)
            return false;

        var adjusted = new TrackInfo
        {
            Index = track.Index,
            StartLba = dataStart,
            ChdOffset = track.ChdOffset + skipped,
            Frames = track.Frames - skipped,
            TrackType = track.TrackType,
            IsDataTrack = track.IsDataTrack,
            Pregap = 0,
            Postgap = track.Postgap,
            Metadata = track.Metadata
        };

        var parser = new Iso9660Parser(_reader);
        return parser.Parse(rootNode, adjusted);
    }
}
