using System.IO;

namespace DiskManager.Services;

public static class ScanOptions
{
    public static EnumerationOptions Create(bool recursive = false) => new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = recursive,
        AttributesToSkip = FileAttributes.ReparsePoint,
        ReturnSpecialDirectories = false
    };
}