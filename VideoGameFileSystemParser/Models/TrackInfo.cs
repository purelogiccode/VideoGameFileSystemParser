namespace VideoGameFileSystemParser.Models;

/// <summary>
/// Represents metadata for a single disc image track.
/// </summary>
public class TrackInfo
{
    /// <summary>
    /// The one-based track index within the disc.
    /// </summary>
    public int Index { get; set; }
    /// <summary>
    /// The LBA of the first sector of the track.
    /// </summary>
    public uint StartLba { get; set; }
    /// <summary>
    /// The frame offset within the CHD hunk stream.
    /// </summary>
    public uint ChdOffset { get; set; }
    /// <summary>
    /// The number of frames in this track.
    /// </summary>
    public uint Frames { get; set; }
    /// <summary>
    /// The track type string (e.g. MODE1/2352, AUDIO).
    /// </summary>
    public string TrackType { get; set; } = string.Empty;
    /// <summary>
    /// Whether this track contains data (as opposed to audio).
    /// </summary>
    public bool IsDataTrack { get; set; }
    /// <summary>
    /// The number of pregap frames before this track.
    /// </summary>
    public uint Pregap { get; set; }
    /// <summary>
    /// The number of postgap frames after this track.
    /// </summary>
    public uint Postgap { get; set; }
    /// <summary>
    /// The raw metadata string from the CHD for this track.
    /// </summary>
    public string Metadata { get; set; } = string.Empty;
}
