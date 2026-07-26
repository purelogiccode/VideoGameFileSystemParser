using System.Text;

namespace VideoGameFileSystemParser.Parsers;

/// <summary>
/// Parses the CD-i file system, based on ISO 9660 with custom extensions for interleaved data.
/// </summary>
public class CDiFsParser
{
    private readonly SectorReader _reader;
    private static readonly Encoding Encoding = Encoding.GetEncoding("iso8859-1");

    private const int CdiRecordHeaderSize = 33;
    private const int CdiSystemAreaSize = 12;

    /// <summary>
    /// Initializes a new instance of the CDiFsParser class.
    /// </summary>
    /// <param name="reader">The SectorReader to read sectors from.</param>
    public CDiFsParser(SectorReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// Parses the CD-i file system and builds the directory tree.
    /// </summary>
    /// <param name="track">Optional track.</param>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool Parse(FsNode rootNode, TrackInfo? track = null)
    {
        var sectorData = new byte[2048];
        var trackStart = track?.StartLba ?? 0u;

        if (TryParseFromLba(rootNode, 0u, trackStart, sectorData))
            return true;

        if (track != null)
            return TryParseFromLba(rootNode, trackStart, trackStart, sectorData);

        return false;
    }

    private bool TryParseFromLba(FsNode rootNode, uint searchLba, uint baseLba, byte[] sectorData)
    {
        _reader.Reset();
        _reader.SetTrack(null);

        var bestVdData = SearchForVolumeDescriptor(searchLba, sectorData);
        if (bestVdData == null) return false;

        var pathTableSize = BeU32(bestVdData, 136);
        var pathTableAddr = BeU32(bestVdData, 148);

        if (pathTableSize == 0 || pathTableAddr == 0) return false;

        var pathTable = ParsePathTable(baseLba + pathTableAddr, pathTableSize);
        if (pathTable == null || pathTable.Count == 0) return false;

        var rootPt = pathTable[0];
        var rootLba = baseLba + rootPt.Lba;

        var rootDirBytes = _reader.ReadSector(rootLba);
        if (rootDirBytes == null) return false;

        var rootRecord = ParseRootRecord(rootDirBytes);
        if (rootRecord == null) return false;

        rootNode.Lba = rootLba;
        rootNode.Size = rootRecord.Size;

        var rootDir = new CdiDirContext
        {
            Lba = rootLba,
            Size = rootRecord.Size,
            PathTableIndex = 1
        };

        ParseDirectory(rootNode, rootDir, pathTable, baseLba);

        rootNode.Name = "/";
        rootNode.IsDirectory = true;
        return true;
    }

    private byte[]? SearchForVolumeDescriptor(uint startLba, byte[] sectorData)
    {
        for (uint offset = 0; offset < 100; offset++)
        {
            var currentLba = startLba + offset;
            if (!_reader.ReadSector(currentLba, sectorData)) continue;

            var type = sectorData[0];
            var hasCdi = CheckSig(sectorData, 1, "CD-I ");
            var hasIso = CheckSig(sectorData, 1, "CD001");
            var hasHighSierra = CheckSig(sectorData, 9, "CDROM");

            if (hasCdi || hasIso || hasHighSierra)
            {
                var copy = new byte[2048];
                Array.Copy(sectorData, copy, 2048);
                return copy;
            }

            if (type == 255 && offset >= 16) break;
        }

        return null;
    }

    private List<CdiPathEntry>? ParsePathTable(uint pathTableLba, uint pathTableSize)
    {
        var table = new List<CdiPathEntry>();

        var sectorsNeeded = (pathTableSize + 2047) / 2048;
        var buf = new byte[sectorsNeeded * 2048];

        for (uint i = 0; i < sectorsNeeded; i++)
        {
            var sector = _reader.ReadSector(pathTableLba + i);
            if (sector == null) return null;

            Array.Copy(sector, 0, buf, (int)(i * 2048), 2048);
        }

        uint off = 0;
        while (off < pathTableSize)
        {
            if (off + 8 > pathTableSize) break;

            var nameLen = buf[off];
            if (nameLen == 0) break;

            var startLbn = BeU32(buf, (int)off + 2);
            var parentDirNo = BeU16(buf, (int)off + 6);

            off += 8;
            var name = Encoding.GetString(buf, (int)off, nameLen);

            table.Add(new CdiPathEntry
            {
                Lba = startLbn,
                Name = name,
                Parent = parentDirNo
            });

            off += nameLen;
            if ((nameLen & 1) != 0)
            {
                off++;
            }
        }

        return table;
    }

    private static CdiRootRecord? ParseRootRecord(byte[] sectorData)
    {
        if (sectorData.Length < CdiRecordHeaderSize) return null;

        var recordLen = sectorData[0];
        if (recordLen is 0 or < CdiRecordHeaderSize) return null;

        var size = BeU32(sectorData, 14);

        return new CdiRootRecord
        {
            Size = size
        };
    }

    private void ParseDirectory(FsNode dirNode, CdiDirContext dirCtx,
        List<CdiPathEntry> pathTable, uint trackStart)
    {
        var size = dirCtx.Size == 0 ? 2048u : dirCtx.Size;
        var sectorsToRead = Math.Min((size + 2047) / 2048, 4096);

        for (uint i = 0; i < sectorsToRead; i++)
        {
            var sector = _reader.ReadSector(dirCtx.Lba + i);
            if (sector == null) break;

            uint pos = 0;
            var hasRecords = false;

            while (pos <= 2048 - CdiRecordHeaderSize)
            {
                var recordLen = sector[pos];
                if (recordLen == 0)
                    break;

                if (recordLen < CdiRecordHeaderSize || pos + recordLen > 2048)
                    break;

                hasRecords = true;

                var startLbn = BeU32(sector, (int)pos + 6);
                var fileSize = BeU32(sector, (int)pos + 14);
                var nameLen = sector[pos + 32];

                if (nameLen == 0)
                {
                    pos += recordLen;
                    if ((pos & 1) != 0)
                    {
                        pos++;
                    }

                    continue;
                }

                if (33 + nameLen > recordLen || pos + 33 + nameLen > 2048) break;

                if (nameLen == 1)
                {
                    var nameByte = sector[pos + 33];
                    if (nameByte is 0x00 or 0x01)
                    {
                        pos += recordLen;
                        if ((pos & 1) != 0)
                        {
                            pos++;
                        }

                        continue;
                    }
                }

                var name = Encoding.GetString(sector, (int)pos + 33, nameLen);

                var saOff = (int)pos + 33 + nameLen;
                if ((saOff & 1) != 0)
                {
                    saOff++;
                }

                var isDir = false;
                byte fileNumber = 0;
                var isInterleaved = false;

                if (saOff + CdiSystemAreaSize <= pos + recordLen)
                {
                    var attrs = BeU16(sector, (uint)(saOff + 4));
                    fileNumber = sector[saOff + 8];

                    isDir = (attrs & 0x8000) != 0;
                    isInterleaved = (attrs & 0x2000) != 0;
                }

                switch (isDir)
                {
                    case true when pathTable.Count > 0:
                    {
                        var subDirs = GetSubdirsFromPathTable(dirCtx.PathTableIndex, pathTable, trackStart);
                        if (subDirs.Count > 0)
                        {
                            foreach (var sub in subDirs)
                            {
                                var child = new FsNode
                                {
                                    Name = sub.Name,
                                    Lba = sub.Lba,
                                    Size = sub.Size,
                                    IsDirectory = true,
                                    FileNumber = 0
                                };

                                var childCtx = new CdiDirContext
                                {
                                    Lba = sub.Lba,
                                    Size = sub.Size,
                                    PathTableIndex = sub.PathTableIndex
                                };

                                ParseDirectory(child, childCtx, pathTable, trackStart);
                                dirNode.Children.Add(child);
                            }
                        }

                        break;
                    }
                    case false:
                    {
                        var child = new FsNode
                        {
                            Name = name,
                            Lba = trackStart + startLbn,
                            Size = fileSize,
                            IsDirectory = false,
                            FileNumber = fileNumber,
                            IsInterleaved = isInterleaved
                        };
                        dirNode.Children.Add(child);
                        break;
                    }
                    default:
                    {
                        var child = new FsNode
                        {
                            Name = name,
                            Lba = trackStart + startLbn,
                            Size = fileSize,
                            IsDirectory = true,
                            FileNumber = fileNumber,
                            IsInterleaved = isInterleaved
                        };
                        var childCtx = new CdiDirContext
                        {
                            Lba = child.Lba,
                            Size = fileSize,
                            PathTableIndex = dirCtx.PathTableIndex
                        };
                        ParseDirectory(child, childCtx, pathTable, trackStart);
                        dirNode.Children.Add(child);
                        break;
                    }
                }

                pos += recordLen;
                if ((pos & 1) != 0)
                {
                    pos++;
                }
            }

            if (!hasRecords) break;
        }
    }

    private List<CdiSubDirEntry> GetSubdirsFromPathTable(int parentIndex,
        List<CdiPathEntry> pathTable, uint trackStart)
    {
        var result = new List<CdiSubDirEntry>();

        for (var i = 0; i < pathTable.Count; i++)
        {
            var entry = pathTable[i];
            if (entry.Parent != parentIndex || i == 0) continue;

            var dirLba = trackStart + entry.Lba;
            var sector = _reader.ReadSector(dirLba);
            if (sector == null) continue;

            var record = ParseRootRecord(sector);
            if (record == null) continue;

            result.Add(new CdiSubDirEntry
            {
                Name = entry.Name,
                Lba = dirLba,
                Size = record.Size,
                PathTableIndex = i + 1
            });
        }

        return result;
    }

    private static bool CheckSig(byte[] d, int o, string s)
    {
        if (o + s.Length > d.Length) return false;

        for (var i = 0; i < s.Length; i++)
            if (d[o + i] != s[i])
                return false;

        return true;
    }

    private static uint BeU32(byte[] d, int o)
    {
        return (uint)((d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]);
    }

    private static ushort BeU16(byte[] d, int o)
    {
        return (ushort)((d[o] << 8) | d[o + 1]);
    }

    private static ushort BeU16(byte[] d, uint o)
    {
        return (ushort)((d[o] << 8) | d[o + 1]);
    }

    private class CdiPathEntry
    {
        public uint Lba;
        public string Name = "";
        public ushort Parent;
    }

    private class CdiRootRecord
    {
        public uint Size;
    }

    private class CdiDirContext
    {
        public uint Lba;
        public uint Size;
        public int PathTableIndex;
    }

    private class CdiSubDirEntry
    {
        public string Name = "";
        public uint Lba;
        public uint Size;
        public int PathTableIndex;
    }
}
