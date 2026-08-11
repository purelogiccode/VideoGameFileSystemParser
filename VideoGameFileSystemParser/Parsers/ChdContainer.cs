using System.Buffers;
using System.Globalization;
using System.Text;
using CHDSharp;
using CHDSharp.Models;

namespace VideoGameFileSystemParser.Parsers;

/// <summary>
/// Opens and manages a CHD disc image, providing file system access via console-specific parsers
/// or virtual CUE/BIN export for raw image access.
/// </summary>
public class ChdContainer : IDisposable, IAsyncDisposable
{
    private const uint SectorSize = 2048;
    private const uint InvalidHandle = uint.MaxValue;

    private readonly List<FileEntry> _entries = [];
    private readonly List<uint> _parentHandles = [];
    private readonly Dictionary<string, uint> _entryMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SectorReader> _readerPool = [];
    private readonly List<SectorReader> _availableReaders = [];
    private readonly object _poolLock = new();
    private bool _poolShutdown;
    private readonly string _chdPath;
    private uint _rootHandle;

    private ChdFile? _primaryChd;

    private enum CueExportMode
    {
        CueBin2352,
        CueBin2048,
        CueIso2352,
        CueIso2048,
        CueBinWav2352,
        CueBinWav2048,
        CueIsoWav2352,
        CueIsoWav2048
    }

    private bool _cueExportEnabled;
    private CueExportMode _cueMode;
    private string _cueText = "";
    private string _cueStemName = "";
    private ulong _cueBinSize;
    private uint _cueSectorSize;
    private List<TrackInfo>? _cachedTracks;
    private Dictionary<int, byte[]>? _wavHeaders;
    private Dictionary<int, ulong>? _wavDataSizes;

    /// <summary>
    /// Gets the read-only list of all file and directory entries in the container.
    /// </summary>
    public IReadOnlyList<FileEntry> Entries => _entries;

    /// <summary>
    /// Gets the volume name (derived from the CHD file name).
    /// </summary>
    public string VolumeName { get; private set; } = "";

    /// <summary>
    /// Gets the total size of the disc image in bytes.
    /// </summary>
    public ulong VolumeSize { get; private set; }

    /// <summary>
    /// Gets whether the CHD contains at least one data track.
    /// </summary>
    public bool HasDataTracks { get; private set; }

    /// <summary>
    /// Gets the number of bytes per sector unit (e.g., 2048 or 2352).
    /// </summary>
    public uint UnitBytes { get; private set; }

    /// <summary>
    /// Gets the number of bytes per compressed hunk.
    /// </summary>
    public uint HunkBytes { get; private set; }

    /// <summary>
    /// Gets or sets the console type used for parsing this image.
    /// </summary>
    public ConsoleType ConsoleType { get; set; } = ConsoleType.Unknown;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChdContainer"/> class.
    /// </summary>
    /// <param name="chdPath">The file system path to the CHD disc image.</param>
    public ChdContainer(string chdPath)
    {
        _chdPath = chdPath;
    }

    /// <summary>
    /// Opens the CHD file and initializes the reader pool for the specified console type.
    /// </summary>
    /// <param name="consoleType">The console type to configure the reader for.</param>
    /// <returns><c>true</c> if the CHD was opened successfully; otherwise <c>false</c>.</returns>
    public bool Open(ConsoleType consoleType)
    {
        ConsoleType = consoleType;

        var err = ChdFile.Open(_chdPath, out var chd);
        if (err != ChdError.Chderrnone || chd is null)
            return false;

        _primaryChd = chd;

        var unitBytes = chd.UnitBytes;
        var reader = new SectorReader(chd, unitBytes);
        UnitBytes = unitBytes;
        HunkBytes = chd.HunkBytes;
        VolumeSize = chd.TotalBytes;
        VolumeName = Path.GetFileNameWithoutExtension(_chdPath);

        _readerPool.Add(reader);
        HasDataTracks = reader.Tracks.Any(static t => t.IsDataTrack);
        lock (_poolLock)
        {
            _availableReaders.Add(reader);
        }

        return true;
    }

