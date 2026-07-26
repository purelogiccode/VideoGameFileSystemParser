using System.Globalization;
using System.Text;

namespace VideoGameFileSystemParser.Parsers;

/// <summary>
/// Parses ISO 9660 (High Sierra, Joliet, CD-XA) file systems. Supports SUSP/Rock Ridge for POSIX attributes and symlinks.
/// </summary>
public class Iso9660Parser
{
    private const int MaxCeChain = 64;

    private readonly SectorReader _reader;
    private readonly HashSet<uint> _visitedDirs = [];
    private readonly bool _scanWithinSector;
    private bool _isHighSierra;
    private bool _isJoliet;
    private bool _isXa;
    private bool _suspActive;
    private byte _suspSkip;

    /// <summary>
    /// Initializes a new instance of the Iso9660Parser class.
    /// </summary>
    /// <param name="reader">The SectorReader to read sectors from.</param>
    /// <param name="scanWithinSector">If true, scans entire sectors byte-offset for VD signatures
    /// (handles raw-sector images where CD sync headers weren't fully stripped,
    /// e.g. some PC-FX and other CD-ROM dumps).</param>
    public Iso9660Parser(SectorReader reader, bool scanWithinSector = false)
    {
        _reader = reader;
        _scanWithinSector = scanWithinSector;
    }

    /// <summary>
    /// Parses the ISO 9660 file system, locating the PVD and building the directory tree.
    /// </summary>
    /// <param name="track">Optional track to restrict parsing to.</param>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <returns>true if parsing succeeded.</returns>
    public bool Parse(FsNode rootNode, TrackInfo? track = null)
    {
        _reader.Reset();

        if (track is { Frames: > 0 })
            _reader.SetTrack(track, true);
        else
            _reader.SetTrack(null);

        _isHighSierra = false;
        _isJoliet = false;
        _isXa = false;
        _suspActive = false;
        _suspSkip = 0;
        _visitedDirs.Clear();

        var effectiveTrackStart = (track?.StartLba ?? 0) + (track?.Pregap ?? 0);

        var vdOffsets = new List<uint> { 16, 17 };
        if (effectiveTrackStart != (track?.StartLba ?? 0))
        {
            vdOffsets.Add(166);
            vdOffsets.Add(167);
        }

        var foundPvd = false;
        byte[]? bestVdData = null;
        var sectorData = new byte[2048];

        foreach (var offset in vdOffsets)
        {
            if (ScanSectorForVd(effectiveTrackStart + offset, sectorData, ref foundPvd, ref _isHighSierra, ref _isJoliet, ref bestVdData!))
                break;
        }

        if (!foundPvd && effectiveTrackStart != 0)
        {
            _reader.SetTrack(null);
            foreach (var offset in vdOffsets)
            {
                if (ScanSectorForVd(offset, sectorData, ref foundPvd, ref _isHighSierra, ref _isJoliet, ref bestVdData!))
                {
                    effectiveTrackStart = 0;
                    _reader.SetTrack(null);
                    break;
                }
            }
        }

        if (!foundPvd)
        {
            var scanLimit = track is { Frames: > 0 } ? Math.Min(track.Frames, 5000u) : 5000u;
            for (uint i = 0; i < scanLimit; i++)
            {
                if (_reader.ReadSector(effectiveTrackStart + i, sectorData) && sectorData.Length >= 16)
                {
                    if (TryFindVdInSector(sectorData, out var isHs, out var isJoliet))
                    {
                        _isHighSierra = isHs;
                        _isJoliet = isJoliet;
                        foundPvd = true;
                        bestVdData = (byte[])sectorData.Clone();
                        break;
                    }
                }
            }
        }

        if (!foundPvd)
            return false;

        // CD-XA marker in the PVD application-use area (PVD offset 883 + 141 = 1024)
        _isXa = !_isHighSierra && !_isJoliet && CheckMagic(bestVdData!, 1024, "CD-XA001");

        var rootOff = _isHighSierra ? 180 : 156;
        var rootRelLba = LeU32(bestVdData!, rootOff + 2);
        var rootSize = LeU32(bestVdData!, rootOff + 10);

        var baseLba = ResolveDirectoryBase(effectiveTrackStart, rootRelLba, track);

        rootNode.Name = "/";
        rootNode.IsDirectory = true;
        rootNode.Lba = unchecked(baseLba + rootRelLba);
        rootNode.Size = rootSize;
        rootNode.Extents.Add(new FsExtent { Lba = rootNode.Lba, Size = rootSize });

        if (!_isJoliet && !_isHighSierra)
            DetectSusp(rootNode.Lba);

        return ParseDirectory(rootNode, baseLba);
    }

