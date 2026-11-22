# Changelog

## v2.4.4 (2026-11-22)

### Added
- About dialog with version, runtime, privilege level, log-folder shortcut and repository link.
- Recent target folders (five most recent) reused for copy and move operations.
- File-system column and a five-cell metrics strip (disks, capacity, used, free, peak usage) on the disk overview.
- Compact operation log with neutral status dots.

### Changed
- Warm editorial theme with a warm ivory surface, grey navigation rail and ink text.
- Explorer-style layout: 200 px navigation rail, full-width lists, 32 px rows and collapsible detail panel.
- Unified typography: Microsoft YaHei UI with ClearType, 13 px body, 12 px table headers and monospaced log timestamps.
- Disk overview rebuilt as a dense metrics view with 3 px usage tracks, right-aligned percentages and objective trend figures (7-day change, days to full).
- Numeric columns right-aligned with monospaced figures.

### Fixed
- Startup stall on slow removable drives: drive enumeration moved out of `PathGuard` and the disk list refreshes in the background.
- Duplicate deletion now shows a per-group preview (kept path and delete count) before running.
- Permanent deletes of 1 GB or more require a second confirmation.
- Batch operations surface permission failures and show an elevation hint.
- Duplicate detection keeps only size-collision candidates in memory via a two-pass size census.
- Log retention prunes files older than 30 days on startup.
- Window layout ignores off-screen coordinates when restoring.

## v2.1.0 (2026-11-21)

### Added
- Scan filters: default exclusions for system and build directories, custom directory names or paths, and per-extension exclusions, applied to large-file and duplicate scans.
- Column-header sorting and text search for the large-file and file-operation lists.
- Portable release pipeline: pushing a `v*` tag builds a self-contained single-file zip and a SHA-256 checksum.
- Application icon and version label.

### Changed
- Window layout, active tab, scan paths, cleanup drive, filter settings and mode choices persist between sessions.
- Folder sizes are calculated in the background.
- Selection summaries show total and selected counts and sizes.

### Fixed
- Moving a directory into itself deleted the source along with the fresh copy.
- Recycle-bin fallback could delete permanently without a warning.
- Read-only files and trees failed permanent deletion.
- Duplicate scanning rehydrated OneDrive placeholders.
- Delete progress stayed at 0%.
- The duplicate TreeView had no virtualization.
- Single-instance exit released a mutex it did not own.
- Cancellation-source disposal raced with window shutdown.

## v2.0.0

Initial public overhaul: recycle-bin-first deletion, a boundary-aware protected path guard, thread-safe logging and history, directory-aware file operations, sampling-based duplicate detection, MVVM, 51 tests and Windows CI.