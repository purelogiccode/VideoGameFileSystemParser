using System.Text;

namespace VideoGameFileSystemParser.Parsers;

/// <summary>
/// Parses the XDVDFS file system used on original Xbox and Xbox 360 discs.
/// </summary>
public class XdvdfsParser
{
    private readonly SectorReader _reader;
    private TrackInfo? _currentTrack;

    private static readonly Encoding XdvdfsEncoding = CreateXdvdfsEncoding();

    private static Encoding CreateXdvdfsEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252);
        }
        catch (Exception)
        {
            return Encoding.Latin1;
        }
    }

    /// <summary>
    /// Initializes a new instance of the XdvdfsParser class.
    /// </summary>
    /// <param name="reader">The SectorReader to read sectors from.</param>
    public XdvdfsParser(SectorReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// Sets the track for parsing and locks the reader to that track.
    /// </summary>
    /// <param name="track">The track to parse.</param>
    public void SetTrack(TrackInfo track)
    {
        if (track is not { Frames: > 0 }) return;

        _currentTrack = track;
        _reader.SetTrack(track, true);
    }

    private static readonly byte[] XdvdfsMagic = "MICROSOFT*XBOX*MEDIA"u8.ToArray();

    /// <summary>
    /// Parses the XDVDFS file system and builds the directory tree.
    /// </summary>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool Parse(FsNode rootNode)
    {
        _reader.Reset();
        if (_currentTrack != null)
            _reader.SetTrack(_currentTrack, true);

        var sectorData = new byte[2048];
        uint volumeOffsetSectors = 0;
        uint rootDirSector = 0;
        uint rootDirExtentSize = 0;
        var found = false;

        uint[] offsets = [32, 129856, 16672, 198176, 0];

        foreach (var offset in offsets)
        {
            if (_reader.ReadSector(offset, sectorData))
            {
                if (CheckMagic(sectorData, 0, XdvdfsMagic) && CheckMagic(sectorData, 0x7EC, XdvdfsMagic))
                {
                    rootDirSector = LeU32(sectorData, 20);
                    rootDirExtentSize = LeU32(sectorData, 24);

                    volumeOffsetSectors = offset switch
                    {
                        32 or 0 => 0,
                        129856 => 129824,
                        16672 => 16640,
                        198176 => 198144,
                        _ => 0
                    };
                    found = true;
                    break;
                }
            }
        }

        if (!found)
        {
            var sectorData2 = new byte[2048];
            for (uint offset = 0; offset < 102400; offset++)
            {
                if (offsets.Contains(offset)) continue;

                if (_reader.ReadSector(offset, sectorData2))
                {
                    if (CheckMagic(sectorData2, 0, XdvdfsMagic) && CheckMagic(sectorData2, 0x7EC, XdvdfsMagic))
                    {
                        rootDirSector = LeU32(sectorData2, 20);
                        rootDirExtentSize = LeU32(sectorData2, 24);
                        var baseCandidate = offset >= 32 ? offset - 32 : 0;
                        volumeOffsetSectors = baseCandidate;
                        found = true;
                        break;
                    }
                }
            }
        }

        if (!found) return false;

        rootNode.Name = "/";
        rootNode.IsDirectory = true;
        rootNode.Lba = volumeOffsetSectors + rootDirSector;
        rootNode.Size = rootDirExtentSize;

        var visited = new HashSet<ulong>();
        var llCompat = true;
        return ParseDirectoryTree(rootNode.Lba, 0, rootNode, volumeOffsetSectors, rootDirExtentSize, 0, visited, ref llCompat);
    }

    private bool ParseDirectoryTree(uint dirSector, uint dirOffset, FsNode parentNode, uint volumeOffsetSectors, uint dirExtentSize, int depth, HashSet<ulong> visited, ref bool llCompat)
    {
        while (true)
        {
            if (depth > 2048) return false;

            if (dirOffset >= dirExtentSize) return true;

            var absoluteSector = dirSector + dirOffset / 2048;
            var offsetInSector = dirOffset % 2048;

            var nodeId = ((ulong)absoluteSector << 32) | offsetInSector;
            if (!visited.Add(nodeId)) return true;

            var sectorData = new byte[2048];
            if (!_reader.ReadSector(absoluteSector, sectorData)) return false;

            if (offsetInSector + 14 > 2048)
            {
                var nextOffset = dirOffset + (2048 - offsetInSector);
                dirOffset = nextOffset;
                continue;
            }

            var allFf = true;
            var allZero = true;
            for (var i = 0; i < 14; i++)
            {
                var b = sectorData[offsetInSector + i];
                if (b != 0xFF)
                {
                    allFf = false;
                }

                if (b != 0x00)
                {
                    allZero = false;
                }
            }

            if (allFf || allZero)
            {
                if (dirOffset == 0) return true;

                var remainder = dirOffset % 2048;
                var nextOffset = remainder == 0 ? dirOffset + 2048 : dirOffset + (2048 - remainder);
                if (nextOffset >= dirExtentSize) return true;

                dirOffset = nextOffset;
                continue;
            }

            var leftSubTree = LeU16(sectorData, (int)offsetInSector);
            var rightSubTree = LeU16(sectorData, (int)(offsetInSector + 2));
            var startSector = LeU32(sectorData, (int)(offsetInSector + 4));
            var fileSize = LeU32(sectorData, (int)(offsetInSector + 8));
            var attributes = sectorData[offsetInSector + 12];
            var nameLen = sectorData[offsetInSector + 13];

            if (offsetInSector + 14 + nameLen > 2048)
            {
                var nextOffset = dirOffset + (2048 - offsetInSector);
                if (nextOffset >= dirExtentSize) return true;

                dirOffset = nextOffset;
                continue;
            }

            if (leftSubTree != 0 && leftSubTree != 0xFFFF)
            {
                llCompat = false;
                ParseDirectoryTree(dirSector, (uint)(leftSubTree * 4), parentNode, volumeOffsetSectors, dirExtentSize, depth + 1, visited, ref llCompat);
            }

            if (nameLen > 0)
            {
                var node = new FsNode { Name = XdvdfsEncoding.GetString(sectorData, (int)(offsetInSector + 14), nameLen), Lba = volumeOffsetSectors + startSector, Size = fileSize, IsDirectory = (attributes & 0x10) != 0 };
                node.Extents.Add(new FsExtent { Lba = node.Lba, Size = node.Size });

                if (node is { IsDirectory: true, Size: > 0 })
                {
                    var subVisited = new HashSet<ulong>();
                    var subLlCompat = llCompat;
                    ParseDirectoryTree(node.Lba, 0, node, volumeOffsetSectors, fileSize, depth + 1, subVisited, ref subLlCompat);
                }

                parentNode.Children.Add(node);
            }

            if (rightSubTree != 0 && rightSubTree != 0xFFFF)
            {
                var rightOffset = (uint)rightSubTree * 4;
                if (llCompat)
                {
                    var currentSector = (dirOffset + 14u + nameLen) / 2048;
                    if (rightOffset / 2048 > currentSector)
                    {
                        rightOffset = (currentSector + 1) * 2048;
                    }
                }

                dirOffset = rightOffset;
                continue;
            }

            return true;
        }
    }

    private static bool CheckMagic(byte[] data, int offset, byte[] magic)
    {
        for (var i = 0; i < magic.Length; i++)
            if (data[offset + i] != magic[i])
                return false;

        return true;
    }

    private static ushort LeU16(byte[] d, int o)
    {
        return (ushort)(d[o] | (d[o + 1] << 8));
    }

    private static uint LeU32(byte[] d, int o)
    {
        return (uint)(d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24));
    }
}
