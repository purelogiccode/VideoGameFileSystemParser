using System.Text;

namespace VideoGameFileSystemParser.Parsers;

internal class HfsParser
{
    private readonly SectorReader _reader;
    private uint _hfsStartLba;
    private uint _hfsPartitionByteOffset;
    private uint _allocationBlockSize;
    private uint _allocationBlockStart;
    private readonly Dictionary<uint, HfsFolderRecord> _folders = [];
    private readonly List<HfsCatalogEntry> _entries = [];

    internal HfsParser(SectorReader reader)
    {
        _reader = reader;
    }

    internal bool Parse(FsNode rootNode, TrackInfo? track = null)
    {
        _reader.Reset();

        if (track is { Frames: > 0 })
            _reader.SetTrack(track, true);
        else
            _reader.SetTrack(null);

        _folders.Clear();
        _entries.Clear();

        if (!FindHfsPartitionAndMdb(track, out var catalogStartBlock, out var catalogBlockCount))
            return false;

        if (!ParseCatalogFile(catalogStartBlock, catalogBlockCount))
            return false;

        BuildTree(rootNode);
        return true;
    }

    private bool FindHfsPartitionAndMdb(TrackInfo? track, out uint catalogStartBlock, out uint catalogBlockCount)
    {
        catalogStartBlock = 0;
        catalogBlockCount = 0;

        var trackStart = track?.StartLba ?? 0;

        var sectors = ReadSectors(trackStart, 4);
        if (sectors == null && trackStart != 0)
        {
            _reader.SetTrack(null);
            sectors = ReadSectors(0, 4);
            trackStart = 0;
        }

        if (sectors == null)
            return false;

        // Try standard paths first, then scan for signatures at various offsets
        int[] headerOffsets = [0, 2, 4, 6, 8, 10, 12, 14, 16, 20, 24, 28, 32];

        // Apple Partition Map path (signature "ER")
        foreach (var hdrOff in headerOffsets)
        {
            if (hdrOff + 2 > sectors.Length) continue;
            if (sectors[hdrOff] != 0x45 || sectors[hdrOff + 1] != 0x52) continue;

            for (var entry = 0; entry < 64; entry++)
            {
                var byteOffset = hdrOff + 512 * entry;
                if (byteOffset + 512 > sectors.Length)
                    break;

                if (sectors[byteOffset] != 0x50 || sectors[byteOffset + 1] != 0x4d)
                    continue;

                var partitionType = Encoding.ASCII.GetString(sectors, byteOffset + 48, 32)
                    .TrimEnd('\0', ' ');

                if (partitionType.Equals("Apple_HFS", StringComparison.Ordinal))
                {
                    var firstPhysicalBlock = BeU32(sectors, byteOffset + 8);
                    _hfsPartitionByteOffset = firstPhysicalBlock * 512;
                    _hfsStartLba = trackStart + _hfsPartitionByteOffset / 2048;

                    if (TryReadMdb(out catalogStartBlock, out catalogBlockCount))
                        return true;

                    if (TryReadHfsPlusHeader(out catalogStartBlock, out catalogBlockCount))
                        return true;
                }
                else if (partitionType.Equals("Apple_HFS+", StringComparison.Ordinal) ||
                         partitionType.Equals("Apple_HFSX", StringComparison.Ordinal))
                {
                    var firstPhysicalBlock = BeU32(sectors, byteOffset + 8);
                    _hfsPartitionByteOffset = firstPhysicalBlock * 512;
                    _hfsStartLba = trackStart + _hfsPartitionByteOffset / 2048;

                    if (TryReadHfsPlusHeader(out catalogStartBlock, out catalogBlockCount))
                        return true;
                }
            }
        }

        // Direct HFS path (signature "LK" bootblock) at various offsets
        foreach (var hdrOff in headerOffsets)
        {
            if (hdrOff + 2 > sectors.Length) continue;
            if (sectors[hdrOff] != 0x4C || sectors[hdrOff + 1] != 0x4B) continue;

            _hfsStartLba = trackStart;
            _hfsPartitionByteOffset = 0;

            if (TryReadMdb(out catalogStartBlock, out catalogBlockCount))
                return true;

            if (TryReadHfsPlusHeader(out catalogStartBlock, out catalogBlockCount))
                return true;
        }

        // Direct HFS+ path (no bootblock, volume header at sector 2)
        _hfsStartLba = trackStart;
        _hfsPartitionByteOffset = 0;
        if (TryReadHfsPlusHeader(out catalogStartBlock, out catalogBlockCount))
            return true;

        // Brute-force scan for non-standard disc layouts
        return BruteForceFindHfs(trackStart, out catalogStartBlock, out catalogBlockCount);
    }