    private uint ResolveDirectoryBase(uint trackStart, uint rootRelLba, TrackInfo? track)
    {
        uint[] candidates = [trackStart, 150, 0];

        foreach (var candidate in candidates)
        {
            if (IsRootDirectorySector(candidate + rootRelLba, rootRelLba, true))
                return candidate;
        }

        // Multisession discs (CD-Extra style): extents are absolute LBAs including an
        // inter-session gap that is not stored in the CHD, so the bias is disc-specific.
        // Locate the root directory inside the track via its "." self-record.
        if (track != null)
        {
            var scanLimit = Math.Min(track.Frames, 512u);
            for (uint k = 0; k < scanLimit; k++)
            {
                var lba = trackStart + k;
                if (IsRootDirectorySector(lba, rootRelLba, true))
                    return unchecked(lba - rootRelLba);
            }
        }

        foreach (var candidate in candidates)
        {
            if (IsRootDirectorySector(candidate + rootRelLba, rootRelLba, false))
                return candidate;
        }

        return trackStart;
    }

    private bool ScanSectorForVd(uint lba, byte[] sectorData, ref bool found, ref bool isHs, ref bool isJol, ref byte[] best)
    {
        if (!_reader.ReadSector(lba, sectorData) || sectorData.Length < 16)
            return false;

        if (TryFindVdInSector(sectorData, out var hs, out var jol))
        {
            isHs = hs;
            isJol = jol;
            found = true;
            best = (byte[])sectorData.Clone();
            return true;
        }

        return false;
    }

    private bool TryFindVdInSector(byte[] data, out bool isHs, out bool isJoliet)
    {
        isHs = false;
        isJoliet = false;

        // standard positions
        if (CheckMagic(data, 1, "CD001"))
        {
            var type = data[0];
            switch (type)
            {
                case 2 when IsJolietSvd(data):
                    isJoliet = true;
                    return true;
                case 1:
                    return true;
            }
        }

        if (CheckMagic(data, 9, "CDROM"))
        {
            isHs = true;
            return true;
        }

        // byte-offset scanning (handles raw-sector images with un-stripped sync headers)
        if (!_scanWithinSector)
            return false;

        for (var i = 0; i < data.Length - 16; i++)
        {
            if (CheckMagicOffset(data, i, "CD001", 1))
            {
                var type = data[i];
                switch (type)
                {
                    case 2 when IsJolietSvdAt(data, i):
                        isJoliet = true;
                        return true;
                    case 1:
                        return true;
                }
            }

            if (CheckMagicOffset(data, i, "CDROM", 9))
            {
                isHs = true;
                return true;
            }
        }

        return false;
    }

    private static bool CheckMagicOffset(byte[] data, int baseOff, string magic, int magicOffset)
    {
        var off = baseOff + magicOffset;
        if (off + magic.Length > data.Length) return false;

        for (var i = 0; i < magic.Length; i++)
        {
            if (data[off + i] != magic[i]) return false;
        }

        return true;
    }

    private static bool IsJolietSvdAt(byte[] d, int baseOff)
    {
        return d[baseOff + 88] == 0x25 && d[baseOff + 89] == 0x2F && d[baseOff + 90] is 0x40 or 0x43 or 0x45;
    }

    private bool IsRootDirectorySector(uint lba, uint expectedExtent, bool strict)
    {
        var sec = new byte[2048];
        if (!_reader.ReadSector(lba, sec))
            return false;

        var recLen = sec[0];
        if (recLen < 34)
            return false;

        var nameLenOff = _isHighSierra ? 31 : 32;
        var nameOff = _isHighSierra ? 32 : 33;
        if (sec[nameLenOff] != 1 || sec[nameOff] != 0x00)
            return false;

        var flagsOff = _isHighSierra ? 24 : 25;
        if ((sec[flagsOff] & 0x02) == 0)
            return false;

        if (!strict)
            return true;

        var selfExtent = LeU32(sec, 2) + sec[1];
        return selfExtent == expectedExtent;
    }

