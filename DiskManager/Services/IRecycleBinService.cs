namespace DiskManager.Services;

public interface IRecycleBinService
{
    bool Recycle(string path, bool isDirectory);
    bool Empty(string? driveRoot = null);
    RecycleBinInfo Query(string? driveRoot = null);
}

public readonly record struct RecycleBinInfo(long Size, int ItemCount);