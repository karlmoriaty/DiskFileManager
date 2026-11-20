namespace DiskManager.Services;

public interface IRecycleBinService
{
    bool Recycle(string path, bool isDirectory);
    bool Empty();
    RecycleBinInfo Query();
}

public readonly record struct RecycleBinInfo(long Size, int ItemCount);