    /// <summary>
    /// Parses a directory sector chain recursively.
    /// </summary>
    /// <param name="dirNode">The directory node to populate.</param>
    /// <param name="trackStart">The LBA of the containing track.</param>
    /// <returns>true if parsing succeeded.</returns>
    internal bool ParseDirectory(FsNode dirNode, uint trackStart)
    {
        if (!_visitedDirs.Add(dirNode.Lba))
            return true;

        var sectorsToRead = (uint)((dirNode.Size + 2047) / 2048);
        var sectorData = new byte[2048];
        string? illMultiExtentName = null;

        for (uint i = 0; i < sectorsToRead; i++)
        {
            var currentLba = dirNode.Lba + i;
            if (!_reader.ReadSector(currentLba, sectorData))
                break;

            uint pos = 0;
            while (pos < 2048)
            {
                var recordLen = sectorData[pos];
                if (recordLen == 0) break;

                if (pos + recordLen > 2048 || recordLen < 34)
                {
                    pos += recordLen;
                    if ((pos & 1) != 0)
                    {
                        pos++;
                    }

                    continue;
                }

                var xattrLen = sectorData[pos + 1];
                var relLba = LeU32(sectorData, (int)(pos + 2));
                ulong extentSize = LeU32(sectorData, (int)(pos + 10));

                var flagsOff = _isHighSierra ? 24 : 25;
                var flags = sectorData[pos + flagsOff];
                var isDir = (flags & 0x02) != 0;
                var isMulti = (flags & 0x80) != 0;

                var nameLenOff = _isHighSierra ? 31 : 32;
                var nameLen = sectorData[pos + nameLenOff];
                var nameOff = _isHighSierra ? 32 : 33;

                if (nameOff + nameLen > recordLen || pos + nameOff + nameLen > 2048)
                {
                    pos += recordLen;
                    if ((pos & 1) != 0)
                    {
                        pos++;
                    }

                    continue;
                }

                var name = DecodeName(sectorData, (int)pos + nameOff, nameLen);

                if (name != "." && name != "..")
                {
                    // Extended attribute records occupy xattr_len blocks before the data (ECMA-119 9.1.2)
                    var absoluteLba = trackStart + relLba + xattrLen;
                    var skipRecord = false;

                    var suStart = nameOff + nameLen + ((nameLen & 1) == 0 ? 1 : 0);
                    var suLen = recordLen - suStart;

                    byte xaFileNumber = 0;
                    var xaInterleaved = false;
                    if (_isXa && suLen >= 14)
                        TryParseXa(sectorData, (int)pos + suStart, suLen, nameLen, out xaFileNumber, out xaInterleaved);

                    SuspInfo? susp = null;
                    if (_suspActive && suLen > _suspSkip + 4)
                    {
                        susp = ParseSusp(sectorData, (int)pos + suStart, suLen, trackStart);
                        if (susp.Relocated)
                        {
                            skipRecord = true;
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(susp.Name))
                            {
                                name = susp.Name;
                            }

                            if (susp.ChildLinkLba != 0 &&
                                ReadSelfRecord(trackStart + susp.ChildLinkLba, out var clLba, out var clSize))
                            {
                                isDir = true;
                                isMulti = false;
                                absoluteLba = trackStart + clLba;
                                extentSize = clSize;
                            }
                        }
                    }

                    if (!skipRecord && illMultiExtentName != null)
                    {
                        if (name == illMultiExtentName && !isDir)
                        {
                            if (!isMulti)
                            {
                                illMultiExtentName = null;
                            }

                            skipRecord = true;
                        }
                        else
                        {
                            illMultiExtentName = null;
                        }
                    }

                    if (!skipRecord)
                    {
                        var last = dirNode.Children.Count > 0 ? dirNode.Children[^1] : null;
                        if (last is { IsMultiExtent: true, IsDirectory: false } && last.Name == name && !isDir)
                        {
                            if (last.Extents.Count > 0 && last.Extents[^1].Size % 2048 != 0)
                            {
                                last.IsMultiExtent = false;
                                if (isMulti)
                                {
                                    illMultiExtentName = name;
                                }
                            }
                            else
                            {
                                last.Size += extentSize;
                                last.Extents.Add(new FsExtent { Lba = absoluteLba, Size = extentSize });
                                last.IsMultiExtent = isMulti;
                            }
                        }
                        else
                        {
                            var child = new FsNode
                            {
                                Name = name,
                                Lba = absoluteLba,
                                Size = extentSize,
                                IsDirectory = isDir,
                                IsMultiExtent = isMulti,
                                FileNumber = xaFileNumber,
                                IsInterleaved = xaInterleaved && _reader.UnitBytes >= 2352,
                                ModifiedTime = ParseRecordTime(sectorData, (int)pos + 18)
                            };
                            if (susp is { SymlinkTarget: not null })
                            {
                                child.NodeType = FsNodeType.Symlink;
                                child.SymlinkTarget = susp.SymlinkTarget;
                            }
                            else if (isDir)
                            {
                                child.NodeType = FsNodeType.Directory;
                            }

                            if (susp != null)
                            {
                                if (susp.UnixMode.HasValue)
                                {
                                    child.UnixMode = susp.UnixMode;
                                }

                                if (susp.Uid.HasValue)
                                {
                                    child.Uid = susp.Uid;
                                }

                                if (susp.Gid.HasValue)
                                {
                                    child.Gid = susp.Gid;
                                }

                                if (susp.Inode.HasValue)
                                {
                                    child.Inode = susp.Inode;
                                }

                                if (susp.LinkCount.HasValue)
                                {
                                    child.LinkCount = susp.LinkCount;
                                }

                                if (susp.CreatedTime.HasValue)
                                {
                                    child.CreatedTime = susp.CreatedTime;
                                }

                                if (susp.AccessedTime.HasValue)
                                {
                                    child.AccessedTime = susp.AccessedTime;
                                }
                            }

                            child.Extents.Add(new FsExtent { Lba = child.Lba, Size = child.Size });
                            if (child.IsDirectory) ParseDirectory(child, trackStart);
                            dirNode.Children.Add(child);
                        }
                    }
                }

                pos += recordLen;
                if ((pos & 1) != 0)
                {
                    pos++;
                }
            }
        }

