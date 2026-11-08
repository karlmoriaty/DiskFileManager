using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using DiskManager.Models;

namespace DiskManager.Services
{
    public class CleanupService
    {
        private readonly List<string> _systemProtectedPaths = new()
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.System)
        };

        public List<CleanupSuggestion> GetCleanupSuggestions(string driveLetter)
        {
            var suggestions = new List<CleanupSuggestion>();

            suggestions.Add(GetTempFilesSuggestion());
            suggestions.Add(GetRecycleBinSuggestion());
            suggestions.Add(GetBrowserCacheSuggestion());
            suggestions.Add(GetWindowsUpdateCleanupSuggestion());
            suggestions.Add(GetDownloadsSuggestion());

            return suggestions.Where(s => s.Files.Count > 0).OrderByDescending(s => s.TotalSize).ToList();
        }

        private CleanupSuggestion GetTempFilesSuggestion()
        {
            var suggestion = new CleanupSuggestion
            {
                Category = "临时文件",
                Description = "系统和应用程序产生的临时文件，可以安全删除",
                IsSafeToDelete = true
            };

            var tempPaths = new List<string>
            {
                Path.GetTempPath(),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")
            };

            foreach (var tempPath in tempPaths)
            {
                if (Directory.Exists(tempPath))
                {
                    AddFilesFromDirectory(tempPath, suggestion, "*.*", true);
                }
            }

            return suggestion;
        }

        private CleanupSuggestion GetRecycleBinSuggestion()
        {
            var suggestion = new CleanupSuggestion
            {
                Category = "回收站",
                Description = "回收站中的文件，删除后无法恢复",
                IsSafeToDelete = true
            };

            try
            {
                var recyclePath = @"C:\$Recycle.Bin";
                if (Directory.Exists(recyclePath))
                {
                    AddFilesFromDirectory(recyclePath, suggestion, "*.*", true);
                }
            }
            catch (Exception) { }

            return suggestion;
        }

        private CleanupSuggestion GetBrowserCacheSuggestion()
        {
            var suggestion = new CleanupSuggestion
            {
                Category = "浏览器缓存",
                Description = "浏览器缓存文件，可以安全删除",
                IsSafeToDelete = true
            };

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var browserCachePaths = new List<string>
            {
                Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Cache"),
                Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Code Cache"),
                Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Cache"),
                Path.Combine(localAppData, @"Mozilla\Firefox\Profiles")
            };

            foreach (var cachePath in browserCachePaths)
            {
                if (Directory.Exists(cachePath))
                {
                    AddFilesFromDirectory(cachePath, suggestion, "*.*", true);
                }
            }

            return suggestion;
        }

        private CleanupSuggestion GetWindowsUpdateCleanupSuggestion()
        {
            var suggestion = new CleanupSuggestion
            {
                Category = "Windows更新缓存",
                Description = "Windows更新下载的旧文件，可以安全删除",
                IsSafeToDelete = true
            };

            var winSxS = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "WinSxS");
            var softwareDistribution = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"SoftwareDistribution\Download");

            if (Directory.Exists(softwareDistribution))
            {
                AddFilesFromDirectory(softwareDistribution, suggestion, "*.*", true);
            }

            return suggestion;
        }

        private CleanupSuggestion GetDownloadsSuggestion()
        {
            var suggestion = new CleanupSuggestion
            {
                Category = "下载文件夹",
                Description = "下载文件夹中的文件，请谨慎选择删除",
                IsSafeToDelete = false
            };

            var downloadsPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            downloadsPath = Path.Combine(downloadsPath, "Downloads");

            if (Directory.Exists(downloadsPath))
            {
                AddFilesFromDirectory(downloadsPath, suggestion, "*.*", false);
            }

            return suggestion;
        }

        private void AddFilesFromDirectory(string path, CleanupSuggestion suggestion, string searchPattern, bool recursive)
        {
            try
            {
                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var files = Directory.GetFiles(path, searchPattern, searchOption);

                foreach (var file in files)
                {
                    try
                    {
                        if (!IsSystemProtectedPath(file))
                        {
                            var fileInfo = new FileInfo(file);
                            suggestion.Files.Add(new FileItem
                            {
                                FullPath = file,
                                Size = fileInfo.Length,
                                LastModified = fileInfo.LastWriteTime,
                                IsDirectory = false
                            });
                            suggestion.TotalSize += fileInfo.Length;
                        }
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        public bool IsSystemProtectedPath(string path)
        {
            var fullPath = Path.GetFullPath(path);
            return _systemProtectedPaths.Any(p => fullPath.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        }

        public bool DeleteFile(string filePath, bool confirm = true)
        {
            if (IsSystemProtectedPath(filePath))
            {
                return false;
            }

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool DeleteFiles(List<string> filePaths, IProgress<int>? progress = null)
        {
            var success = true;
            var processed = 0;

            foreach (var filePath in filePaths)
            {
                if (!DeleteFile(filePath, false))
                {
                    success = false;
                }
                processed++;
                progress?.Report((int)((double)processed / filePaths.Count * 100));
            }

            return success;
        }
    }
}
