# Contributing

Thank you for considering contributing to **VideoGameFileSystemParser**! This page covers the repository layout, how to build, test, and submit changes.

---

## Repository layout

```
CSharp_VideoGameFileSystemParser/
├── VideoGameFileSystemParser/        # The library project
│   ├── Interfaces/                   # IConsoleParser contract
│   ├── Models/                       # ConsoleType, FileEntry, FsNode, TrackInfo,
│   │                                 # ConsoleTypeRegistry, ...
│   └── Parsers/
│       ├── ChdContainer.cs           # High-level facade
│       ├── SectorReader.cs           # Sector pipeline (caching, descrambling)
│       ├── ParserFactory.cs          # ConsoleType → parser dispatch
│       ├── Iso9660Parser.cs, UdfParser.cs, XdvdfsParser.cs,
│       ├── HfsParser.cs, ThreeDoParser.cs, CDiFsParser.cs, ...
│       └── Systems/                  # Console-specific wrappers
├── docs/                             # This wiki (multi-page documentation)
├── Directory.Build.props             # Shared build settings + target frameworks
├── README.md                         # Landing page (mirrors docs/Home.md)
├── WhatsNew.md                       # Release history (ships in the NuGet package)
└── CSharp_VideoGameFileSystemParser.sln
```

## Prerequisites

- .NET SDK **8.0 or newer** (the library multi-targets `net8.0;net9.0;net10.0`).
- Any IDE (Visual Studio, Rider, VS Code) or the CLI.

## Building

```bash
# Build Debug
dotnet build CSharp_VideoGameFileSystemParser.sln

# Build Release (all target frameworks)
dotnet build VideoGameFileSystemParser/VideoGameFileSystemParser.csproj -c Release

# Create the NuGet package
dotnet pack VideoGameFileSystemParser/VideoGameFileSystemParser.csproj -c Release -o bin/Release
```

The build produces `VideoGameFileSystemParser.<version>.nupkg` and `.snupkg`.

## Testing

The library's automated test suite lives in the companion repository — [**CHDMounter**](https://github.com/drpetersonfernandes/CHDMounter), the Windows application where this library is battle-tested in production:

- **Unit tests:** [`CHDMounter.Core.Tests`](https://github.com/drpetersonfernandes/CHDMounter/tree/master/CHDMounter.Core.Tests) — parser tests (including 18 per-console integration tests such as `AmigaCdIntegrationTests`), `ConsoleTypeRegistry` tests, `FileNameMatcher` tests, and more.
- **Integration:** mounting real CHDs as virtual drives exercises every parser and virtual export mode end-to-end.

When contributing, use CHDMounter and its tests to validate your changes against real images. Manual verification checklist for changes:

1. Build succeeds for all three target frameworks with no new warnings.
2. `MountAndParse` still returns `true` for representative images of the affected console(s).
3. Virtual export modes produce byte-correct CUE/BIN/WAV (verify with a CUE parser or an emulator).
4. Package validation passes (`EnablePackageValidation` is on — it runs API-compat checks during pack).

## Code style

- **`var` everywhere** — the codebase standardizes on implicit typing (see the v1.2.0 code-modernization pass).
- **XML doc comments** on all public members; `CS1591` is suppressed but documentation is expected for new public API.
- **File-scoped namespaces**, modern C# (latest language version).
- **Never break the public API silently** — adding members is fine; renaming/removing requires a major/minor version bump and a [Migration Guide](Migration-Guide.md) entry.
- Keep `ConsoleTypeRegistry` as the single source of truth when adding a new console type: add the `ConsoleType` member, a `ConsoleTypeInfo` entry with aliases, and a `ParserFactory` mapping.

## Adding a new console

1. Add the `ConsoleType` enum member in `Models/ConsoleType.cs` (with doc comment).
2. Add a `ConsoleTypeInfo` entry (display name + CLI aliases) in `ConsoleTypeRegistry`.
3. Implement `IConsoleParser` (or reuse an existing parser with a wrapper) and register it in `ParserFactory.CreateParser`.
4. Document the console in `docs/Supported-Consoles.md` (reference table + parsing logic).
5. Update `README.md` and `WhatsNew.md` release notes.

## Documentation

The wiki lives in `docs/` and is the source of truth for user documentation:

- **Multi-page structure** — one topic per page (`Home`, `Getting-Started`, `Usage-Guide`, `Supported-Consoles`, `Virtual-Exports`, `API-Reference`, `Architecture`, `Migration-Guide`, `FAQ`, `Contributing`).
- **API signatures must match the code** — when the public API changes, update `docs/API-Reference.md` in the same commit.
- Keep cross-page links relative (they work both in the repo and on GitHub).

## Submitting changes

1. Fork the repository and create a feature branch.
2. Make focused commits with descriptive messages.
3. Update docs (README, docs/wiki, WhatsNew.md) alongside code changes.
4. Open a pull request describing the change, motivation, and verification performed.

## Release process (maintainers)

1. Bump `<Version>`, `<AssemblyVersion>`, `<FileVersion>` in `VideoGameFileSystemParser.csproj`.
2. Update `PackageReleaseNotes`, `README.md` badge/release notes, and add a `WhatsNew.md` entry.
3. `dotnet pack -c Release` and validate the nupkg contents.
4. Push the tag, publish to [nuget.org](https://www.nuget.org/packages/VideoGameFileSystemParser), and update the GitHub release.

---

*Previous: [FAQ](FAQ.md) · [Back to Home](Home.md)*
