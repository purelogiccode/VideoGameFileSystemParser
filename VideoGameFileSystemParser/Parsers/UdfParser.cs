using System.Text;

namespace VideoGameFileSystemParser.Parsers;

/// <summary>
/// Parses UDF (Universal Disk Format) file systems. Supports metadata partitions, allocation descriptors, embedded data, and symlinks.
/// </summary>
public class UdfParser
{
    private const int MaxDirectoryBytes = 64 * 1024 * 1024;
    private const int MaxDirectoryDepth = 64;

    private readonly SectorReader _reader;
    private uint _blockSize = 2048;

    private readonly Dictionary<ushort, uint> _physicalPartitions = [];
    private PartitionMapRef[] _maps = [];
    private readonly List<FsExtent> _metaExtents = [];

    private enum MapKind
    {
        Physical,
        Metadata,
        Virtual,
        Unsupported
    }

    private struct PartitionMapRef
    {
        public MapKind Kind;
        public ushort PartitionNumber;
        public uint MetadataFileLoc;
        public uint MetadataMirrorLoc;
    }

    /// <summary>
    /// Initializes a new instance of the UdfParser class.
    /// </summary>
    /// <param name="reader">The SectorReader to read sectors from.</param>
    public UdfParser(SectorReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// Parses the UDF file system, locating the AVDP and building the directory tree.
    /// </summary>
    /// <param name="track">Optional track.</param>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool Parse(FsNode rootNode, TrackInfo? track = null)
    {
        if (track is { Frames: > 0 })
            _reader.SetTrack(track, true);

        _physicalPartitions.Clear();
        _metaExtents.Clear();
        _maps = [];
        _blockSize = 2048;

        var sector = new byte[2048];

        if (!ReadAvdp(sector, out var vdsLoc, out var vdsLen))
            return false;

        var fsdLen = 0u;
        var fsdLbn = 0u;
        ushort fsdPart = 0;
        var haveLvd = false;

        var vdsSectors = (vdsLen + _blockSize - 1) / _blockSize;
        for (uint i = 0; i < vdsSectors; i++)
        {
            if (!_reader.ReadSector(vdsLoc + i, sector)) break;

            if (!ValidTag(sector, 0)) continue;

            var tagId = LeU16(sector, 0);
            if (tagId == 5)
            {
                // Partition Descriptor (ECMA-167 3/10.5): number @ 22, start @ 188
                var partNum = LeU16(sector, 22);
                _physicalPartitions[partNum] = LeU32(sector, 188);
            }
            else if (tagId == 6)
            {
                // Logical Volume Descriptor (ECMA-167 3/10.6)
                _blockSize = LeU32(sector, 212);
                if (_blockSize != 2048) return false;

                fsdLen = LeU32(sector, 248);
                fsdLbn = LeU32(sector, 252);
                fsdPart = LeU16(sector, 256);
                _maps = ParsePartitionMaps(sector, LeU32(sector, 268));
                haveLvd = true;
            }
            else if (tagId == 8)
            {
                break;
            }
        }

        if (!haveLvd || fsdLen == 0 || _physicalPartitions.Count == 0)
            return false;

        if (!LoadMetadataPartition())
            return false;

        if (!ResolveLba(fsdLbn, fsdPart, out var fsdLba)) return false;

        var foundFsd = false;
        Span<uint> fsdCandidates = [fsdLba, fsdLba + 1];
        foreach (var candidate in fsdCandidates)
        {
            if (!_reader.ReadSector(candidate, sector)) continue;
            if (!ValidTag(sector, 0)) continue;
            if (LeU16(sector, 0) != 256) continue;

            foundFsd = true;
            break;
        }

        if (!foundFsd) return false;

        rootNode.Name = "/";
        rootNode.IsDirectory = true;
        rootNode.NodeType = FsNodeType.Directory;

        // Root directory ICB long_ad @ 400 (ECMA-167 4/14.1)
        var rootLbn = LeU32(sector, 404);
        var rootPart = LeU16(sector, 408);
        return ReadFileEntry(rootLbn, rootPart, rootNode, 0);
    }

    private bool ReadAvdp(byte[] sector, out uint vdsLoc, out uint vdsLen)
    {
        vdsLoc = 0;
        vdsLen = 0;

        var totalSectors = _reader.UnitBytes > 0 ? (uint)(_reader.TotalBytes / _reader.UnitBytes) : 0u;
        Span<uint> candidates = [256, totalSectors >= 257 ? totalSectors - 257 : 0, totalSectors > 0 ? totalSectors - 1 : 0];

        foreach (var lba in candidates)
        {
            if (lba == 0) continue;
            if (!_reader.ReadSector(lba, sector)) continue;
            if (!ValidTag(sector, 0) || LeU16(sector, 0) != 2) continue;

            vdsLen = LeU32(sector, 16);
            vdsLoc = LeU32(sector, 20);
            if (vdsLoc != 0 && vdsLen != 0)
                return true;
        }

        return false;
    }

    private static PartitionMapRef[] ParsePartitionMaps(byte[] lvd, uint numMaps)
    {
        var maps = new List<PartitionMapRef>();
        var off = 440;

        for (uint m = 0; m < numMaps && off + 2 <= lvd.Length; m++)
        {
            int mapType = lvd[off];
            int mapLen = lvd[off + 1];
            if (mapLen < 2 || off + mapLen > lvd.Length) break;

            switch (mapType)
            {
                case 1 when mapLen >= 6:
                    maps.Add(new PartitionMapRef { Kind = MapKind.Physical, PartitionNumber = LeU16(lvd, off + 4) });
                    break;
                case 2 when mapLen >= 64:
                {
                    var id = Encoding.ASCII.GetString(lvd, off + 5, 23).TrimEnd('\0', ' ');
                    var partNum = LeU16(lvd, off + 38);

                    switch (id)
                    {
                        case "*UDF Metadata Partition":
                            maps.Add(new PartitionMapRef
                            {
                                Kind = MapKind.Metadata,
                                PartitionNumber = partNum,
                                MetadataFileLoc = LeU32(lvd, off + 40),
                                MetadataMirrorLoc = LeU32(lvd, off + 44)
                            });
                            break;
                        case "*UDF Virtual Partition":
                            maps.Add(new PartitionMapRef { Kind = MapKind.Virtual, PartitionNumber = partNum });
                            break;
                        case "*UDF Sparable Partition":
                            maps.Add(new PartitionMapRef { Kind = MapKind.Physical, PartitionNumber = partNum });
                            break;
                        default:
                            maps.Add(new PartitionMapRef { Kind = MapKind.Unsupported });
                            break;
                    }

                    break;
                }
                default:
                    maps.Add(new PartitionMapRef { Kind = MapKind.Unsupported });
                    break;
            }

            off += mapLen;
        }

        return maps.ToArray();
    }

    private bool LoadMetadataPartition()
    {
        foreach (var map in _maps)
        {
            if (map.Kind != MapKind.Metadata) continue;

            if (!_physicalPartitions.TryGetValue(map.PartitionNumber, out var physStart)) return false;

            if (LoadMetadataFile(physStart + map.MetadataFileLoc, physStart))
                return true;

            return LoadMetadataFile(physStart + map.MetadataMirrorLoc, physStart);
        }

        return true;
    }

    private bool LoadMetadataFile(uint feLba, uint physStart)
    {
        var sector = new byte[2048];
        if (!_reader.ReadSector(feLba, sector)) return false;
        if (!ValidTag(sector, 0)) return false;

        var tagId = LeU16(sector, 0);
        if (tagId is not (261 or 266)) return false;

        if (!GetAllocDescriptors(sector, tagId == 266, out var allocDesc, out var icbFlags, out _))
            return false;

        _metaExtents.Clear();
        var adType = icbFlags & 7;
        var stride = adType switch
        {
            0 => 8,
            1 => 16,
            _ => 0
        };
        if (stride == 0) return false;

        for (var off = 0; off + stride <= allocDesc.Length; off += stride)
        {
            var len = LeU32(allocDesc, off);
            var lbn = LeU32(allocDesc, off + 4);
            var extType = len >> 30;
            len &= 0x3FFFFFFF;
            if (len == 0 || extType != 0) continue;

            _metaExtents.Add(new FsExtent { Lba = physStart + lbn, Size = len });
        }

        return _metaExtents.Count > 0;
    }

    private bool ResolveLba(uint lbn, ushort partRef, out uint lba)
    {
        lba = 0;
        if (partRef >= _maps.Length) return false;

        var map = _maps[partRef];
        switch (map.Kind)
        {
            case MapKind.Physical:
            case MapKind.Virtual:
                if (!_physicalPartitions.TryGetValue(map.PartitionNumber, out var start)) return false;

                lba = start + lbn;
                return true;

            case MapKind.Metadata:
            {
                var remaining = lbn;
                foreach (var ext in _metaExtents)
                {
                    var blocks = (uint)((ext.Size + _blockSize - 1) / _blockSize);
                    if (remaining < blocks)
                    {
                        lba = ext.Lba + remaining;
                        return true;
                    }

                    remaining -= blocks;
                }

                return false;
            }

            default:
                return false;
        }
    }

    private bool ReadFileEntry(uint lbn, ushort partRef, FsNode node, int depth)
    {
        if (depth > MaxDirectoryDepth) return false;
        if (!ResolveLba(lbn, partRef, out var feLba)) return false;

        var sector = new byte[2048];
        if (!_reader.ReadSector(feLba, sector)) return false;

        var allZero = true;
        for (var i = 0; i < 16; i++)
        {
            if (sector[i] != 0)
            {
                allZero = false;
                break;
            }
        }

        if (allZero) return false;

        if (!ValidTag(sector, 0)) return false;

        var tagId = LeU16(sector, 0);
        if (tagId is not (261 or 266)) return false;

        if (!GetAllocDescriptors(sector, tagId == 266, out var allocDesc, out var icbFlags, out var adOffset))
            return false;

        var fileType = sector[27];
        node.Size = LeU64(sector, 56);
        node.IsDirectory = fileType == 4;
        node.NodeType = fileType switch
        {
            4 => FsNodeType.Directory,
            12 => FsNodeType.Symlink,
            _ => FsNodeType.File
        };
        node.ModifiedTime = ParseUdfTimestamp(sector, tagId == 266 ? 92 : 84);
        node.Extents.Clear();

        var adType = icbFlags & 7;
        switch (adType)
        {
            // Short ADs: block numbers are relative to the partition the FE is recorded in
            case 0:
            {
                for (var off = 0; off + 8 <= allocDesc.Length; off += 8)
                {
                    var len = LeU32(allocDesc, off);
                    var loc = LeU32(allocDesc, off + 4);
                    var extType = len >> 30;
                    len &= 0x3FFFFFFF;
                    if (len == 0 || extType != 0) continue;
                    if (!ResolveLba(loc, partRef, out var extLba)) continue;

                    node.Extents.Add(new FsExtent { Lba = extLba, Size = len });
                    if (node.Lba == 0)
                    {
                        node.Lba = extLba;
                    }
                }

                break;
            }
            // Long ADs: carry an explicit partition reference number
            case 1:
            {
                for (var off = 0; off + 16 <= allocDesc.Length; off += 16)
                {
                    var len = LeU32(allocDesc, off);
                    var loc = LeU32(allocDesc, off + 4);
                    var part = LeU16(allocDesc, off + 8);
                    var extType = len >> 30;
                    len &= 0x3FFFFFFF;
                    if (len == 0 || extType != 0) continue;
                    if (!ResolveLba(loc, part, out var extLba)) continue;

                    node.Extents.Add(new FsExtent { Lba = extLba, Size = len });
                    if (node.Lba == 0)
                    {
                        node.Lba = extLba;
                    }
                }

                break;
            }
            // Embedded/inline data: file content lives inside the File Entry itself
            case 3:
            {
                if (node.Size > 2048 - adOffset) return false;

                node.IsEmbedded = true;
                node.Lba = feLba;
                node.EmbeddedOffset = adOffset;
                break;
            }
            default:
                return !node.IsDirectory;
        }

        if (node.IsDirectory)
            return ParseDirectory(node, depth);

        return true;
    }

    private bool ParseDirectory(FsNode dirNode, int depth)
    {
        var data = ReadDirectoryData(dirNode);
        if (data == null) return false;

        var dataLen = data.Length;
        var pos = 0;
        while (pos + 38 <= dataLen)
        {
            if (!ValidTag(data, pos)) break;

            var tagId = LeU16(data, pos);
            if (tagId != 257) break; // FID

            var fileChar = data[pos + 18];
            var nameLen = data[pos + 19];
            var implUseLen = LeU16(data, pos + 36);

            var fidLen = 4 * ((38 + nameLen + implUseLen + 3) / 4);
            if (pos + fidLen > dataLen) break;

            var isParent = (fileChar & 0x08) != 0;
            var isDeleted = (fileChar & 0x04) != 0;

            if (!isParent && !isDeleted && nameLen > 0)
            {
                var nameOffset = pos + 38 + implUseLen;
                var name = ParseUdfName(data, nameOffset, nameLen);

                if (name.Length > 0)
                {
                    // FID ICB long_ad @ 20: block @ +4, partition @ +8
                    var icbLbn = LeU32(data, pos + 24);
                    var icbPart = LeU16(data, pos + 28);
                    var child = new FsNode { Name = name };
                    if (ReadFileEntry(icbLbn, icbPart, child, depth + 1))
                        dirNode.Children.Add(child);
                }
            }

            pos += fidLen;
        }

        return true;
    }

    private byte[]? ReadDirectoryData(FsNode dirNode)
    {
        var sector = new byte[2048];

        if (dirNode.IsEmbedded)
        {
            if (dirNode.Size == 0 || dirNode.EmbeddedOffset + dirNode.Size > 2048) return null;
            if (!_reader.ReadSector(dirNode.Lba, sector)) return null;

            var embedded = new byte[dirNode.Size];
            Array.Copy(sector, (int)dirNode.EmbeddedOffset, embedded, 0, (int)dirNode.Size);
            return embedded;
        }

        ulong totalSize = 0;
        foreach (var extent in dirNode.Extents)
        {
            totalSize += extent.Size;
        }

        if (totalSize is 0 or > MaxDirectoryBytes) return null;

        var data = new byte[totalSize];
        var written = 0;

        foreach (var extent in dirNode.Extents)
        {
            var remaining = extent.Size;
            var sectors = (uint)((extent.Size + _blockSize - 1) / _blockSize);
            for (uint s = 0; s < sectors && remaining > 0; s++)
            {
                if (!_reader.ReadSector(extent.Lba + s, sector)) return null;

                var chunk = (int)Math.Min(remaining, 2048);
                Array.Copy(sector, 0, data, written, chunk);
                written += chunk;
                remaining -= (ulong)chunk;
            }
        }

        return data;
    }

    private static bool GetAllocDescriptors(byte[] sector, bool isEfe, out byte[] allocDesc, out ushort icbFlags, out uint adOffset)
    {
        // FE (ECMA-167 4/14.9): L_EA @ 168, L_AD @ 172, ADs @ 176 + L_EA
        // EFE (ECMA-167 4/14.17): L_EA @ 208, L_AD @ 212, ADs @ 216 + L_EA
        allocDesc = [];
        adOffset = 0;
        icbFlags = LeU16(sector, 34);

        var lEa = LeU32(sector, isEfe ? 208 : 168);
        var lAd = LeU32(sector, isEfe ? 212 : 172);
        var baseOff = (isEfe ? 216 : 176) + lEa;
        if (baseOff > sector.Length) return false;

        adOffset = (uint)baseOff;
        allocDesc = new byte[lAd];
        Array.Copy(sector, (int)baseOff, allocDesc, 0, Math.Min(lAd, (uint)(sector.Length - baseOff)));
        return true;
    }

    private static string ParseUdfName(byte[] data, int offset, int length)
    {
        if (length <= 1) return "";

        var compression = data[offset];
        switch (compression)
        {
            case 8:
                return Encoding.Latin1.GetString(data, offset + 1, length - 1).TrimEnd('\0');
            case 16:
            {
                var name = Encoding.BigEndianUnicode.GetString(data, offset + 1, (length - 1) & ~1);
                var nul = name.IndexOf('\0');
                return nul >= 0 ? name[..nul] : name;
            }
            default:
                return "";
        }
    }

    private static DateTime? ParseUdfTimestamp(byte[] d, int off)
    {
        if (off + 12 > d.Length) return null;

        var allZero = true;
        for (var i = 0; i < 12; i++)
        {
            if (d[off + i] != 0)
            {
                allZero = false;
                break;
            }
        }

        if (allZero) return null;

        var typeAndTz = LeU16(d, off);
        var tz = typeAndTz & 0x0FFF;
        if ((tz & 0x800) != 0)
        {
            tz -= 0x1000;
        }

        if (tz is < -14 * 60 or > 14 * 60)
        {
            tz = 0;
        }

        int year = LeU16(d, off + 2);
        int month = d[off + 4], day = d[off + 5], hour = d[off + 6], minute = d[off + 7], second = d[off + 8];
        int centiseconds = d[off + 9];
        int hundredMicros = d[off + 10];

        if (month is < 1 or > 12 || day is < 1 or > 31) return null;
        if (hour > 23 || minute > 59 || second > 59) return null;

        try
        {
            var dt = new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.FromMinutes(tz));
            return dt.UtcDateTime.AddMilliseconds(10.0 * centiseconds + hundredMicros / 10.0);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static bool ValidTag(byte[] d, int o)
    {
        if (o + 16 > d.Length) return false;

        byte sum = 0;
        for (var i = 0; i < 16; i++)
        {
            if (i != 4)
            {
                sum = (byte)(sum + d[o + i]);
            }
        }

        return sum == d[o + 4];
    }

    private static ushort LeU16(byte[] d, int o)
    {
        return (ushort)(d[o] | (d[o + 1] << 8));
    }

    private static uint LeU32(byte[] d, int o)
    {
        return (uint)(d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24));
    }

    private static ulong LeU64(byte[] d, int o)
    {
        return d[o] | ((ulong)d[o + 1] << 8) | ((ulong)d[o + 2] << 16) | ((ulong)d[o + 3] << 24) |
               ((ulong)d[o + 4] << 32) | ((ulong)d[o + 5] << 40) | ((ulong)d[o + 6] << 48) | ((ulong)d[o + 7] << 56);
    }
}