    private bool BruteForceFindHfs(uint trackStart, out uint catalogStartBlock, out uint catalogBlockCount)
    {
        catalogStartBlock = 0;
        catalogBlockCount = 0;

        const uint scanLimit = 5000u;

        for (uint sector = 0; sector < scanLimit; sector++)
        {
            var lba = trackStart + sector;
            var sec = new byte[2048];
            if (!_reader.ReadSector(lba, sec))
                continue;

            int[] headerOffsets = [0, 2, 4, 6, 8, 10, 12, 14, 16, 20, 24, 28, 32];

            foreach (var hdrOff in headerOffsets)
            {
                if (hdrOff + 2 > sec.Length) continue;

                // Check for HFS boot block "LK"
                if (sec[hdrOff] == 0x4C && sec[hdrOff + 1] == 0x4B)
                {
                    _hfsStartLba = lba;
                    _hfsPartitionByteOffset = 0;
                    if (TryReadMdb(out catalogStartBlock, out catalogBlockCount))
                        return true;
                    if (TryReadHfsPlusHeader(out catalogStartBlock, out catalogBlockCount))
                        return true;
                }

                // Check for HFS MDB "BD" at byte offset 1024 from data start
                if (hdrOff + 1026 <= sec.Length && sec[hdrOff + 1024] == 0x42 && sec[hdrOff + 1025] == 0x44)
                {
                    _hfsStartLba = lba;
                    _hfsPartitionByteOffset = 0;
                    if (TryReadMdb(out catalogStartBlock, out catalogBlockCount))
                        return true;
                }

                // Check for HFS+ volume header "HX" or "H+" at byte offset 1024
                if (hdrOff + 1026 <= sec.Length)
                {
                    var isHx = sec[hdrOff + 1024] == 0x48 && sec[hdrOff + 1025] == 0x58;
                    var isHp = sec[hdrOff + 1024] == 0x48 && sec[hdrOff + 1025] == 0x2B;
                    if (isHx || isHp)
                    {
                        _hfsStartLba = lba;
                        _hfsPartitionByteOffset = 0;
                        if (TryReadHfsPlusHeader(out catalogStartBlock, out catalogBlockCount))
                            return true;
                    }
                }

                // Check for Apple Partition Map "ER"
                if (sec[hdrOff] == 0x45 && sec[hdrOff + 1] == 0x52)
                {
                    for (var entry = 0; entry < 64; entry++)
                    {
                        var byteOffset = hdrOff + 512 * entry;
                        if (byteOffset + 512 > sec.Length)
                            break;

                        if (sec[byteOffset] != 0x50 || sec[byteOffset + 1] != 0x4d)
                            continue;

                        var partitionType = Encoding.ASCII.GetString(sec, byteOffset + 48, 32)
                            .TrimEnd('\0', ' ');

                        if (!partitionType.Equals("Apple_HFS", StringComparison.Ordinal) &&
                            !partitionType.Equals("Apple_HFS+", StringComparison.Ordinal) &&
                            !partitionType.Equals("Apple_HFSX", StringComparison.Ordinal))
                            continue;

                        var firstPhysicalBlock = BeU32(sec, byteOffset + 8);
                        _hfsPartitionByteOffset = firstPhysicalBlock * 512;
                        _hfsStartLba = trackStart + _hfsPartitionByteOffset / 2048;

                        if (TryReadMdb(out catalogStartBlock, out catalogBlockCount))
                            return true;
                        if (TryReadHfsPlusHeader(out catalogStartBlock, out catalogBlockCount))
                            return true;
                    }
                }
            }
        }

        return false;
    }