        return true;
    }

    private sealed class SuspInfo
    {
        public string? Name;
        public bool Relocated;
        public uint ChildLinkLba;
        public uint? UnixMode;
        public uint? Uid;
        public uint? Gid;
        public uint? Inode;
        public uint? LinkCount;
        public string? SymlinkTarget;
        public DateTime? CreatedTime;
        public DateTime? AccessedTime;
    }

    private void DetectSusp(uint rootLba)
    {
        var sec = new byte[2048];
        if (!_reader.ReadSector(rootLba, sec)) return;

        var recLen = sec[0];
        if (recLen < 34) return;

        var nameLen = sec[32];
        if (nameLen != 1 || sec[33] != 0x00) return; // must be the "." self record

        var su = 33 + nameLen + (0);
        if (su + 7 > recLen) return;

        // SUSP "SP" entry: 'S' 'P' len ver 0xBE 0xEF skip
        if (sec[su] == 'S' && sec[su + 1] == 'P' && sec[su + 2] >= 7 && sec[su + 4] == 0xBE && sec[su + 5] == 0xEF)
        {
            _suspActive = true;
            _suspSkip = sec[su + 6];
        }
    }

    private SuspInfo ParseSusp(byte[] sec, int suOffset, int suLen, uint trackStart)
    {
        var info = new SuspInfo();
        StringBuilder? nameBuilder = null;
        var nameDone = false;

        var buf = sec;
        var off = suOffset + _suspSkip;
        var len = suLen - _suspSkip;
        uint ceBlock = 0, ceOffset = 0, ceLen = 0;
        var haveCe = false;
        var chain = 0;

        while (true)
        {
            while (len >= 4)
            {
                var s0 = buf[off];
                var s1 = buf[off + 1];
                var entryLen = buf[off + 2];
                if (entryLen < 4 || entryLen > len) break;

                if (s0 == 'S' && s1 == 'T')
                {
                    break;
                }

                // CE: Continuation Entry (deferred)
                if (s0 == 'C' && s1 == 'E' && entryLen >= 28)
                {
                    ceBlock = LeU32(buf, off + 4);
                    ceOffset = LeU32(buf, off + 12);
                    ceLen = LeU32(buf, off + 20);
                    haveCe = true;
                }
                // NM: Alternate Name
                else if (s0 == 'N' && s1 == 'M' && entryLen >= 5 && !nameDone)
                {
                    var nmFlags = buf[off + 4];
                    var continued = (nmFlags & 0x01) != 0;

                    if ((nmFlags & 0x06) == 0 || entryLen > 5)
                    {
                        nameBuilder ??= new StringBuilder();
                        var nameBytes = entryLen - 5;
                        var nameStr = Encoding.UTF8.GetString(buf, off + 5, nameBytes);
                        var nul = nameStr.IndexOf('\0');
                        if (nul >= 0)
                        {
                            nameStr = nameStr[..nul];
                        }

                        nameBuilder.Append(nameStr);

                        if (!continued)
                        {
                            nameDone = true;
                        }
                    }
                }
                // PX: POSIX File Attributes
                else if (s0 == 'P' && s1 == 'X' && entryLen >= 36)
                {
                    info.UnixMode = BeU32(buf, off + 4);
                    info.LinkCount = BeU32(buf, off + 12);
                    info.Uid = BeU32(buf, off + 20);
                    info.Gid = BeU32(buf, off + 28);
                    if (entryLen >= 44)
                    {
                        info.Inode = BeU32(buf, off + 36);
                    }
                }
                // TF: Time Stamps
                else if (s0 == 'T' && s1 == 'F' && entryLen >= 5)
                {
                    var tfFlags = buf[off + 4];
                    var isLongForm = (tfFlags & 0x80) != 0;
                    var stampSize = isLongForm ? 17 : 7;
                    var stampOff = off + 5;
                    var idx = 0;

                    if ((tfFlags & 0x01) != 0 && stampOff + (idx + 1) * stampSize <= off + entryLen)
                    {
                        info.CreatedTime = isLongForm
                            ? ParseLongTimestamp(buf, stampOff + idx * stampSize)
                            : ParseRecordTime(buf, stampOff + idx * stampSize);
                    }

                    idx += (tfFlags & 0x01) != 0 ? 1 : 0;

                    if ((tfFlags & 0x02) != 0 && stampOff + (idx + 1) * stampSize <= off + entryLen)
                    {
                        info.AccessedTime = isLongForm
                            ? ParseLongTimestamp(buf, stampOff + idx * stampSize)
                            : ParseRecordTime(buf, stampOff + idx * stampSize);
                    }

                    idx += (tfFlags & 0x02) != 0 ? 1 : 0;

                    if ((tfFlags & 0x04) != 0 && stampOff + (idx + 1) * stampSize <= off + entryLen)
                    {
                        /* ModifiedTime set from ISO record; TF value skipped */
                    }
                }
                // SL: Symbolic Link
                else if (s0 == 'S' && s1 == 'L' && entryLen >= 5)
                {
                    var slFlags = buf[off + 4];
                    if ((slFlags & 0x02) != 0)
                    {
                        var slBuilder = new StringBuilder();
                        var slPos = off + 5;
                        while (slPos + 2 <= off + entryLen)
                        {
                            var compFlags = buf[slPos];
                            var compLen = buf[slPos + 1];
                            if (compLen < 2 || slPos + 2 + compLen > off + entryLen) break;

                            var component = Encoding.UTF8.GetString(buf, slPos + 2, compLen);
                            if ((compFlags & 0x04) != 0)
                                slBuilder.Append('/');
                            slBuilder.Append(component);
                            if ((compFlags & 0x01) == 0)
                                slBuilder.Append('/');

                            slPos += 2 + compLen;
                        }

                        info.SymlinkTarget = slBuilder.ToString();
                    }
                }
                // RE: Relocated Entry
                else if (s0 == 'R' && s1 == 'E')
                {
                    info.Relocated = true;
                }
                // CL: Child Link
                else if (s0 == 'C' && s1 == 'L' && entryLen >= 12)
                {
                    info.ChildLinkLba = LeU32(buf, off + 4);
                }

                off += entryLen;
                len -= entryLen;
            }

            if (!haveCe || chain++ >= MaxCeChain) break;
            if (ceOffset >= 2048 || ceLen == 0) break;

            var ceSec = new byte[2048];
            if (!_reader.ReadSector(trackStart + ceBlock, ceSec)) break;

            buf = ceSec;
            off = (int)ceOffset;
            len = (int)Math.Min(ceLen, 2048 - ceOffset);
            haveCe = false;
        }

        info.Name = nameBuilder?.ToString();
        return info;
    }

    private bool ReadSelfRecord(uint dirLba, out uint extentLba, out uint extentSize)
    {
        extentLba = 0;
        extentSize = 0;

        var sec = new byte[2048];
        if (!_reader.ReadSector(dirLba, sec)) return false;
        if (sec[0] < 34 || sec[32] != 1 || sec[33] != 0x00) return false;

        extentLba = LeU32(sec, 2) + sec[1];
        extentSize = LeU32(sec, 10);
        return true;
    }

    private static void TryParseXa(byte[] data, int suOffset, int suLen, byte nameLen, out byte fileNumber, out bool interleaved)
    {
        fileNumber = 0;
        interleaved = false;

        // Some mastering tools pad the system-use area before the XA record (Win98 quirk)
        Span<int> candidates = [0, nameLen + (nameLen & 1)];
        foreach (var cand in candidates)
        {
            var off = suOffset + cand;
            if (cand + 14 > suLen) continue;
            if (data[off + 6] != 'X' || data[off + 7] != 'A') continue;

            var reservedZero = true;
            for (var r = 9; r < 14; r++)
            {
                if (data[off + r] != 0)
                {
                    reservedZero = false;
                    break;
                }
            }

            if (!reservedZero) continue;

            var attributes = (ushort)((data[off + 4] << 8) | data[off + 5]);
            fileNumber = data[off + 8];
            interleaved = (attributes & 0x2000) != 0; // XA_ATTR_INTERLEAVED
            return;
        }
    }

    private DateTime? ParseRecordTime(byte[] d, int off)
    {
        var allZero = true;
        for (var i = 0; i < 7; i++)
        {
            if (d[off + i] != 0)
            {
                allZero = false;
                break;
            }
        }

        if (allZero) return null;

        var year = 1900 + d[off];
        int month = d[off + 1], day = d[off + 2], hour = d[off + 3], minute = d[off + 4], second = d[off + 5];

        if (month is < 1 or > 12 || day is < 1 or > 31) return null;
        if (hour > 23 || minute > 59 || second > 59) return null;

        var tzMinutes = _isHighSierra ? 0 : 15 * (sbyte)d[off + 6];
        if (tzMinutes is < -14 * 60 or > 14 * 60)
        {
            tzMinutes = 0;
        }

        try
        {
            return new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.FromMinutes(tzMinutes)).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static bool IsJolietSvd(byte[] d)
    {
        // Joliet escape sequences @ SVD offset 88: %/@ %/C %/E
        return d[88] == 0x25 && d[89] == 0x2F && d[90] is 0x40 or 0x43 or 0x45;
    }

    private string DecodeName(byte[] data, int offset, byte nameLen)
    {
        if (_isJoliet) return DecodeUtf16Be(data, offset, nameLen);

        switch (nameLen)
        {
            case 1 when data[offset] == 0x00:
                return ".";
            case 1 when data[offset] == 0x01:
                return "..";
        }

        var name = Encoding.ASCII.GetString(data, offset, nameLen);
        var semi = name.IndexOf(';');
        if (semi >= 0)
        {
            name = name[..semi];
        }

        if (name.EndsWith('.'))
        {
            name = name[..^1];
        }

        return name;
    }

    private static string DecodeUtf16Be(byte[] data, int offset, int len)
    {
        switch (len)
        {
            case 1 when data[offset] == 0x00:
                return ".";
            case 1 when data[offset] == 0x01:
                return "..";
        }

        var name = Encoding.BigEndianUnicode.GetString(data, offset, len & ~1);
        var nul = name.IndexOf('\0');
        if (nul >= 0)
        {
            name = name[..nul];
        }

        var semi = name.IndexOf(';');
        return semi >= 0 ? name[..semi] : name;
    }

    private static bool CheckMagic(byte[] data, int offset, string magic)
    {
        return Encoding.ASCII.GetString(data, offset, magic.Length) == magic;
    }

    private static uint LeU32(byte[] data, int offset)
    {
        return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
    }

    private static uint BeU32(byte[] data, int offset)
    {
        return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
    }

    private static DateTime? ParseLongTimestamp(byte[] d, int off)
    {
        var allZero = true;
        for (var i = 0; i < 16; i++)
        {
            if (d[off + i] != 0 && d[off + i] != '0')
            {
                allZero = false;
                break;
            }
        }

        if (allZero) return null;

        var str = Encoding.ASCII.GetString(d, off, 16);
        try
        {
            var year = int.Parse(str[..4], CultureInfo.InvariantCulture);
            var month = int.Parse(str[4..6], CultureInfo.InvariantCulture);
            var day = int.Parse(str[6..8], CultureInfo.InvariantCulture);
            var hour = int.Parse(str[8..10], CultureInfo.InvariantCulture);
            var minute = int.Parse(str[10..12], CultureInfo.InvariantCulture);
            var second = int.Parse(str[12..14], CultureInfo.InvariantCulture);

            if (month is < 1 or > 12 || day is < 1 or > 31) return null;
            if (hour > 23 || minute > 59 || second > 59) return null;

            var tzMinutes = 15 * (sbyte)d[off + 16];
            if (tzMinutes is < -14 * 60 or > 14 * 60)
            {
                tzMinutes = 0;
            }

            return new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.FromMinutes(tzMinutes)).UtcDateTime;
        }
        catch
        {
            return null;
        }
    }
}
