using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiskManager.Models;

namespace DiskManager.Services
{
    public class DiskAnalysisService
    {
        public event EventHandler<ProgressEventArgs>? ProgressChanged;

        public List<DiskInfo> GetAllDisks()
        {
            var disks = new List<DiskInfo>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                {
                    disks.Add(new DiskInfo
                    {
                        DriveLetter = drive.Name,
                        Name = drive.VolumeLabel,
                        TotalSize = drive.TotalSize,
                        UsedSpace = drive.TotalSize - drive.AvailableFreeSpace,
                        FreeSpace = drive.AvailableFreeSpace
                    });
                }
            }
            return disks;
        }

        public async Task<FileItem?> ScanDirectoryAsync(string path, IProgress<int>? progress = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var directoryInfo = new DirectoryInfo(path);
                    var rootItem = new FileItem
                    {
                        FullPath = path,
                        IsDirectory = true,
                        LastModified = directoryInfo.LastWriteTime
                    };

                    var totalFiles = 0;
                    var processedFiles = 0;

                    CountFiles(path, ref totalFiles);
                    ScanDirectoryRecursive(path, rootItem, ref processedFiles, totalFiles, progress);

                    return rootItem;
                }
                catch (UnauthorizedAccessException)
                {
                    return null;
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        private void CountFiles(string path, ref int count)
        {
            try
            {
                count += Directory.GetFiles(path).Length;
                foreach (var dir in Directory.GetDirectories(path))
                {
                    try
                    {
                        CountFiles(dir, ref count);
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        private void ScanDirectoryRecursive(string path, FileItem parent, ref int processedFiles, int totalFiles, IProgress<int>? progress)
        {
            try
            {
                foreach (var file in Directory.GetFiles(path))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        parent.Children.Add(new FileItem
                        {
                            FullPath = file,
                            Size = fileInfo.Length,
                            LastModified = fileInfo.LastWriteTime,
                            IsDirectory = false
                        });
                        parent.Size += fileInfo.Length;
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }

                    processedFiles++;
                    if (totalFiles > 0)
                    {
                        progress?.Report((int)((double)processedFiles / totalFiles * 100));
                    }
                }

                foreach (var dir in Directory.GetDirectories(path))
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(dir);
                        var dirItem = new FileItem
                        {
                            FullPath = dir,
                            IsDirectory = true,
                            LastModified = dirInfo.LastWriteTime
                        };
                        ScanDirectoryRecursive(dir, dirItem, ref processedFiles, totalFiles, progress);
                        parent.Children.Add(dirItem);
                        parent.Size += dirItem.Size;
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        public List<FileItem> FindLargeFiles(string path, long minSizeBytes, int maxResults = 100)
        {
            var largeFiles = new List<FileItem>();
            FindLargeFilesRecursive(path, minSizeBytes, largeFiles);
            return largeFiles.OrderByDescending(f => f.Size).Take(maxResults).ToList();
        }

        private void FindLargeFilesRecursive(string path, long minSizeBytes, List<FileItem> results)
        {
            try
            {
                foreach (var file in Directory.GetFiles(path))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.Length >= minSizeBytes)
                        {
                            results.Add(new FileItem
                            {
                                FullPath = file,
                                Size = fileInfo.Length,
                                LastModified = fileInfo.LastWriteTime,
                                IsDirectory = false
                            });
                        }
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }

                foreach (var dir in Directory.GetDirectories(path))
                {
                    try
                    {
                        FindLargeFilesRecursive(dir, minSizeBytes, results);
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        public List<FileCategory> GetFileCategories(string path)
        {
            var categories = new List<FileCategory>
            {
                new FileCategory { Name = "文档", Extensions = new List<string> { ".doc", ".docx", ".pdf", ".txt", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".rtf" } },
                new FileCategory { Name = "图片", Extensions = new List<string> { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".svg", ".webp", ".ico" } },
                new FileCategory { Name = "视频", Extensions = new List<string> { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v" } },
                new FileCategory { Name = "音频", Extensions = new List<string> { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a" } },
                new FileCategory { Name = "压缩包", Extensions = new List<string> { ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2" } },
                new FileCategory { Name = "程序", Extensions = new List<string> { ".exe", ".msi", ".dll", ".app" } },
                new FileCategory { Name = "代码", Extensions = new List<string> { ".cs", ".java", ".py", ".js", ".html", ".css", ".cpp", ".c", ".h", ".php", ".rb", ".go", ".rs" } },
                new FileCategory { Name = "其他", Extensions = new List<string>() }
            };

            CalculateCategorySizes(path, categories);
            return categories.Where(c => c.FileCount > 0).OrderByDescending(c => c.TotalSize).ToList();
        }

        private void CalculateCategorySizes(string path, List<FileCategory> categories)
        {
            try
            {
                foreach (var file in Directory.GetFiles(path))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        var extension = Path.GetExtension(file).ToLower();
                        var category = categories.FirstOrDefault(c => c.Extensions.Contains(extension)) 
                            ?? categories.Last();
                        category.TotalSize += fileInfo.Length;
                        category.FileCount++;
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }

                foreach (var dir in Directory.GetDirectories(path))
                {
                    try
                    {
                        CalculateCategorySizes(dir, categories);
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }
    }

    public class ProgressEventArgs : EventArgs
    {
        public int Progress { get; set; }
        public string? Message { get; set; }
    }
}