    private bool TryReadMdb(out uint catalogStartBlock, out uint catalogBlockCount)
    {
        catalogStartBlock = 0;
        catalogBlockCount = 0;

        // Check standard MDB byte offsets and header-offset-adjusted positions
        int[] candidateOffsets =
        [
            0, 512, 1024, 1536,
            2, 514, 1026, 1538,
            4, 516, 1028, 1540,
            6, 518, 1030, 1542,
            8, 520, 1032, 1544,
            10, 522, 1034, 1546,
            12, 524, 1036, 1548,
            14, 526, 1038, 1550,
            16, 528, 1040, 1552,
            20, 532, 1044, 1556,
            24, 536, 1048, 1560,
            28, 540, 1052, 1564,
            32, 544, 1056, 1568
        ];

        foreach (var candidateOffset in candidateOffsets)
        {
            for (var sectorOffset = 0; sectorOffset <= 2; sectorOffset++)
            {
                var mdbLba = _hfsStartLba + (uint)sectorOffset;
                var sector = new byte[2048];

                if (!_reader.ReadSector(mdbLba, sector))
                    continue;

                if (candidateOffset + 162 > sector.Length)
                    continue;

                if (sector[candidateOffset] != 0x42 || sector[candidateOffset + 1] != 0x44)
                    continue;

                var allocBlockSize = BeU32(sector, candidateOffset + 20);

                if (allocBlockSize == 0)
                    continue;

                _allocationBlockSize = allocBlockSize;
                _allocationBlockStart = BeU16(sector, candidateOffset + 28);

                catalogStartBlock = BeU16(sector, candidateOffset + 150);
                catalogBlockCount = BeU16(sector, candidateOffset + 152);

                return catalogBlockCount > 0;
            }
        }

        return false;
    }

    private bool TryReadHfsPlusHeader(out uint catalogStartBlock, out uint catalogBlockCount)
    {
        catalogStartBlock = 0;
        catalogBlockCount = 0;

        var sector2Lba = _hfsStartLba + 2;
        var sector = new byte[2048];
        if (!_reader.ReadSector(sector2Lba, sector))
            return false;

        if (sector.Length < 160)
            return false;

        // Check for HFS+ signature at various header offsets
        int[] headerOffsets = [0, 2, 4, 6, 8, 10, 12, 14, 16, 20, 24, 28, 32];

        foreach (var hdrOff in headerOffsets)
        {
            if (hdrOff + 160 > sector.Length) continue;

            var sig0 = sector[hdrOff];
            var sig1 = sector[hdrOff + 1];

            var isHfsPlus = (sig0 == 0x48 && sig1 == 0x58) || // "HX"
                            (sig0 == 0x48 && sig1 == 0x2B); // "H+"

            if (!isHfsPlus)
                continue;

            var allocBlockSize = BeU32(sector, hdrOff + 8);
            if (allocBlockSize == 0 || allocBlockSize % 512 != 0)
                continue;

            _allocationBlockSize = allocBlockSize;
            _allocationBlockStart = 0;

            catalogStartBlock = BeU32(sector, hdrOff + 128);
            catalogBlockCount = BeU32(sector, hdrOff + 132);

            if (catalogBlockCount == 0)
                continue;

            var catalogExtents = new List<(uint startBlock, uint blockCount)>();
            for (var i = 0; i < 3; i++)
            {
                var extOff = hdrOff + 128 + i * 8;
                var start = BeU32(sector, extOff);
                var count = BeU32(sector, extOff + 4);
                if (start > 0 && count > 0)
                    catalogExtents.Add((start, count));
            }

            if (catalogExtents.Count > 0 && ParseHfsPlusCatalog(catalogExtents))
                return true;
        }

        return false;
    }

