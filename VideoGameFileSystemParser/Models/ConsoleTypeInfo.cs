namespace VideoGameFileSystemParser.Models;

/// <summary>
/// Associates a <see cref="ConsoleType"/> with its display name and CLI aliases.
/// </summary>
/// <param name="Type">The console type identifier.</param>
/// <param name="DisplayName">The human-readable name shown in the UI (e.g., "CUE/BIN RAW 2352 (Default)").</param>
/// <param name="Aliases">The command-line aliases that resolve to this console type (case-insensitive).</param>
public sealed record ConsoleTypeInfo(ConsoleType Type, string DisplayName, IReadOnlyList<string> Aliases);
