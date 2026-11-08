using System;
using System.Collections.Generic;

namespace DiskManager.Models
{
    public class FileCategory
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Extensions { get; set; } = new List<string>();
        public long TotalSize { get; set; }
        public int FileCount { get; set; }
        public string TotalSizeFormatted => FormatSize(TotalSize);

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