    private bool ParseHfsPlusCatalog(List<(uint startBlock, uint blockCount)> extents)
    {
        var regionData = new List<byte>();
        foreach ((uint extStartBlock, uint extBlockCount) in extents)
        {
            if (extStartBlock == 0 || extBlockCount == 0)
                continue;

            var bytePos = _hfsPartitionByteOffset + (ulong)extStartBlock * _allocationBlockSize;
            var totalBytes = (ulong)extBlockCount * _allocationBlockSize;

            ulong totalRead = 0;
            while (totalRead < totalBytes)
            {
                var curByte = bytePos + totalRead;
                var curLba = (uint)(curByte / 2048);
                var curOff = (int)(curByte % 2048);

                var sector = new byte[2048];
                if (!_reader.ReadSector(curLba, sector))
                    return false;

                var copyLen = Math.Min(2048 - curOff, (int)(totalBytes - totalRead));
                var segment = new byte[copyLen];
                Array.Copy(sector, curOff, segment, 0, copyLen);
                regionData.AddRange(segment);
                totalRead += (ulong)copyLen;
            }
        }

        if (regionData.Count == 0)
            return false;

        var nodeData = regionData.ToArray();

        ushort headerRecOff;
        ushort nodeSize;

        var nodeDesc = ReadBtNodeDescriptor(nodeData, 0);
        if (nodeDesc.Kind != KBtHeaderNode)
        {
            if (!ScanForBtreeHeaderRecord(nodeData, out headerRecOff, out nodeSize))
                return false;
        }
        else
        {
            headerRecOff = BeU16(nodeData, 14);
            if (headerRecOff <= 0 || headerRecOff + 30 > nodeData.Length)
            {
                if (!ScanForBtreeHeaderRecord(nodeData, out headerRecOff, out nodeSize))
                    return false;
            }
            else
            {
                nodeSize = BeU16(nodeData, headerRecOff + 18);
                if (nodeSize == 0 || nodeSize > nodeData.Length)
                    return false;
            }
        }

        var currentLeaf = BeU32(nodeData, headerRecOff + 10);

        var visited = new HashSet<uint>();
        for (var safety = 0; safety < 100000 && currentLeaf != 0; safety++)
        {
            if (!visited.Add(currentLeaf))
                break;

            var leafOffset = (int)((ulong)currentLeaf * nodeSize);
            if (leafOffset + nodeSize > nodeData.Length)
                break;

            var leafDesc = ReadBtNodeDescriptor(nodeData, leafOffset);
            if (leafDesc.Kind == KBtLeafNode)
            {
                ProcessHfsPlusLeafNode(nodeData, leafOffset, leafDesc.NumRecords, nodeSize);
            }

            currentLeaf = leafDesc.FLink;
        }

        return _entries.Count > 0;
    }

    private void ProcessHfsPlusLeafNode(byte[] nodeData, int nodeOffset, ushort numRecords, ushort nodeSize)
    {
        if (numRecords == 0)
            return;

        var recordOffsets = new ushort[numRecords];
        for (var i = 0; i < numRecords; i++)
        {
            var tableOffset = nodeSize - 2 * (i + 1);
            recordOffsets[i] = BeU16(nodeData, nodeOffset + tableOffset);
        }

        foreach (var off in recordOffsets.OrderBy(static o => o))
        {
            if (off + 7 > nodeSize)
                continue;

            var keyLen = BeU16(nodeData, nodeOffset + off);
            if (keyLen < 6 || off + keyLen > nodeSize)
                continue;

            var parentId = BeU32(nodeData, nodeOffset + off + 2);
            var nameLength = nodeData[nodeOffset + off + 6];

            if (off + 10 + nameLength > nodeSize)
                continue;

            var name = Encoding.BigEndianUnicode.GetString(nodeData, nodeOffset + off + 10, nameLength * 2);

            var keySize = 6 + nameLength;
            var alignedKeySize = (keySize + 1) & ~1;
            var dataOff = nodeOffset + off + alignedKeySize;

            if (dataOff + 2 > nodeOffset + nodeSize)
                continue;

            var recordType = BeU16(nodeData, dataOff);

            switch (recordType)
            {
                case KHfsFolderRecord:
                {
                    if (dataOff + 88 > nodeOffset + nodeSize)
                        continue;

                    var folder = ReadHfsPlusFolderRecord(nodeData, dataOff);
                    folder.ParentId = parentId;
                    folder.Name = name;
                    _entries.Add(new HfsCatalogEntry
                    {
                        Name = name,
                        ParentId = parentId,
                        RecordType = HfsRecordType.Folder,
                        Folder = folder
                    });
                    _folders[folder.FolderId] = folder;
                    break;
                }
                case KHfsFileRecord:
                {
                    if (dataOff + 243 > nodeOffset + nodeSize)
                        continue;

                    var file = ReadHfsPlusFileRecord(nodeData, dataOff);
                    _entries.Add(new HfsCatalogEntry
                    {
                        Name = name,
                        ParentId = parentId,
                        RecordType = HfsRecordType.File,
                        File = file
                    });
                    break;
                }
                case KHfsFolderThreadRecord:
                case KHfsFileThreadRecord:
                {
                    if (dataOff + 10 > nodeOffset + nodeSize)
                        continue;

                    var threadParentId = BeU32(nodeData, dataOff + 8);
                    _entries.Add(new HfsCatalogEntry
                    {
                        Name = name,
                        ParentId = threadParentId,
                        RecordType = recordType == KHfsFolderThreadRecord
                            ? HfsRecordType.FolderThread
                            : HfsRecordType.FileThread
                    });
                    break;
                }
            }
        }
    }

