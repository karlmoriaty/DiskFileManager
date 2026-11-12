# Disk File Manager

A portable Windows disk and file manager built with C#/.NET 8 and WPF. It surveys
drives, finds large and duplicate files, suggests safe cleanups, and performs batch
file operations — from a single self-contained executable that runs off a USB stick
with no .NET runtime installed.

## Features

- **Disk overview** — capacity, used space, free space and usage percentage per
  drive.
- **Large file scan** — lists the files taking up the most space, with path, size
  and last-modified time.
- **Duplicate detection** — groups identical files and can delete duplicates while
  keeping one copy.
- **Cleanup suggestions** — flags temporary files and caches that are safe to
  remove, with a suggested action per item.
- **Batch operations** — add files or whole folders to a work list, then copy, move
  or delete them in one go.
- **Operation log** — every action is recorded with its timestamp and result.

## Requirements

- Windows 10 (1607 or newer) or Windows 11, x64
- No installation and no .NET runtime needed — the published build is
  self-contained and single-file
- Roughly 100–150 MB of disk space

## Running the prebuilt portable build

1. Copy the whole `DiskManager_Portable` folder to a USB stick or anywhere on the
   target machine.
2. Double-click `启动程序.bat` (or `DiskManager.exe` directly).

The first start takes a few seconds while the single-file bundle unpacks itself.

## Building from source

```powershell
cd DiskManager
dotnet publish --configuration Release --runtime win-x64 --self-contained true `
  --output ..\DiskManager_Portable /p:PublishSingleFile=true `
  /p:PublishTrimmed=true /p:TrimMode=link /p:DebuggerSupport=false `
  /p:PublishReadyToRun=true
```

Copy `启动程序.bat`, `启动程序.ps1` and the usage notes into
`DiskManager_Portable` afterwards.

## Notes

- Some antivirus engines flag freshly built single-file .NET executables as
  suspicious; add an exclusion if you trust the source.
- Deletion is permanent — the app confirms before removing files, but there is no
  recycle-bin step for every operation, so double-check the selection.
- System files are protected and skipped.
- The UI is in Chinese.
- WPF only runs on Windows; macOS and Linux are not supported.

## License

MIT
