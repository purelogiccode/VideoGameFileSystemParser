namespace VideoGameFileSystemParser.Models;

/// <summary>
/// Represents a file or directory entry in the virtual file system.
/// </summary>
public class FileEntry
{
    /// <summary>
    /// The file or directory name (without path).
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// The full path of the entry with leading backslash.
    /// </summary>
    public string FullPath { get; set; } = string.Empty;
    /// <summary>
    /// The logical block address of the beginning of the entry.
    /// </summary>
    public uint Lba { get; set; }
    /// <summary>
    /// The total size in bytes.
    /// </summary>
    public ulong Size { get; set; }
    /// <summary>
    /// The byte offset within the sector for embedded data.
    /// </summary>
    public uint Offset { get; set; }
    /// <summary>
    /// Whether this entry is a directory.
    /// </summary>
    public bool IsDirectory { get; set; }
    /// <summary>
    /// The last modified date and time.
    /// </summary>
    public DateTime ModifiedTime { get; set; } = DateTime.MinValue;
    /// <summary>
    /// The file number used for interleaved (XA) data access.
    /// </summary>
    public byte FileNumber { get; set; }
    /// <summary>
    /// Whether the data is interleaved across multiple files.
    /// </summary>
    public bool IsInterleaved { get; set; }
    /// <summary>
    /// Whether to read as raw bytes directly from the CHD.
    /// </summary>
    public bool IsRawPassthrough { get; set; }
    /// <summary>
    /// Whether the data is embedded within a file entry sector.
    /// </summary>
    public bool IsEmbedded { get; set; }
    /// <summary>
    /// The list of contiguous data extents that make up this file's data.
    /// </summary>
    public List<FileExtent> Extents { get; set; } = [];
}

/// <summary>
/// Represents a contiguous data extent with a starting LBA and size.
/// </summary>
public struct FileExtent
{
    /// <summary>
    /// The starting logical block address.
    /// </summary>
    public uint Lba { get; set; }
    /// <summary>
    /// The size in bytes.
    /// </summary>
    public ulong Size { get; set; }
}