    private static HfsFolderRecord ReadHfsPlusFolderRecord(byte[] data, int offset)
    {
        return new HfsFolderRecord
        {
            FolderId = BeU32(data, offset + 8),
            CreateDate = BeU32(data, offset + 16),
            ModifyDate = BeU32(data, offset + 20)
        };
    }

    private static HfsFileRecord ReadHfsPlusFileRecord(byte[] data, int offset)
    {
        var rec = new HfsFileRecord
        {
            CreateDate = BeU32(data, offset + 16),
            ModifyDate = BeU32(data, offset + 20),
            DataLogicalSize = (int)BeU64(data, offset + 56),
            ResourceLogicalSize = (int)BeU64(data, offset + 88)
        };

        for (var i = 0; i < 3; i++)
        {
            var extOff = offset + 112 + i * 8;
            rec.DataExtents[i] = (BeU32(data, extOff), BeU32(data, extOff + 4));
        }

        for (var i = 0; i < 3; i++)
        {
            var extOff = offset + 152 + i * 8;
            rec.RsrcExtents[i] = (BeU32(data, extOff), BeU32(data, extOff + 4));
        }

        return rec;
    }

    private static ulong BeU64(byte[] data, int offset)
    {
        return ((ulong)BeU32(data, offset) << 32) | BeU32(data, offset + 4);
    }

    private uint BlockToAbsoluteLba(uint allocationBlock)
    {
        var byteOffset = _hfsPartitionByteOffset + (ulong)_allocationBlockStart * 512 + (ulong)allocationBlock * _allocationBlockSize;
        return _hfsStartLba + (uint)(byteOffset / 2048);
    }

    private bool ParseCatalogFile(uint startBlock, uint blockCount)
    {
        var catalogExtents = new List<(uint startBlock, uint blockCount)> { (startBlock, blockCount) };

        var regionData = new List<byte>();
        foreach ((uint extStartBlock, uint extBlockCount) in catalogExtents)
        {
            if (extStartBlock == 0 || extBlockCount == 0)
                continue;

            var bytePos = _hfsPartitionByteOffset + (ulong)_allocationBlockStart * 512 + (ulong)extStartBlock * _allocationBlockSize;
            var totalBytes = (ulong)extBlockCount * _allocationBlockSize;

            ulong totalRead = 0;
            while (totalRead < totalBytes)
            {
                var curByte = bytePos + totalRead;
                var curLba = (uint)(curByte / 2048);
                var curOff = (int)(curByte % 2048);

                var sector = new byte[2048];
                if (!_reader.ReadSector(curLba, sector))
                    return false;

                var copyLen = Math.Min(2048 - curOff, (int)(totalBytes - totalRead));
                for (var j = 0; j < copyLen; j++)
                    regionData.Add(sector[curOff + j]);
                totalRead += (ulong)copyLen;
            }
        }

        if (regionData.Count == 0)
            return false;

        var nodeData = regionData.ToArray();

        ushort headerRecOff;
        ushort nodeSize;

        var nodeDesc = ReadBtNodeDescriptor(nodeData, 0);
        if (nodeDesc.Kind != KBtHeaderNode)
        {
            if (!ScanForBtreeHeaderRecord(nodeData, out headerRecOff, out nodeSize))
                return false;
        }
        else
        {
            var nodeSizeBtree = _allocationBlockSize;
            headerRecOff = BeU16(nodeData, (int)nodeSizeBtree - 2);

            if (headerRecOff <= 0 || headerRecOff + 30 > nodeData.Length)
            {
                headerRecOff = BeU16(nodeData, (int)nodeSizeBtree - 2);
                if (headerRecOff + 30 > nodeData.Length)
                {
                    if (!ScanForBtreeHeaderRecord(nodeData, out headerRecOff, out nodeSize))
                        return false;

                    goto parseLeaves;
                }
            }

            if (headerRecOff == 0 && nodeSizeBtree < 2048)
            {
                headerRecOff = BeU16(nodeData, 512 - 2);
                if (headerRecOff == 0 || headerRecOff + 30 > nodeData.Length)
                {
                    headerRecOff = BeU16(nodeData, 1024 - 2);
                }
            }

            if (headerRecOff <= 0 || headerRecOff + 30 > nodeData.Length)
            {
                if (!ScanForBtreeHeaderRecord(nodeData, out headerRecOff, out nodeSize))
                    return false;

                goto parseLeaves;
            }

            var headerRec = ReadBtHeaderRec(nodeData, headerRecOff);
            nodeSize = headerRec.NodeSize;
        }

        parseLeaves:
        if (nodeSize == 0 || nodeSize > nodeData.Length)
            return false;

        var currentLeaf = BeU32(nodeData, headerRecOff + 10);

        var visited = new HashSet<uint>();
        for (var safety = 0; safety < 100000 && currentLeaf != 0; safety++)
        {
            if (!visited.Add(currentLeaf))
                break;

            var leafOffset = (int)((ulong)currentLeaf * nodeSize);
            if (leafOffset + nodeSize > nodeData.Length)
                break;

            var leafDesc = ReadBtNodeDescriptor(nodeData, leafOffset);

            if (leafDesc.Kind == KBtLeafNode)
            {
                ProcessLeafNode(nodeData, leafOffset, leafDesc.NumRecords, nodeSize);
            }

            currentLeaf = leafDesc.FLink;
        }

        return true;
    }