    /// <summary>
    /// Opens the CHD, creates the appropriate parser, parses the file system, and builds the entry tree.
    /// </summary>
    /// <param name="consoleType">The console type to parse the image as.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise <c>false</c>.</returns>
    public bool MountAndParse(ConsoleType consoleType)
    {
        if (!Open(consoleType))
            return false;

        if (consoleType is ConsoleType.GenericCueBin2352 or ConsoleType.GenericCueBin2048
            or ConsoleType.GenericCueIso2352 or ConsoleType.GenericCueIso2048
            or ConsoleType.GenericCueBinWav2352 or ConsoleType.GenericCueBinWav2048
            or ConsoleType.GenericCueIsoWav2352 or ConsoleType.GenericCueIsoWav2048)
        {
            var rootNode = new FsNode { Name = "/", IsDirectory = true };
            BuildFromFsNode(rootNode);

            // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
            var mode = consoleType switch
            {
                ConsoleType.GenericCueBin2352 => CueExportMode.CueBin2352,
                ConsoleType.GenericCueBin2048 => CueExportMode.CueBin2048,
                ConsoleType.GenericCueIso2352 => CueExportMode.CueIso2352,
                ConsoleType.GenericCueIso2048 => CueExportMode.CueIso2048,
                ConsoleType.GenericCueBinWav2352 => CueExportMode.CueBinWav2352,
                ConsoleType.GenericCueBinWav2048 => CueExportMode.CueBinWav2048,
                ConsoleType.GenericCueIsoWav2352 => CueExportMode.CueIsoWav2352,
                ConsoleType.GenericCueIsoWav2048 => CueExportMode.CueIsoWav2048,
                _ => throw new InvalidOperationException(
                    $"Unexpected console type: {consoleType}")
            };

            BuildVirtualCueExport(mode);
            return true;
        }

        var parser = ParserFactory.CreateParser(consoleType, _readerPool[0]);
        if (parser is null)
            return false;

        var parsedRoot = new FsNode();
        if (!parser.Parse(parsedRoot))
            return false;

        BuildFromFsNode(parsedRoot);

        if (consoleType is ConsoleType.PcEngineCd or ConsoleType.PcFx)
            BuildVirtualCueExport(CueExportMode.CueBin2352);

        return true;
    }

    /// <summary>
    /// Builds the internal file entry table from a parsed <see cref="FsNode"/> tree.
    /// </summary>
    /// <param name="rootNode">The root node of the parsed file system tree.</param>
    public void BuildFromFsNode(FsNode rootNode)
    {
        _entries.Clear();
        _parentHandles.Clear();
        _entryMap.Clear();

        var rootEntry = new FileEntry { Name = "\\", FullPath = "\\", Lba = rootNode.Lba, Size = rootNode.Size, IsDirectory = true };
        _rootHandle = RegisterEntry(rootEntry, InvalidHandle);

        foreach (var child in rootNode.Children)
            AddFsNodeRecursive(child, _rootHandle, "\\");
    }

    private void AddFsNodeRecursive(FsNode node, uint parentHandle, string parentPath)
    {
        var currentPath = parentPath == "\\" ? $"\\{node.Name}" : $"{parentPath}\\{node.Name}";

        var entry = new FileEntry
        {
            Name = node.Name,
            FullPath = currentPath,
            Lba = node.Lba,
            Size = node.Size,
            IsDirectory = node.IsDirectory,
            FileNumber = node.FileNumber,
            IsInterleaved = node.IsInterleaved,
            IsRawPassthrough = node.IsRawPassthrough,
            IsEmbedded = node.IsEmbedded,
            Offset = node.EmbeddedOffset
        };

        if (node.ModifiedTime.HasValue)
        {
            entry.ModifiedTime = node.ModifiedTime.Value;
        }

        foreach (var ext in node.Extents)
            entry.Extents.Add(new FileExtent { Lba = ext.Lba, Size = ext.Size });

        var handle = RegisterEntry(entry, parentHandle);

        if (entry.IsDirectory)
        {
            foreach (var child in node.Children)
                AddFsNodeRecursive(child, handle, currentPath);
        }
    }

