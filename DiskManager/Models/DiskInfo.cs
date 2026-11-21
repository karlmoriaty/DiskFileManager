using DiskManager.Utils;

namespace DiskManager.Models;

public class DiskInfo
{
    public string DriveLetter { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FileSystem { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public long UsedSpace { get; set; }
    public long FreeSpace { get; set; }
    public double UsedPercentage => TotalSize > 0 ? (double)UsedSpace / TotalSize * 100 : 0;
    public string TotalSizeFormatted => SizeFormatter.Format(TotalSize);
    public string UsedSpaceFormatted => SizeFormatter.Format(UsedSpace);
    public string FreeSpaceFormatted => SizeFormatter.Format(FreeSpace);
}