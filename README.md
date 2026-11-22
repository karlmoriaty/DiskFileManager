# Disk File Manager

A portable Windows disk and file manager built with C#/.NET 9 and WPF. It surveys
drives, finds large and duplicate files, suggests safe cleanups, and performs batch
file operations — from a single self-contained executable that runs off a USB stick
with no .NET runtime installed.

Version 2.4 focuses on the interface: a warm editorial theme, an explorer-style
layout and a dense disk overview with objective trend figures. Safety and usability
carry over from 2.1: deletion goes to the recycle bin by default, protected system
locations are enforced by path boundaries, scans can be filtered and cancelled, and
results can be sorted, searched and exported.

## Features

- **Disk overview** — capacity, used space, free space and usage percentage for fixed
  and removable drives.
- **Large file scan** — top-N files by size, computed with a bounded heap, with a live
  scanned-file counter and cancellation.
- **Duplicate detection** — three-stage pipeline: group by size, pre-filter with a
  4 KB head/tail SHA-256 sample, then confirm with a full SHA-256 hash. Groups can
  keep the shortest path, the newest copy, or the oldest copy.
- **Cleanup suggestions** — temporary files (user and Windows), recycle bin, browser
  caches (Chrome, Edge, Firefox cache directories, INetCache), Windows Update download
  cache and Downloads.
- **Batch operations** — copy, move and delete files or whole directories, with
  overwrite/skip/rename conflict policies and a cancellable progress bar.
- **Scan filters** — system and build directories (`Windows`, `Program Files`,
  `node_modules`, `.git`, `obj`, `bin`) are excluded by default; extra names, paths and
  extensions are configurable and persisted.
- **Sorting, search and export** — large-file and file-operation lists sort from the
  column headers, filter as you type, and export to UTF-8 CSV.
- **Operation log** — every action is recorded with its timestamp and result; the log
  file rotates daily and the in-memory buffer is capped.

## Safety model

- Deletion defaults to **recycle bin**. Permanent deletion is an explicit opt-in in the
  status bar.
- Protected locations: `Windows`, `Program Files`, `Program Files (x86)`, `ProgramData`,
  `System32`, `SysWOW64`, `WinSxS`, `Boot`, `Recovery`, every drive root, the user
  profile root, `System Volume Information` and `$Recycle.Bin`.
- Matching uses full path boundaries, so `C:\Windows10Backup` is **not** treated as
  `C:\Windows`.
- Explicit exceptions for `Windows\Temp`, `SoftwareDistribution\Download` and
  `Windows\Logs\CBS` (strict children only).
- Firefox profile data (bookmarks, passwords, logins) is never touched; only
  `cache2`, `startupCache`, `shader-cache` and `thumbnails` are scanned.
- Reparse points (junctions/symlinks) are skipped during enumeration to avoid loops
  and duplicate scans.

## Download

Prebuilt portable builds are attached to GitHub Releases:

<https://github.com/karlmoriaty/DiskFileManager/releases>

Each release ships `DiskManager_Portable.zip` and a SHA-256 checksum. Unzip anywhere
and run `启动程序.bat`.

## Requirements

- Windows 10 (1607 or newer) or Windows 11, x64
- No installation and no .NET runtime needed — the published build is
  self-contained and single-file
- Roughly 100–150 MB of disk space
- Administrator rights are recommended for system locations; the manifest runs
  as the invoking user (`asInvoker`), so start the app elevated when needed

## Running the prebuilt portable build

1. Copy the whole `DiskManager_Portable` folder to a USB stick or anywhere on the
   target machine.
2. Double-click `启动程序.bat` (or `DiskManager.exe` directly).

The first start takes a few seconds while the single-file bundle unpacks itself.

## Building from source

```powershell
cd DiskManager
dotnet build -c Release
dotnet test ..\DiskManager.Tests\DiskManager.Tests.csproj -c Release
```

## Publishing a portable build

```powershell
cd DiskManager
dotnet publish -c Release -r win-x64 --self-contained true -o ..\DiskManager_Portable `
  /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:PublishReadyToRun=true

pwsh .\publish-win-x64.ps1
```

`PublishTrimmed` is intentionally **not** used: trimming is not supported for WPF
applications and can produce an executable that crashes at runtime.

## Notes

- Some antivirus engines flag freshly built single-file .NET executables as
  suspicious; add an exclusion if you trust the source.
- The app is single-instance; launching a second copy shows a message and exits.
- Only one long-running task can run at a time; the cancel button stops it.
- The UI is in Chinese.
- WPF only runs on Windows; macOS and Linux are not supported.

## License

MIT