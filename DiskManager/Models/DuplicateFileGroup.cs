using System;
using System.Collections.Generic;

namespace DiskManager.Models
{
    public class DuplicateFileGroup
    {
        public string FileHash { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public List<FileItem> Files { get; set; } = new List<FileItem>();
        public long TotalWastedSpace => FileSize * (Files.Count - 1);
        public string Name => Files.Count > 0 ? Files[0].Name : "未知文件";
        public string FileSizeFormatted => FormatSize(FileSize);
        public string TotalWastedSpaceFormatted => FormatSize(TotalWastedSpace);

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
