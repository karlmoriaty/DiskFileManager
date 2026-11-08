using System;

namespace DiskManager.Models
{
    public class DiskInfo
    {
        public string DriveLetter { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public long UsedSpace { get; set; }
        public long FreeSpace { get; set; }
        public double UsedPercentage => TotalSize > 0 ? (double)UsedSpace / TotalSize * 100 : 0;
        public string TotalSizeFormatted => FormatSize(TotalSize);
        public string UsedSpaceFormatted => FormatSize(UsedSpace);
        public string FreeSpaceFormatted => FormatSize(FreeSpace);

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }
    }
}
