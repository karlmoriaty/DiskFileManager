using System.IO;

namespace DiskManager.Services;

public static class ScanOptions
{
    public static EnumerationOptions Create(bool recursive = false) => new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = recursive,
        AttributesToSkip = 0,
        ReturnSpecialDirectories = false
    };
}