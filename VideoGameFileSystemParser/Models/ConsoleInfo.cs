namespace VideoGameFileSystemParser.Models;

/// <summary>
/// Associates a <see cref="ConsoleType"/> with its human-readable display name.
/// </summary>
/// <param name="Type">The console type identifier.</param>
/// <param name="Name">The human-readable name of the console (e.g., "PlayStation 2", "Xbox").</param>
public sealed record ConsoleInfo(ConsoleType Type, string Name);
