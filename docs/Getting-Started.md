# Getting Started

This guide walks you through installing the library, creating a first project, and running your first parse.

## Prerequisites

- **.NET SDK 8.0 or newer** (the package ships `net8.0`, `net9.0`, and `net10.0` assemblies).
- A disc image to test with — a **CHD** file is the primary supported format. ISO and raw sector images are supported through the same API.

## Installation

### .NET CLI

```bash
dotnet add package VideoGameFileSystemParser
```

### Package Manager Console

```powershell
Install-Package VideoGameFileSystemParser
```

### Direct .csproj reference

```xml
<PackageReference Include="VideoGameFileSystemParser" Version="1.2.0" />
```

> **Note:** if you migrate from an older version, review the [Migration Guide](Migration-Guide.md) — v1.2.0 renamed several `ConsoleType` values.

### What gets installed

The package contains:

- `lib/net8.0/` — .NET 8 assembly
- `lib/net9.0/` — .NET 9 assembly
- `lib/net10.0/` — .NET 10 assembly
- XML documentation files for IntelliSense
- `README.md`, `WhatsNew.md`, `LICENSE.txt`, package icon

The only runtime dependency is **CHDSharp 1.2.0** (MAME CHD format reader), resolved automatically by NuGet.

## First project

Create a console application and add the package:

```bash
dotnet new console -n DiscReader
cd DiscReader
dotnet add package VideoGameFileSystemParser
```

Replace `Program.cs` with:

```csharp
using VideoGameFileSystemParser.Models;
using VideoGameFileSystemParser.Parsers;

using var container = new ChdContainer(@"C:\ROMs\my_game.chd");

if (!container.MountAndParse(ConsoleType.Ps1))
{
    Console.WriteLine("Failed to parse disc image.");
    return 1;
}

Console.WriteLine($"Volume:      {container.VolumeName}");
Console.WriteLine($"Total size:  {container.VolumeSize:N0} bytes");
Console.WriteLine($"Sector size: {container.UnitBytes} bytes");
Console.WriteLine($"Hunk size:   {container.HunkBytes} bytes");
Console.WriteLine($"Data tracks: {container.HasDataTracks}");
Console.WriteLine();
Console.WriteLine("Disc contents:");
foreach (var entry in container.Entries)
{
    var indent = new string(' ', entry.FullPath.Count(c => c == '\\') * 2);
    Console.WriteLine($"{indent}{(entry.IsDirectory ? "[DIR] " : "[FILE]")} {entry.Name}");
}
return 0;
```

Run it:

```bash
dotnet run
```

You should see the volume name, disc metadata, and the full file tree of the image.

## Your next steps

1. **Read a file** — see [Usage Guide → Reading files](Usage-Guide.md#reading-files).
2. **Auto-detect the console** — see [Usage Guide → Console type selection](Usage-Guide.md#console-type-selection).
3. **Export a CUE/BIN** — see [Virtual CUE/BIN/ISO/WAV Exports](Virtual-Exports.md).
4. **Which console types exist?** — see [Supported Consoles](Supported-Consoles.md).
5. **Full API details** — see [API Reference](API-Reference.md).
6. **See the library in action** — [CHDMounter](https://github.com/drpetersonfernandes/CHDMounter) is the companion application that mounts CHDs as virtual drives and hosts the library's unit tests. It's the best way to try the library hands-on.

## Troubleshooting install issues

| Problem | Fix |
|---|---|
| `NU1100` unable to resolve | Check your NuGet sources include nuget.org (`dotnet nuget list source`). |
| Restore fails with CHDSharp error | The package targets `net8.0`+; check your project's `TargetFramework`. |
| No parse result | The image may be a format the chosen `ConsoleType` cannot parse — try other types or raw mode (see [FAQ](FAQ.md)). |

---

*Next: [Usage Guide](Usage-Guide.md) · [Back to Home](Home.md)*
