using System.Text;

namespace VideoGameFileSystemParser.Parsers;

/// <summary>
/// Parses the Opera file system used on 3DO Interactive Multiplayer discs.
/// </summary>
public class ThreeDoParser
{
    private readonly SectorReader _reader;

    private static readonly byte[] OperaMagic = [0x01, 0x5A, 0x5A, 0x5A, 0x5A, 0x5A, 0x01];

    private const int DirectoryEntrySize = 0x44;
    private const uint FileFlagsMask = 0xFF;
    private const uint FileTypeDirectory = 7;
    private const uint FlagLastEntry = 0x80000000;
    private const uint FlagLastEntryInBlock = 0x40000000;

    /// <summary>
    /// Initializes a new instance of the ThreeDoParser class.
    /// </summary>
    /// <param name="reader">The SectorReader to read sectors from.</param>
    public ThreeDoParser(SectorReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// Parses the Opera file system and builds the directory tree.
    /// </summary>
    /// <param name="track">Optional track.</param>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool Parse(FsNode rootNode, TrackInfo? track = null)
    {
        _reader.Reset();
        if (track != null)
            _reader.SetTrack(track, true);
        else
            _reader.SetTrack(null);

        var sectorData = new byte[2048];
        var trackStart = track?.StartLba ?? 0;
        var foundVh = _reader.ReadSector(trackStart, sectorData) && CheckMagic(sectorData, 0, OperaMagic);

        if (!foundVh)
        {
            for (uint i = 0; i < 100; i++)
            {
                if (_reader.ReadSector(trackStart + i, sectorData) && CheckMagic(sectorData, 0, OperaMagic))
                {
                    trackStart += i;
                    foundVh = true;
                    break;
                }
            }
        }

        if (!foundVh) return false;

        var blockSize = Be32(sectorData, 0x4C);
        if (blockSize == 0)
        {
            blockSize = 2048;
        }

        var blockSizeRatio = blockSize / 2048;
        if (blockSizeRatio == 0)
        {
            blockSizeRatio = 1;
        }

        var firstRootBlock = (int)Be32(sectorData, 0x64);

        rootNode.Name = "/";
        rootNode.IsDirectory = true;
        rootNode.Lba = (uint)(trackStart + firstRootBlock * blockSizeRatio);
        rootNode.Size = 0;

        return ParseDirectory(firstRootBlock, blockSizeRatio, rootNode, trackStart);
    }

    private bool ParseDirectory(int firstBlock, uint blockSizeRatio, FsNode parentNode, uint trackStart)
    {
        var sectorData = new byte[2048];
        var nextBlock = firstBlock;
        var visited = new HashSet<int>();

        while (true)
        {
            if (!visited.Add(nextBlock)) break;

            var currentLba = (uint)(trackStart + nextBlock * blockSizeRatio);
            if (!_reader.ReadSector(currentLba, sectorData)) return false;

            var headerNextBlock = (int)Be32(sectorData, 0x00);
            var firstEntryOffset = Be32(sectorData, 0x10);

            if (firstEntryOffset is 0 or >= 2048)
            {
                firstEntryOffset = 0x14;
            }

            uint lastEntryFlags = 0;
            var hasEntries = false;

            var pos = (int)firstEntryOffset;
            while (pos + DirectoryEntrySize <= 2048)
            {
                var flags = Be32(sectorData, pos);

                if (flags == 0 && sectorData[pos + 0x20] == 0) break;

                var fileType = flags & FileFlagsMask;
                var isDir = fileType == FileTypeDirectory;

                var name = Encoding.ASCII.GetString(sectorData, pos + 0x20, 32).TrimEnd('\0');
                var byteCount = Be32(sectorData, pos + 0x10);
                var lastCopy = Be32(sectorData, pos + 0x40);
                var extent = Be32(sectorData, pos + 0x44);

                lastEntryFlags = flags;
                hasEntries = true;

                var child = new FsNode
                {
                    Name = name,
                    Lba = (uint)(trackStart + (long)extent * blockSizeRatio),
                    Size = byteCount,
                    IsDirectory = isDir
                };

                if (isDir && extent != 0 && extent != nextBlock)
                    ParseDirectory((int)extent, blockSizeRatio, child, trackStart);

                parentNode.Children.Add(child);

                if ((flags & FlagLastEntry) != 0 || (flags & FlagLastEntryInBlock) != 0)
                    break;

                pos += DirectoryEntrySize + (int)(lastCopy + 1) * 4;
            }

            if (hasEntries && (lastEntryFlags & FlagLastEntry) != 0)
                break;

            if (headerNextBlock == -1)
                break;

            nextBlock = firstBlock + headerNextBlock;
        }

        return true;
    }

    private static bool CheckMagic(byte[] d, int o, byte[] m)
    {
        for (var i = 0; i < m.Length; i++)
        {
            if (d[o + i] != m[i]) return false;
        }

        return true;
    }

    private static uint Be32(byte[] d, int o)
    {
        return (uint)((d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]);
    }
}
