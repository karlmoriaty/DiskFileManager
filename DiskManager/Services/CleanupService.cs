using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiskManager.Models;

namespace DiskManager.Services;

public class CleanupService
{
    private readonly PathGuard _guard;
    private readonly IRecycleBinService _recycleBin;

    public CleanupService(PathGuard guard, IRecycleBinService recycleBin)
    {
        _guard = guard;
        _recycleBin = recycleBin;
    }

    public Task<List<CleanupSuggestion>> GetCleanupSuggestionsAsync(
        string? driveLetter = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.Run(() => GetCleanupSuggestions(driveLetter, progress, cancellationToken), cancellationToken);

    public List<CleanupSuggestion> GetCleanupSuggestions(
        string? driveLetter = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var suggestions = new List<CleanupSuggestion>();
        var throttled = new ThrottledProgress(progress);

        cancellationToken.ThrowIfCancellationRequested();
        suggestions.AddRange(BuildTempSuggestions(cancellationToken));
        throttled.Report(20, true);

        cancellationToken.ThrowIfCancellationRequested();
        suggestions.Add(BuildRecycleBinSuggestion(driveLetter));
        throttled.Report(40, true);

        cancellationToken.ThrowIfCancellationRequested();
        suggestions.Add(BuildBrowserCacheSuggestion(cancellationToken));
        throttled.Report(60, true);

        cancellationToken.ThrowIfCancellationRequested();
        suggestions.Add(BuildWindowsUpdateSuggestion(cancellationToken));
        throttled.Report(80, true);

        cancellationToken.ThrowIfCancellationRequested();
        suggestions.Add(BuildDownloadsSuggestion(cancellationToken));
        throttled.Report(100, true);

        return suggestions
            .Where(s => s.HasItems)
            .OrderByDescending(s => s.TotalSize)
            .ToList();
    }

    private IEnumerable<CleanupSuggestion> BuildTempSuggestions(CancellationToken cancellationToken)
    {
        var userTemp = new CleanupSuggestion
        {
            Kind = CleanupKind.TempFiles,
            Category = "用户临时文件",
            Description = "当前用户产生的临时文件，可以安全删除",
            IsSafeToDelete = true
        };
        AddDirectoryFiles(Path.GetTempPath(), userTemp, true, cancellationToken);
        yield return userTemp;

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrWhiteSpace(windows))
        {
            yield break;
        }

        var windowsTemp = new CleanupSuggestion
        {
            Kind = CleanupKind.TempFiles,
            Category = "Windows 临时文件",
            Description = "系统临时文件，部分文件可能被占用，需要管理员权限",
            IsSafeToDelete = true
        };
        AddDirectoryFiles(Path.Combine(windows, "Temp"), windowsTemp, true, cancellationToken);
        yield return windowsTemp;
    }

    private CleanupSuggestion BuildRecycleBinSuggestion(string? driveLetter)
    {
        var driveRoot = NormalizeDriveRoot(driveLetter);
        var info = _recycleBin.Query(driveRoot);
        return new CleanupSuggestion
        {
            Kind = CleanupKind.RecycleBin,
            Category = driveRoot == null ? "回收站" : $"回收站 {driveRoot}",
            Description = driveRoot == null
                ? "所有磁盘回收站中的文件，清空后无法恢复"
                : $"{driveRoot} 回收站中的文件，清空后无法恢复",
            IsSafeToDelete = true,
            TotalSize = info.Size,
            ItemCount = info.ItemCount
        };
    }

    public static string? NormalizeDriveRoot(string? driveLetter)
    {
        if (string.IsNullOrWhiteSpace(driveLetter))
        {
            return null;
        }

        var trimmed = driveLetter.Trim();
        if (trimmed.EndsWith("\\", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 1);
        }

        return trimmed.Length == 2 && char.IsLetter(trimmed[0]) && trimmed[1] == ':'
            ? trimmed + "\\"
            : null;
    }

    private CleanupSuggestion BuildBrowserCacheSuggestion(CancellationToken cancellationToken)
    {
        var suggestion = new CleanupSuggestion
        {
            Kind = CleanupKind.BrowserCache,
            Category = "浏览器缓存",
            Description = "浏览器缓存目录，不含书签、密码和登录状态",
            IsSafeToDelete = true
        };

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            return suggestion;
        }

        var profileRoots = new[]
        {
            Path.Combine(localAppData, "Google", "Chrome", "User Data"),
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data")
        };

        foreach (var root in profileRoots)
        {
            foreach (var profile in SafeEnumerateDirectories(root))
            {
                foreach (var cache in new[] { "Cache", "Code Cache", "GPUCache", "DawnCache" })
                {
                    AddDirectoryFiles(Path.Combine(profile, cache), suggestion, true, cancellationToken);
                }
            }
        }

        var firefoxProfiles = Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles");
        foreach (var profile in SafeEnumerateDirectories(firefoxProfiles))
        {
            foreach (var cache in new[] { "cache2", "startupCache", "shader-cache", "thumbnails" })
            {
                AddDirectoryFiles(Path.Combine(profile, cache), suggestion, true, cancellationToken);
            }
        }

        var inetCache = Path.Combine(localAppData, "Microsoft", "Windows", "INetCache");
        AddDirectoryFiles(inetCache, suggestion, true, cancellationToken);

        return suggestion;
    }

    private CleanupSuggestion BuildWindowsUpdateSuggestion(CancellationToken cancellationToken)
    {
        var suggestion = new CleanupSuggestion
        {
            Kind = CleanupKind.WindowsUpdate,
            Category = "Windows 更新缓存",
            Description = "更新下载缓存；部分文件被服务占用，建议先停止 Windows Update 服务并提升权限",
            IsSafeToDelete = false
        };

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrWhiteSpace(windows))
        {
            return suggestion;
        }

        AddDirectoryFiles(Path.Combine(windows, "SoftwareDistribution", "Download"), suggestion, true, cancellationToken);
        return suggestion;
    }

    private CleanupSuggestion BuildDownloadsSuggestion(CancellationToken cancellationToken)
    {
        var suggestion = new CleanupSuggestion
        {
            Kind = CleanupKind.Downloads,
            Category = "下载文件夹",
            Description = "下载文件夹中的文件，请谨慎选择删除",
            IsSafeToDelete = false
        };

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
        {
            AddDirectoryFiles(Path.Combine(profile, "Downloads"), suggestion, false, cancellationToken);
        }

        return suggestion;
    }

    private void AddDirectoryFiles(
        string path,
        CleanupSuggestion suggestion,
        bool recursive,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        var before = suggestion.Files.Count;
        try
        {
            if (recursive)
            {
                FileWalker.WalkFiles(path, cancellationToken, file => AddFile(file, suggestion));
            }
            else
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", ScanOptions.Create()))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AddFile(file, suggestion);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
        }

        if (suggestion.Files.Count != before)
        {
            suggestion.NotifyFilesChanged();
        }
    }

    private void AddFile(string file, CleanupSuggestion suggestion)
    {
        if (_guard.IsProtected(file))
        {
            return;
        }

        try
        {
            var info = new FileInfo(file);
            suggestion.Files.Add(new FileItem
            {
                FullPath = file,
                Size = info.Length,
                LastModified = info.LastWriteTime,
                IsDirectory = false
            });
            suggestion.TotalSize += info.Length;
        }
        catch (Exception)
        {
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return Array.Empty<string>();
        }

        try
        {
            return Directory.EnumerateDirectories(path, "*", ScanOptions.Create()).ToList();
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }
}