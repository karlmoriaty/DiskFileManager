using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DiskManager.Models
{
    public class FileItem
    {
        public string FullPath { get; set; } = string.Empty;
        public string Name => Path.GetFileName(FullPath);
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string Extension => Path.GetExtension(FullPath).ToLower();
        public bool IsDirectory { get; set; }
        public List<FileItem> Children { get; set; } = new List<FileItem>();
        public bool IsSelected { get; set; }
        public string SizeFormatted => FormatSize(Size);

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