    private uint RegisterEntry(FileEntry entry, uint parent)
    {
        _entries.Add(entry);
        _parentHandles.Add(parent);
        var handle = (uint)(_entries.Count - 1);
        _entryMap[ResolveEntryKey(handle)] = handle;
        return handle;
    }

    private string ResolveEntryKey(uint handle)
    {
        var parts = new List<string>();
        var current = handle;
        while (current != InvalidHandle)
        {
            parts.Add(_entries[(int)current].Name);
            current = _parentHandles[(int)current];
        }

        parts.Reverse();
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (part == "\\")
            {
                sb.Append('\\');
            }
            else
            {
                if (sb.Length > 0 && sb[^1] != '\\') sb.Append('\\');
                sb.Append(part);
            }
        }

        var path = sb.ToString().ToLowerInvariant();
        if (path.Length > 1 && path[^1] == '\\')
        {
            path = path[..^1];
        }

        if (string.IsNullOrEmpty(path))
        {
            path = "\\";
        }

        return path;
    }

    /// <summary>
    /// Finds a file or directory entry by its full path.
    /// </summary>
    /// <param name="path">The full path to search for (e.g., "\GAME\DATA.BIN").</param>
    /// <returns>The matching <see cref="FileEntry"/>, or <c>null</c> if not found.</returns>
    public FileEntry? FindFile(string path)
    {
        var key = MakeEntryKey(path);
        return _entryMap.TryGetValue(key, out var handle) ? _entries[(int)handle] : null;
    }

    /// <summary>
    /// Attempts to find a file or directory entry by its full path.
    /// </summary>
    /// <param name="path">The full path to search for.</param>
    /// <param name="entry">When successful, the matching <see cref="FileEntry"/>.</param>
    /// <param name="error">When not found, a description of why (e.g., "not found" or "container disposed").</param>
    /// <returns><c>true</c> if the entry was found; otherwise <c>false</c>.</returns>
    public bool TryFindFile(string path, out FileEntry? entry, out string? error)
    {
        if (_poolShutdown)
        {
            entry = null;
            error = "Container is disposed.";
            return false;
        }

        var key = MakeEntryKey(path);
        if (_entryMap.TryGetValue(key, out var handle))
        {
            entry = _entries[(int)handle];
            error = null;
            return true;
        }

        entry = null;
        error = $"Path not found: {path}";
        return false;
    }

    private static string MakeEntryKey(string path)
    {
        if (string.IsNullOrEmpty(path) || path is "\\" or "/") return "\\";

        var result = path.Replace('/', '\\').ToLowerInvariant();
        if (result[0] != '\\')
        {
            result = '\\' + result;
        }

        while (result.Length > 1 && result[^1] == '\\')
        {
            result = result[..^1];
        }

        return result;
    }

    /// <summary>
    /// Enumerates the child entries of a directory specified by path.
    /// </summary>
    /// <param name="path">The full path of the directory.</param>
    /// <returns>An enumeration of <see cref="FileEntry"/> items in the directory.</returns>
    public IEnumerable<FileEntry> ListDirectory(string path)
    {
        var key = MakeEntryKey(path);
        if (!_entryMap.TryGetValue(key, out var handle)) yield break;

        for (uint i = 0; i < _parentHandles.Count; i++)
            if (_parentHandles[(int)i] == handle)
                yield return _entries[(int)i];
    }

    /// <summary>
    /// Reads data from a file entry at the specified offset into the provided buffer.
    /// </summary>
    /// <param name="entry">The file entry to read from.</param>
    /// <param name="offset">The byte offset within the file to start reading from.</param>
    /// <param name="buffer">The destination buffer.</param>
    /// <param name="bufOffset">The offset within the destination buffer to begin writing.</param>
    /// <param name="count">The maximum number of bytes to read.</param>
    /// <returns>The number of bytes actually read.</returns>
    public int ReadFile(FileEntry entry, ulong offset, byte[] buffer, int bufOffset, int count)
    {
        if (entry.IsDirectory || offset >= entry.Size)
            return 0;

        var remaining = entry.Size - offset;
        var bytesToRead = (int)(remaining < (ulong)count ? remaining : (ulong)count);

        if (_cueExportEnabled)
        {
            if (string.Equals(entry.Name, _cueStemName + ".cue", StringComparison.OrdinalIgnoreCase))
            {
                if (offset >= (ulong)_cueText.Length) return 0;

                var cueRead = Math.Min(bytesToRead, _cueText.Length - (int)offset);
                Encoding.ASCII.GetBytes(_cueText, (int)offset, cueRead, buffer, bufOffset);
                return cueRead;
            }

            if (string.Equals(entry.Name, _cueStemName + ".bin", StringComparison.OrdinalIgnoreCase))
                return ReadVirtualBin(offset, buffer, bufOffset, bytesToRead,
                    _cueMode is CueExportMode.CueBinWav2352 or CueExportMode.CueBinWav2048);

            if (string.Equals(entry.Name, _cueStemName + ".iso", StringComparison.OrdinalIgnoreCase))
                return ReadVirtualBin(offset, buffer, bufOffset, bytesToRead,
                    _cueMode is CueExportMode.CueIsoWav2352 or CueExportMode.CueIsoWav2048);

            if (TryParseWavTrackIndex(entry.Name, out var wavTrackIdx))
                return ReadVirtualWav(wavTrackIdx, offset, buffer, bufOffset, bytesToRead);
        }

        if (entry.IsRawPassthrough)
        {
            return ReadRawChdBytes(offset, buffer, bufOffset, bytesToRead);
        }

        var reader = AcquireReader();

        reader.SetTrack(null);

        var sec = ArrayPool<byte>.Shared.Rent((int)SectorSize);
        try
        {
            var totalRead = 0;
            if (entry.IsEmbedded)
            {
                Array.Clear(sec, 0, (int)SectorSize);
                if (!reader.ReadSector(entry.Lba, sec)) return 0;

                var start = entry.Offset + offset;
                if (start >= SectorSize) return 0;

                var chunk = Math.Min(bytesToRead, (int)(SectorSize - start));
                Array.Copy(sec, (int)start, buffer, bufOffset, chunk);
                return chunk;
            }

            if (!entry.IsInterleaved)
            {
                while (totalRead < bytesToRead)
                {
                    var curOff = offset + (ulong)totalRead;
                    var baseLba = entry.Lba;
                    var offsetInExtent = curOff;
                    if (entry.Extents.Count > 0)
                    {
                        ulong extentStart = 0;
                        foreach (var ext in entry.Extents)
                        {
                            if (curOff >= extentStart && curOff < extentStart + ext.Size)
                            {
                                baseLba = ext.Lba;
                                offsetInExtent = curOff - extentStart;
                                break;
                            }

                            extentStart += ext.Size;
                        }
                    }

                    var secNum = baseLba + (uint)(offsetInExtent / SectorSize);
                    var secOff = (uint)(offsetInExtent % SectorSize);
                    Array.Clear(sec, 0, (int)SectorSize);
                    if (!reader.ReadSector(secNum, sec)) break;

                    var chunk = Math.Min((int)(SectorSize - secOff), bytesToRead - totalRead);
                    Array.Copy(sec, (int)secOff, buffer, bufOffset + totalRead, chunk);
                    totalRead += chunk;
                }
            }
            else
            {
                var psec = entry.Lba;
                uint scanned = 0;
                while (totalRead < bytesToRead && scanned < 500000)
                {
                    scanned++;
                    var fn = reader.GetSubheaderFileNumber(psec);
                    psec++;
                    if (fn != entry.FileNumber) continue;

                    Array.Clear(sec, 0, (int)SectorSize);
                    if (!reader.ReadSector(psec - 1, sec)) break;

                    var toCopy = Math.Min((int)SectorSize, bytesToRead - totalRead);
                    Array.Copy(sec, 0, buffer, bufOffset + totalRead, toCopy);
                    totalRead += toCopy;
                }
            }

            return totalRead;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(sec);
            ReleaseReader(reader);
        }
    }

    private void BuildVirtualCueExport(CueExportMode mode)
    {
        if (_primaryChd == null) return;

        _cachedTracks = SectorReader.ParseTracksWithLba(_primaryChd, UnitBytes);
        if (_cachedTracks.Count == 0) return;

        _cueExportEnabled = true;
        _cueMode = mode;
        _cueStemName = Path.GetFileNameWithoutExtension(_chdPath);

        var isIsoMode = mode is CueExportMode.CueIso2352 or CueExportMode.CueIso2048
            or CueExportMode.CueIsoWav2352 or CueExportMode.CueIsoWav2048;
        var isWavMode = mode is CueExportMode.CueBinWav2352 or CueExportMode.CueBinWav2048
            or CueExportMode.CueIsoWav2352 or CueExportMode.CueIsoWav2048;

        // Data-track sector size: 2048 for the cooked variants, otherwise the
        // raw unit size (capped at 2352). Audio tracks inside BINARY files
        // always use 2352-byte sectors (see VirtualTrackSectorSize).
        _cueSectorSize = mode switch
        {
            CueExportMode.CueBin2048 or CueExportMode.CueIso2048
                or CueExportMode.CueBinWav2048 or CueExportMode.CueIsoWav2048 => 2048u,
            _ => Math.Min(UnitBytes, 2352u)
        };

        _wavHeaders = new Dictionary<int, byte[]>();
        _wavDataSizes = new Dictionary<int, ulong>();

        uint cumulativeFrames = 0;
        _cueBinSize = 0;
        var sb = new StringBuilder();

        var hasDataTracks = false;
        foreach (var t in _cachedTracks)
        {
            if (t.IsDataTrack)
            {
                hasDataTracks = true;
                break;
            }
        }

        var currentFile = "";
        var freshFile = true;

        var trackNum = 0;
        uint dataFileFrames = 0;
        foreach (var t in _cachedTracks)
        {
            trackNum++;

            if (t.IsDataTrack)
            {
                var dataFileExt = isIsoMode ? "iso" : "bin";
                var dataFileName = $"{_cueStemName}.{dataFileExt}";

                if (currentFile != dataFileName)
                {
                    if (!freshFile)
                        sb.AppendLine();
                    sb.AppendLine(CultureInfo.InvariantCulture, $"FILE \"{dataFileName}\" BINARY");
                    currentFile = dataFileName;
                }

                var modeStr = t.TrackType.Contains("MODE2") || t.TrackType.Contains("CDI")
                    ? $"MODE2/{_cueSectorSize}"
                    : $"MODE1/{_cueSectorSize}";

                sb.AppendLine(CultureInfo.InvariantCulture, $"  TRACK {trackNum:D2} {modeStr}");

                // In WAV modes the BIN/ISO contains only the data tracks, so the
                // INDEX positions must be relative to the data-file content, not
                // the cumulative disc frame stream (which would be wrong when an
                // audio track precedes a data track or when data tracks are
                // separated by audio). In single-file (non-WAV) modes both are
                // the same stream.
                var dataFilePos = isWavMode ? dataFileFrames : cumulativeFrames;

                if (t.Pregap > 0)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    INDEX 00 {SectorToMsf(dataFilePos)}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    INDEX 01 {SectorToMsf(dataFilePos + t.Pregap)}");
                }
                else
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    INDEX 01 {SectorToMsf(dataFilePos)}");
                }

                cumulativeFrames += t.Frames;
                dataFileFrames += t.Frames;
                _cueBinSize += (ulong)t.Frames * _cueSectorSize;
            }
            else
            {
                if (isWavMode)
                {
                    var wavFileName = $"{_cueStemName}_Track{trackNum:D2}.wav";

                    if (currentFile != wavFileName)
                    {
                        if (!freshFile)
                            sb.AppendLine();
                        sb.AppendLine(CultureInfo.InvariantCulture, $"FILE \"{wavFileName}\" WAVE");
                        currentFile = wavFileName;
                    }

                    // The WAV contains only the audible track data; the pregap
                    // frames (silence stored at the start of the track chunk)
                    // are skipped, matching the INDEX 01 00:00:00 in the CUE.
                    var musicFrames = t.Frames > t.Pregap ? t.Frames - t.Pregap : 0u;
                    var pcmSize = (ulong)musicFrames * 2352;
                    _wavHeaders[trackNum] = BuildWavHeader(pcmSize);
                    _wavDataSizes[trackNum] = pcmSize;
                }
                else
                {
                    var containerFile = isIsoMode ? $"{_cueStemName}.iso" : $"{_cueStemName}.bin";
                    if (currentFile != containerFile)
                    {
                        if (!freshFile)
                            sb.AppendLine();
                        sb.AppendLine(CultureInfo.InvariantCulture, $"FILE \"{containerFile}\" BINARY");
                        currentFile = containerFile;
                    }
                }

                sb.AppendLine(CultureInfo.InvariantCulture, $"  TRACK {trackNum:D2} AUDIO");

                if (isWavMode)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    INDEX 01 00:00:00");
                }
                else if (t.Pregap > 0)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    INDEX 00 {SectorToMsf(cumulativeFrames)}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    INDEX 01 {SectorToMsf(cumulativeFrames + t.Pregap)}");
                }
                else
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    INDEX 01 {SectorToMsf(cumulativeFrames)}");
                }

                cumulativeFrames += t.Frames;

                if (!isWavMode)
                {
                    _cueBinSize += (ulong)t.Frames * VirtualTrackSectorSize(t);
                }
            }

            freshFile = false;
        }

        _cueText = sb.ToString();

        var cueEntry = new FileEntry
        {
            Name = _cueStemName + ".cue",
            Lba = 0,
            Size = (ulong)_cueText.Length,
            IsDirectory = false
        };
        RegisterEntry(cueEntry, _rootHandle);

        // The CUE references the BIN/ISO file whenever there is at least one
        // data track, and also in single-file (non-WAV) modes where audio
        // tracks share the container. Register it in both cases so audio-only
        // discs produce a working mount instead of a CUE pointing at a file
        // that does not exist.
        if (hasDataTracks || !isWavMode)
        {
            var dataFileExt = isIsoMode ? "iso" : "bin";
            var dataEntry = new FileEntry
            {
                Name = _cueStemName + "." + dataFileExt,
                Lba = 0,
                Size = _cueBinSize,
                IsDirectory = false
            };
            RegisterEntry(dataEntry, _rootHandle);
        }

        if (isWavMode)
        {
            trackNum = 0;
            foreach (var t in _cachedTracks)
            {
                trackNum++;
                if (!t.IsDataTrack)
                {
                    var wavTotalSize = 44ul + _wavDataSizes![trackNum];
                    var wavEntry = new FileEntry
                    {
                        Name = _cueStemName + "_Track" + $"{trackNum:D2}" + ".wav",
                        Lba = 0,
                        Size = wavTotalSize,
                        IsDirectory = false
                    };
                    RegisterEntry(wavEntry, _rootHandle);
                }
            }
        }
    }

    private bool TryParseWavTrackIndex(string entryName, out int trackIndex)
    {
        trackIndex = 0;
        if (_wavHeaders == null) return false;
        if (string.IsNullOrEmpty(_cueStemName)) return false;

        var prefix = _cueStemName + "_Track";
        const string suffix = ".wav";
        if (!entryName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        if (!entryName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return false;

        var numStr = entryName.Substring(prefix.Length, entryName.Length - prefix.Length - suffix.Length);
        return int.TryParse(numStr, out trackIndex) && _wavHeaders.ContainsKey(trackIndex);
    }

    private static byte[] BuildWavHeader(ulong pcmDataSize)
    {
        var header = new byte[44];
        if (pcmDataSize > uint.MaxValue - 36)
        {
            pcmDataSize = uint.MaxValue - 36;
        }

        var riffSize = (uint)(36 + pcmDataSize);

        Encoding.ASCII.GetBytes("RIFF", 0, 4, header, 0);
        Array.Copy(BitConverter.GetBytes(riffSize), 0, header, 4, 4);
        Encoding.ASCII.GetBytes("WAVE", 0, 4, header, 8);
        Encoding.ASCII.GetBytes("fmt ", 0, 4, header, 12);
        Array.Copy(BitConverter.GetBytes(16u), 0, header, 16, 4);
        Array.Copy(BitConverter.GetBytes((ushort)1), 0, header, 20, 2);
        Array.Copy(BitConverter.GetBytes((ushort)2), 0, header, 22, 2);
        Array.Copy(BitConverter.GetBytes(44100u), 0, header, 24, 4);
        Array.Copy(BitConverter.GetBytes(176400u), 0, header, 28, 4);
        Array.Copy(BitConverter.GetBytes((ushort)4), 0, header, 32, 2);
        Array.Copy(BitConverter.GetBytes((ushort)16), 0, header, 34, 2);
        Encoding.ASCII.GetBytes("data", 0, 4, header, 36);
        Array.Copy(BitConverter.GetBytes((uint)pcmDataSize), 0, header, 40, 4);

        return header;
    }

    private static string SectorToMsf(uint sectors)
    {
        var m = sectors / (75 * 60);
        var s = sectors / 75 % 60;
        var f = sectors % 75;
        return $"{m:D2}:{s:D2}:{f:D2}";
    }

    /// <summary>
    /// Returns the sector size used for a track inside a single-file virtual
    /// export (BIN/ISO). Data tracks use the configured sector size (2352 for
    /// BIN, 2048 for ISO); audio tracks inside a BINARY file are always raw
    /// 2352-byte sectors, except in the cooked 2048-byte BIN mode which keeps
    /// everything at 2048 bytes.
    /// </summary>
    private uint VirtualTrackSectorSize(TrackInfo t)
    {
        // Audio tracks inside a BINARY file are raw 2352-byte sectors, but only
        // when the CHD actually stores raw sectors (UnitBytes >= 2352). Cooked
        // 2048-byte-unit CHDs cannot contain audio tracks in practice (chdman
        // only produces them from ISOs), but if one did, its sectors must be
        // read at the unit size rather than 2352.
        if (t.IsDataTrack || _cueMode == CueExportMode.CueBin2048 || UnitBytes < 2352)
            return _cueSectorSize;

        return 2352;
    }

    private int ReadVirtualBin(ulong offset, byte[] buffer, int bufOffset, int bytesToRead,
        bool dataTracksOnly = false)
    {
        if (_cachedTracks == null || _cachedTracks.Count == 0) return 0;

        var reader = AcquireReader();

        try
        {
            var currentOffset = offset;
            var totalRead = 0;

            while (totalRead < bytesToRead)
            {
                ulong cumulative = 0;
                TrackInfo? targetTrack = null;
                ulong trackByteOffset = 0;

                foreach (var t in _cachedTracks)
                {
                    if (dataTracksOnly && !t.IsDataTrack)
                        continue;

                    var trackSectorSize = VirtualTrackSectorSize(t);
                    var trackBytes = (ulong)t.Frames * trackSectorSize;
                    if (currentOffset >= cumulative && currentOffset < cumulative + trackBytes)
                    {
                        targetTrack = t;
                        trackByteOffset = cumulative;
                        break;
                    }

                    cumulative += trackBytes;
                }

                if (targetTrack == null) break;

                reader.SetTrack(targetTrack, true);
                var targetSectorSize = VirtualTrackSectorSize(targetTrack);
                var offsetInTrack = currentOffset - trackByteOffset;
                var frameInTrack = (uint)(offsetInTrack / targetSectorSize);
                var byteInFrame = (uint)(offsetInTrack % targetSectorSize);
                var logicalLba = targetTrack.StartLba + frameInTrack;

                if (reader.ReadRawSector(logicalLba, out var rawSector))
                {
                    var dataOffset = targetSectorSize == 2048
                        ? reader.SectorHeaderOffset
                        : reader.SyncOffset;
                    var available = (int)(targetSectorSize - byteInFrame);
                    var toCopy = Math.Min(available, bytesToRead - totalRead);

                    if (dataOffset + byteInFrame + toCopy <= rawSector.Length)
                        Array.Copy(rawSector, dataOffset + byteInFrame, buffer, bufOffset + totalRead, toCopy);
                    else
                        Array.Clear(buffer, bufOffset + totalRead, toCopy);

                    totalRead += toCopy;
                    currentOffset += (uint)toCopy;
                }
                else
                {
                    break;
                }
            }

            return totalRead;
        }
        finally
        {
            ReleaseReader(reader);
        }
    }

    private int ReadVirtualWav(int trackIndex, ulong offset, byte[] buffer, int bufOffset, int bytesToRead)
    {
        if (_cachedTracks == null || _wavHeaders == null || !_wavHeaders.TryGetValue(trackIndex, out var header))
            return 0;

        if (offset < (ulong)header.Length)
        {
            var headerRead = Math.Min(bytesToRead, header.Length - (int)offset);
            Array.Copy(header, (int)offset, buffer, bufOffset, headerRead);
            return headerRead;
        }

        var track = _cachedTracks.Find(t => t.Index == trackIndex);
        if (track == null) return 0;

        var reader = AcquireReader();

        reader.SetTrack(track, true);

        try
        {
            var pcmOffset = offset - (ulong)header.Length;
            var totalRead = 0;
            const uint audioSectorSize = 2352;

            // The WAV contains only the audible track data: the pregap frames
            // (silence stored at the start of the track's CHD chunk) are skipped
            // so the file begins at the music, matching INDEX 01 00:00:00.
            var musicFrames = track.Frames > track.Pregap ? track.Frames - track.Pregap : 0u;

            while (totalRead < bytesToRead)
            {
                var currentPcmOffset = pcmOffset + (ulong)totalRead;
                var frameInTrack = (uint)(currentPcmOffset / audioSectorSize);
                var byteInFrame = (uint)(currentPcmOffset % audioSectorSize);

                if (frameInTrack >= musicFrames) break;

                var logicalLba = track.StartLba + track.Pregap + frameInTrack;

                if (reader.ReadRawSector(logicalLba, out var rawSector))
                {
                    var available = (int)(audioSectorSize - byteInFrame);
                    var toCopy = Math.Min(available, bytesToRead - totalRead);

                    if (byteInFrame + toCopy <= rawSector.Length)
                        Array.Copy(rawSector, byteInFrame, buffer, bufOffset + totalRead, toCopy);
                    else
                        Array.Clear(buffer, bufOffset + totalRead, toCopy);

                    totalRead += toCopy;
                }
                else
                {
                    break;
                }
            }

            return totalRead;
        }
        finally
        {
            ReleaseReader(reader);
        }
    }

    private int ReadRawChdBytes(ulong offset, byte[] buffer, int bufOffset, int bytesToRead)
    {
        if (_primaryChd == null) return 0;

        var err = _primaryChd.Read(offset, buffer, bufOffset, bytesToRead);
        return err == ChdError.Chderrnone ? bytesToRead : 0;
    }

    private SectorReader AcquireReader()
    {
        lock (_poolLock)
        {
            if (_poolShutdown)
                throw new ObjectDisposedException(nameof(ChdContainer));

            if (_availableReaders.Count > 0)
            {
                var reader = _availableReaders[^1];
                _availableReaders.RemoveAt(_availableReaders.Count - 1);
                return reader;
            }
        }

        if (_primaryChd == null)
            throw new InvalidOperationException("CHD container is not opened.");

        var newReader = new SectorReader(_primaryChd, UnitBytes);
        lock (_poolLock)
        {
            _readerPool.Add(newReader);
        }

        return newReader;
    }

    private void ReleaseReader(SectorReader reader)
    {
        lock (_poolLock)
        {
            _availableReaders.Add(reader);
        }
    }

    /// <summary>
    /// Disposes the container, releasing all readers and the underlying CHD file.
    /// </summary>
    public void Dispose()
    {
        lock (_poolLock)
        {
            _poolShutdown = true;
        }

        foreach (var reader in _readerPool)
            reader.Dispose();
        _readerPool.Clear();
        _availableReaders.Clear();
        _cachedTracks = null;
        _primaryChd?.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously disposes the container, releasing all readers and the underlying CHD file.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