    private void ProcessLeafNode(byte[] nodeData, int nodeOffset, ushort numRecords, ushort nodeSize)
    {
        if (numRecords == 0)
            return;

        var recordOffsets = new ushort[numRecords];
        for (var i = 0; i < numRecords; i++)
        {
            var tableOffset = nodeSize - 2 * (i + 1);
            recordOffsets[i] = BeU16(nodeData, nodeOffset + tableOffset);
        }

        foreach (var off in recordOffsets.OrderBy(static o => o))
        {
            if (off + 7 > nodeSize)
                continue;

            var keyLength = nodeData[nodeOffset + off];
            if (keyLength < 7)
                continue;

            var parentId = BeU32(nodeData, nodeOffset + off + 2);
            var nameLength = nodeData[nodeOffset + off + 6];

            if (nameLength > 31 || off + 7 + nameLength + 2 > nodeSize)
                continue;

            var name = nameLength > 0
                ? Encoding.ASCII.GetString(nodeData, nodeOffset + off + 7, nameLength)
                : "";

            var dataOff = nodeOffset + off + 7 + nameLength;
            if ((nameLength & 1) == 0)
            {
                dataOff++;
            }

            if (dataOff + 2 > nodeOffset + nodeSize)
                continue;

            var recordType = BeU16(nodeData, dataOff);

            switch (recordType)
            {
                case KHfsFolderRecord:
                {
                    if (dataOff + 70 > nodeOffset + nodeSize)
                        continue;

                    var folder = ReadFolderRecord(nodeData, dataOff);
                    folder.ParentId = parentId;
                    folder.Name = name;
                    _entries.Add(new HfsCatalogEntry
                    {
                        Name = name,
                        ParentId = parentId,
                        RecordType = HfsRecordType.Folder,
                        Folder = folder
                    });
                    _folders[folder.FolderId] = folder;
                    break;
                }
                case KHfsFileRecord:
                {
                    if (dataOff + 102 > nodeOffset + nodeSize)
                        continue;

                    var file = ReadFileRecord(nodeData, dataOff);
                    _entries.Add(new HfsCatalogEntry
                    {
                        Name = name,
                        ParentId = parentId,
                        RecordType = HfsRecordType.File,
                        File = file
                    });
                    break;
                }
                case KHfsFolderThreadRecord:
                case KHfsFileThreadRecord:
                {
                    var threadParentId = BeU32(nodeData, dataOff + 8);
                    _entries.Add(new HfsCatalogEntry
                    {
                        Name = name,
                        ParentId = threadParentId,
                        RecordType = recordType == KHfsFolderThreadRecord
                            ? HfsRecordType.FolderThread
                            : HfsRecordType.FileThread
                    });
                    break;
                }
            }
        }
    }

    private static HfsFolderRecord ReadFolderRecord(byte[] data, int offset)
    {
        return new HfsFolderRecord
        {
            FolderId = BeU32(data, offset + 6),
            CreateDate = BeU32(data, offset + 10),
            ModifyDate = BeU32(data, offset + 14)
        };
    }

