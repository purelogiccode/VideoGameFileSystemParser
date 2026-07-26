namespace VideoGameFileSystemParser.Models;

/// <summary>
/// Identifies the type of a file system node.
/// </summary>
public enum FsNodeType
{
    /// <summary>A regular file.</summary>
    File = 0,
    /// <summary>A directory.</summary>
    Directory = 4,
    /// <summary>A symbolic link.</summary>
    Symlink = 12
}

/// <summary>
/// Represents a node in the parsed file system tree.
/// </summary>
public class FsNode
{
    /// <summary>
    /// The file or directory name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// The LBA of the first extent.
    /// </summary>
    public uint Lba { get; set; }
    /// <summary>
    /// The total data size in bytes.
    /// </summary>
    public ulong Size { get; set; }
    /// <summary>
    /// The file number for interleaved (XA) access.
    /// </summary>
    public byte FileNumber { get; set; }
    /// <summary>
    /// Whether data is interleaved.
    /// </summary>
    public bool IsInterleaved { get; set; }
    /// <summary>
    /// Whether this node is a directory.
    /// </summary>
    public bool IsDirectory { get; set; }
    /// <summary>
    /// Whether this node spans multiple extents.
    /// </summary>
    public bool IsMultiExtent { get; set; }
    /// <summary>
    /// Whether to read data as raw bytes.
    /// </summary>
    public bool IsRawPassthrough { get; set; }
    /// <summary>
    /// Whether data is embedded within a file entry sector.
    /// </summary>
    public bool IsEmbedded { get; set; }
    /// <summary>
    /// The byte offset within the sector for embedded data.
    /// </summary>
    public uint EmbeddedOffset { get; set; }
    /// <summary>
    /// The last modification timestamp, if available from the file system.
    /// </summary>
    public DateTime? ModifiedTime { get; set; }

    /// <summary>
    /// The creation timestamp, if available from the file system.
    /// </summary>
    public DateTime? CreatedTime { get; set; }

    /// <summary>
    /// The last access timestamp, if available from the file system.
    /// </summary>
    public DateTime? AccessedTime { get; set; }

    /// <summary>
    /// The POSIX file mode bits (permissions and type), if available.
    /// </summary>
    public uint? UnixMode { get; set; }

    /// <summary>
    /// The POSIX user ID of the file owner, if available.
    /// </summary>
    public uint? Uid { get; set; }

    /// <summary>
    /// The POSIX group ID of the file owner, if available.
    /// </summary>
    public uint? Gid { get; set; }

    /// <summary>
    /// The inode number, if available from the file system.
    /// </summary>
    public uint? Inode { get; set; }

    /// <summary>
    /// The number of hard links to this node, if available.
    /// </summary>
    public uint? LinkCount { get; set; }
    /// <summary>
    /// The type of this node (file, directory, or symlink).
    /// </summary>
    public FsNodeType NodeType { get; set; } = FsNodeType.File;
    /// <summary>
    /// The target path of the symbolic link, if this node is a symlink.
    /// </summary>
    public string? SymlinkTarget { get; set; }

    /// <summary>
    /// The list of contiguous data extents that make up this node's data.
    /// </summary>
    public List<FsExtent> Extents { get; set; } = [];

    /// <summary>
    /// The child nodes of this directory node.
    /// </summary>
    public List<FsNode> Children { get; set; } = [];
}

/// <summary>
/// Represents a contiguous data extent with a starting LBA and size.
/// </summary>
public struct FsExtent
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
