using System.Text;

namespace VideoGameFileSystemParser.Parsers.Systems;

/// <summary>
/// Dedicated NEC PC-FX ISO 9660 parser.
/// Handles byte-offset VD signatures within raw sectors and uses tolerant
/// continue-on-error directory record parsing for discs with unusual layouts.
/// </summary>
public class PcFxIsoParser
{
    private readonly SectorReader _reader;
    private readonly HashSet<uint> _visitedDirs = [];
    private bool _isHighSierra;
    private bool _isJoliet;

    /// <summary>
    /// Initializes a new instance of the <see cref="PcFxIsoParser"/> class.
    /// </summary>
    /// <param name="reader">The sector reader to use for reading disc data.</param>
    public PcFxIsoParser(SectorReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// Parses the PC-FX ISO 9660 file system and builds the directory tree.
    /// </summary>
    /// <param name="rootNode">The root FsNode to populate.</param>
    /// <param name="track">Optional track to restrict parsing to.</param>
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
        _visitedDirs.Clear();

        var effectiveTrackStart = (track?.StartLba ?? 0) + (track?.Pregap ?? 0);
        var sectorData = new byte[2048];

        var vdOffsets = new List<uint> { 16, 17 };
        if (effectiveTrackStart != (track?.StartLba ?? 0))
        {
            vdOffsets.Add(166);
            vdOffsets.Add(167);
        }

        var foundPvd = false;
        byte[]? bestVdData = null;
        var pvdOffsetInSector = 0;

        foreach (var offset in vdOffsets)
        {
            if (_reader.ReadSector(effectiveTrackStart + offset, sectorData))
            {
                var vdType = CheckVdInSector(sectorData, out var currentOffsetInSector);
                if (vdType > 0)
                {
                    _isHighSierra = vdType == 2 || (vdType == 3 && CheckMagic(sectorData, currentOffsetInSector + 9, "CDROM"));
                    _isJoliet = !_isHighSierra && sectorData[currentOffsetInSector] == 2;
                    foundPvd = true;
                    bestVdData = (byte[])sectorData.Clone();
                    pvdOffsetInSector = currentOffsetInSector;
                    break;
                }
            }
        }

        if (!foundPvd && effectiveTrackStart != 0)
        {
            _reader.SetTrack(null);
            foreach (var offset in vdOffsets)
            {
                if (_reader.ReadSector(offset, sectorData))
                {
                    var vdType = CheckVdInSector(sectorData, out var currentOffsetInSector);
                    if (vdType > 0)
                    {
                        effectiveTrackStart = 0;
                        _reader.SetTrack(null);
                        _isHighSierra = vdType == 2 || (vdType == 3 && CheckMagic(sectorData, currentOffsetInSector + 9, "CDROM"));
                        _isJoliet = !_isHighSierra && sectorData[currentOffsetInSector] == 2;
                        foundPvd = true;
                        bestVdData = (byte[])sectorData.Clone();
                        pvdOffsetInSector = currentOffsetInSector;
                        break;
                    }
                }
            }
        }

        if (!foundPvd)
        {
            var scanLimit = track is { Frames: > 0 } ? Math.Min(track.Frames, 5000u) : 5000u;
            for (uint i = 0; i < scanLimit; i++)
            {
                if (_reader.ReadSector(effectiveTrackStart + i, sectorData))
                {
                    var vdType = CheckVdInSector(sectorData, out var currentOffsetInSector);
                    if (vdType > 0)
                    {
                        _isHighSierra = vdType == 2 || (vdType == 3 && CheckMagic(sectorData, currentOffsetInSector + 9, "CDROM"));
                        _isJoliet = !_isHighSierra && sectorData[currentOffsetInSector] == 2;
                        foundPvd = true;
                        bestVdData = (byte[])sectorData.Clone();
                        pvdOffsetInSector = currentOffsetInSector;
                        break;
                    }
                }
            }
        }

        if (!foundPvd)
            return false;

        var rootRecOff = pvdOffsetInSector + (_isHighSierra ? 180 : 156);
        var rootRelLba = LeU32(bestVdData!, rootRecOff + 2);
        var rootSize = LeU32(bestVdData!, rootRecOff + 10);

        var baseLba = ResolveDirectoryBase(effectiveTrackStart, rootRelLba, track);

        rootNode.Name = "/";
        rootNode.IsDirectory = true;
        rootNode.Lba = unchecked(baseLba + rootRelLba);
        rootNode.Size = rootSize;
        rootNode.Extents.Add(new FsExtent { Lba = rootNode.Lba, Size = rootSize });

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
    /// Checks a sector for a valid Volume Descriptor signature (CD001 or CDROM).
    /// First checks at standard positions, then scans the entire sector for
    /// byte-offset occurrences (handles raw-sector images with un-stripped headers).
    /// </summary>
    private static int CheckVdInSector(byte[] data, out int foundOffset)
    {
        foundOffset = 0;
        switch (data.Length)
        {
            case < 16:
                return 0;
            case >= 6 when CheckMagic(data, 1, "CD001"):
                foundOffset = 0;
                return 1;
            case >= 14 when CheckMagic(data, 9, "CDROM"):
                foundOffset = 0;
                return 2;
        }

        for (var i = 0; i < data.Length - 16; i++)
        {
            if (CheckMagic(data, i + 1, "CD001") || CheckMagic(data, i + 9, "CDROM"))
            {
                foundOffset = i;
                return 3;
            }
        }

        return 0;
    }

    /// <summary>
    /// Parses a directory sector chain. Uses continue (not break) for invalid
    /// records, matching the C++ DiscImageCreator behavior. This is more tolerant
    /// of discs with unusual record layouts.
    /// </summary>
    private bool ParseDirectory(FsNode dirNode, uint baseLba)
    {
        if (!_visitedDirs.Add(dirNode.Lba))
            return true;

        var sectorsToRead = (uint)((dirNode.Size + 2047) / 2048);
        var sectorData = new byte[2048];

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
                var extentSize = (ulong)LeU32(sectorData, (int)(pos + 10));

                var flagsOff = _isHighSierra ? 24 : 25;
                var flags = sectorData[pos + flagsOff];
                var isDirectory = (flags & 0x02) != 0;
                var isMultiExtent = (flags & 0x80) != 0;

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

                var name = DecodeName(sectorData, (int)(pos + nameOff), nameLen);

                if (name != "." && name != "..")
                {
                    var absoluteLba = baseLba + relLba + xattrLen;
                    var last = dirNode.Children.Count > 0 ? dirNode.Children[^1] : null;

                    if (last != null && isMultiExtent && !last.IsDirectory && last.Name == name)
                    {
                        last.Size += extentSize;
                        last.Extents.Add(new FsExtent { Lba = absoluteLba, Size = extentSize });
                        last.IsMultiExtent = isMultiExtent;
                    }
                    else
                    {
                        var child = new FsNode
                        {
                            Name = name,
                            Lba = absoluteLba,
                            Size = extentSize,
                            IsDirectory = isDirectory,
                            IsMultiExtent = isMultiExtent,
                            ModifiedTime = ParseRecordTime(sectorData, (int)pos + 18)
                        };
                        child.Extents.Add(new FsExtent { Lba = child.Lba, Size = child.Size });

                        if (child.IsDirectory)
                            ParseDirectory(child, baseLba);

                        dirNode.Children.Add(child);
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

    private string DecodeName(byte[] data, int offset, byte nameLen)
    {
        if (_isJoliet)
            return DecodeUtf16Be(data, offset, nameLen);

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

    private static bool CheckMagic(byte[] data, int offset, string magic)
    {
        if (offset + magic.Length > data.Length) return false;

        for (var i = 0; i < magic.Length; i++)
        {
            if (data[offset + i] != magic[i]) return false;
        }

        return true;
    }

    private static uint LeU32(byte[] data, int offset)
    {
        return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
    }
}
