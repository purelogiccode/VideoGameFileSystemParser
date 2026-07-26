namespace VideoGameFileSystemParser.Interfaces;

/// <summary>
/// Defines the contract for a console-specific file system parser.
/// </summary>
public interface IConsoleParser
{
    /// <summary>
    /// Returns the ConsoleType that this parser handles.
    /// </summary>
    /// <returns>The console type handled by this parser.</returns>
    ConsoleType GetConsoleType();

    /// <summary>
    /// Returns the human-readable console name that this parser handles.
    /// </summary>
    /// <returns>The display name of the console.</returns>
    string GetConsoleName();

    /// <summary>
    /// Parses the file system from the reader's tracks and populates the given root node.
    /// </summary>
    /// <returns>true if parsing succeeded.</returns>
    bool Parse(FsNode rootNode);

    /// <summary>
    /// Parses the file system from a specific track.
    /// </summary>
    /// <returns>true if parsing succeeded.</returns>
    bool ParseTrack(FsNode rootNode, TrackInfo track);

    /// <summary>
    /// Gets or sets a value indicating whether to force parsing even when verification fails.
    /// </summary>
    bool ForceMode { get; set; }
}
