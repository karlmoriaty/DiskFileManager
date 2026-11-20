namespace DiskManager.Models;

public enum DeleteMode
{
    RecycleBin = 0,
    Permanent = 1
}

public enum ConflictPolicy
{
    Overwrite = 0,
    Skip = 1,
    Rename = 2
}

public enum DuplicateKeepStrategy
{
    ShortestPath = 0,
    Newest = 1,
    Oldest = 2
}

public enum CleanupKind
{
    TempFiles = 0,
    RecycleBin = 1,
    BrowserCache = 2,
    WindowsUpdate = 3,
    Downloads = 4
}