    private static HfsFileRecord ReadFileRecord(byte[] data, int offset)
    {
        var rec = new HfsFileRecord
        {
            DataLogicalSize = BeS32(data, offset + 26),
            ResourceLogicalSize = BeS32(data, offset + 36),
            CreateDate = BeU32(data, offset + 44),
            ModifyDate = BeU32(data, offset + 48)
        };

        for (var i = 0; i < 3; i++)
        {
            var extOff = offset + 70 + i * 4;
            rec.DataExtents[i] = (BeU16(data, extOff), BeU16(data, extOff + 2));
        }

        for (var i = 0; i < 3; i++)
        {
            var extOff = offset + 82 + i * 4;
            rec.RsrcExtents[i] = (BeU16(data, extOff), BeU16(data, extOff + 2));
        }

        return rec;
    }

    private void BuildTree(FsNode rootNode)
    {
        rootNode.Name = "/";
        rootNode.IsDirectory = true;
        rootNode.Lba = _hfsStartLba;
        rootNode.NodeType = FsNodeType.Directory;

        var seen = new HashSet<string>();
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            var e = _entries[i];
            var key = $"{e.RecordType}:{e.ParentId}:{e.Name}";
            if (!seen.Add(key))
                _entries.RemoveAt(i);
        }

        BuildDirectory(rootNode, KHfsRootFolderId);
    }

    private void BuildDirectory(FsNode dirNode, uint folderId)
    {
        var children = _entries
            .Where(e => e.ParentId == folderId)
            .OrderBy(static e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var entry in children)
        {
            switch (entry.RecordType)
            {
                case HfsRecordType.Folder when entry.Folder != null:
                {
                    var child = new FsNode
                    {
                        Name = entry.Name,
                        IsDirectory = true,
                        Lba = _hfsStartLba,
                        NodeType = FsNodeType.Directory,
                        ModifiedTime = MacTimeToDateTime(entry.Folder.ModifyDate),
                        CreatedTime = MacTimeToDateTime(entry.Folder.CreateDate)
                    };
                    child.Extents.Add(new FsExtent { Lba = child.Lba, Size = 0 });
                    BuildDirectory(child, entry.Folder.FolderId);
                    dirNode.Children.Add(child);
                    break;
                }
                case HfsRecordType.File when entry.File != null:
                {
                    var file = entry.File;
                    var dataSize = file.DataLogicalSize;
                    var rsrcSize = file.ResourceLogicalSize;

                    if (dataSize > 0)
                    {
                        var child = CreateFileNode(entry.Name, file.DataExtents, (ulong)dataSize,
                            file.ModifyDate, file.CreateDate);
                        dirNode.Children.Add(child);
                    }
                    else if (rsrcSize > 0)
                    {
                        var child = CreateFileNode(entry.Name, file.RsrcExtents, (ulong)rsrcSize,
                            file.ModifyDate, file.CreateDate);
                        dirNode.Children.Add(child);
                    }
                    else
                    {
                        var child = new FsNode
                        {
                            Name = entry.Name,
                            IsDirectory = false,
                            Size = 0,
                            Lba = _hfsStartLba,
                            NodeType = FsNodeType.File,
                            ModifiedTime = MacTimeToDateTime(file.ModifyDate),
                            CreatedTime = MacTimeToDateTime(file.CreateDate)
                        };
                        dirNode.Children.Add(child);
                    }

                    break;
                }
            }
        }
    }

    private FsNode CreateFileNode(string name,
        (uint startBlock, uint blockCount)[] extents,
        ulong logicalSize, uint modifyDate, uint createDate)
    {
        var child = new FsNode
        {
            Name = name,
            IsDirectory = false,
            Size = logicalSize,
            NodeType = FsNodeType.File,
            ModifiedTime = MacTimeToDateTime(modifyDate),
            CreatedTime = MacTimeToDateTime(createDate)
        };

        var remaining = (long)logicalSize;
        foreach ((uint startBlock, uint blockCount) in extents)
        {
            if (startBlock == 0 || blockCount == 0)
                continue;

            var lba = BlockToAbsoluteLba(startBlock);
            var extentByteSize = (ulong)blockCount * _allocationBlockSize;
            var extentSize = Math.Min((ulong)remaining, extentByteSize);

            child.Extents.Add(new FsExtent { Lba = lba, Size = extentSize });
            remaining -= (long)extentSize;

            if (remaining <= 0)
                break;
        }

        if (child.Extents.Count > 0)
        {
            child.Lba = child.Extents[0].Lba;
        }
        else
        {
            child.Lba = _hfsStartLba;
            child.Size = 0;
        }

        return child;
    }

    private static BtNodeDescriptor ReadBtNodeDescriptor(byte[] data, int offset)
    {
        return new BtNodeDescriptor
        {
            FLink = BeU32(data, offset),
            Kind = (sbyte)data[offset + 8],
            NumRecords = BeU16(data, offset + 10)
        };
    }

    private static BtHeaderRec ReadBtHeaderRec(byte[] data, int offset)
    {
        return new BtHeaderRec
        {
            NodeSize = BeU16(data, offset + 18)
        };
    }

    private static bool ScanForBtreeHeaderRecord(byte[] nodeData, out ushort headerRecOff, out ushort nodeSize)
    {
        headerRecOff = 0;
        nodeSize = 0;

        for (var scan = 0; scan < nodeData.Length - 32; scan += 2)
        {
            var nsz = BeU16(nodeData, scan + 18);
            if (nsz is not (512 or 1024 or 2048 or 4096 or 8192))
                continue;

            var ttl = BeU32(nodeData, scan + 22);
            if (ttl is <= 0 or >= 100000)
                continue;

            var treeDepth = BeU16(nodeData, scan);
            if (treeDepth is 0 or > 16)
                continue;

            var firstLeaf = BeU32(nodeData, scan + 10);
            if (firstLeaf > ttl)
                continue;

            headerRecOff = (ushort)scan;
            nodeSize = nsz;
            return true;
        }

        return false;
    }

    private byte[]? ReadSectors(uint lba, int count)
    {
        if (count > 1024)
            return null;

        var result = new byte[count * 2048];
        for (var i = 0; i < count; i++)
        {
            if (!_reader.ReadSector(lba + (uint)i, result, i * 2048))
                return null;
        }

        return result;
    }

    private static DateTime? MacTimeToDateTime(uint macTime)
    {
        if (macTime == 0)
            return null;

        const long macEpochOffset = 2082844800;
        var unixTime = macTime - macEpochOffset;

        if (unixTime < 0)
            return null;

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
        }
        catch
        {
            return null;
        }
    }

    private static uint BeU32(byte[] data, int offset)
    {
        return (uint)((data[offset] << 24) | (data[offset + 1] << 16)
                                           | (data[offset + 2] << 8) | data[offset + 3]);
    }

    private static ushort BeU16(byte[] data, int offset)
    {
        return (ushort)((data[offset] << 8) | data[offset + 1]);
    }

    private static int BeS32(byte[] data, int offset)
    {
        return (data[offset] << 24) | (data[offset + 1] << 16)
                                    | (data[offset + 2] << 8) | data[offset + 3];
    }

    private const sbyte KBtLeafNode = -1;
    private const sbyte KBtHeaderNode = 1;

    private const ushort KHfsFolderRecord = 0x100;
    private const ushort KHfsFileRecord = 0x200;
    private const ushort KHfsFolderThreadRecord = 0x300;
    private const ushort KHfsFileThreadRecord = 0x400;

    private const uint KHfsRootFolderId = 2;

    private struct BtNodeDescriptor
    {
        public uint FLink;
        public sbyte Kind;
        public ushort NumRecords;
    }

    private struct BtHeaderRec
    {
        public ushort NodeSize;
    }

    private enum HfsRecordType
    {
        Folder,
        File,
        FolderThread,
        FileThread
    }

    private sealed class HfsCatalogEntry
    {
        public string Name { get; set; } = "";
        public uint ParentId { get; set; }
        public HfsRecordType RecordType { get; set; }
        public HfsFolderRecord? Folder { get; set; }
        public HfsFileRecord? File { get; set; }
    }

    private sealed class HfsFolderRecord
    {
        public uint FolderId;
        public uint ParentId;
        public string Name { get; set; } = "";
        public uint CreateDate;
        public uint ModifyDate;
    }

    private sealed class HfsFileRecord
    {
        public int DataLogicalSize;
        public int ResourceLogicalSize;
        public uint CreateDate;
        public uint ModifyDate;
        public readonly (uint startBlock, uint blockCount)[] DataExtents = new (uint, uint)[3];
        public readonly (uint startBlock, uint blockCount)[] RsrcExtents = new (uint, uint)[3];
    }
}
