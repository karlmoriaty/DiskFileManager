using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DiskManager.Models;

namespace DiskManager.Services;

public class DuplicateFileService
{
    private const int SampleBytes = 4096;

    private readonly record struct FileEntry(string Path, long Size, DateTime LastModified);

    public Task<List<DuplicateFileGroup>> FindDuplicatesAsync(
        string path,
        IProgress<int>? progress = null,
        ScanFilters? filters = null,
        CancellationToken cancellationToken = default)
        => Task.Run(() => FindDuplicates(path, progress, filters, cancellationToken), cancellationToken);

    public int LastSkippedCloudFileCount { get; private set; }

    public List<DuplicateFileGroup> FindDuplicates(
        string path,
        IProgress<int>? progress = null,
        ScanFilters? filters = null,
        CancellationToken cancellationToken = default)
    {
        var throttled = new ThrottledProgress(progress);
        var sizeCounts = new Dictionary<long, int>();
        LastSkippedCloudFileCount = 0;

        FileWalker.WalkFiles(path, cancellationToken, file =>
        {
            try
            {
                if (filters != null && filters.IsExcludedFile(file))
                {
                    return;
                }

                var length = new FileInfo(file).Length;
                if (length == 0)
                {
                    return;
                }

                sizeCounts.TryGetValue(length, out var count);
                sizeCounts[length] = count + 1;
            }
            catch (Exception)
            {
            }
        }, filters);

        throttled.Report(5, true);

        var entries = new List<FileEntry>();
        FileWalker.WalkFiles(path, cancellationToken, file =>
        {
            try
            {
                if (filters != null && filters.IsExcludedFile(file))
                {
                    return;
                }

                var info = new FileInfo(file);
                if (info.Length == 0
                    || !sizeCounts.TryGetValue(info.Length, out var count)
                    || count < 2)
                {
                    return;
                }

                if ((info.Attributes & FileAttributes.Offline) != 0)
                {
                    LastSkippedCloudFileCount++;
                    return;
                }

                entries.Add(new FileEntry(file, info.Length, info.LastWriteTime));
            }
            catch (Exception)
            {
            }
        }, filters);

        throttled.Report(10, true);

        var sizeGroups = entries
            .GroupBy(entry => entry.Size)
            .Where(group => group.Count() > 1)
            .ToList();

        var candidates = new List<(long Size, List<FileEntry> Files)>();
        var processed = 0;
        foreach (var group in sizeGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var sampleGroup in group.GroupBy(entry => ComputeSampleHash(entry.Path)).Where(g => g.Count() > 1))
            {
                candidates.Add((group.Key, sampleGroup.ToList()));
            }

            processed++;
            throttled.Report(10 + (int)((double)processed / Math.Max(1, sizeGroups.Count) * 30));
        }

        var results = new List<DuplicateFileGroup>();
        processed = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var hashGroup in candidate.Files.GroupBy(entry => ComputeFullHash(entry.Path)))
            {
                var files = hashGroup.ToList();
                if (files.Count < 2)
                {
                    continue;
                }

                var group = new DuplicateFileGroup
                {
                    FileHash = hashGroup.Key,
                    FileSize = candidate.Size
                };

                foreach (var entry in files)
                {
                    group.Files.Add(new FileItem
                    {
                        FullPath = entry.Path,
                        Size = entry.Size,
                        LastModified = entry.LastModified,
                        IsDirectory = false
                    });
                }

                results.Add(group);
            }

            processed++;
            throttled.Report(40 + (int)((double)processed / Math.Max(1, candidates.Count) * 60));
        }

        throttled.Report(100, true);
        return results.OrderByDescending(group => group.TotalWastedSpace).ToList();
    }

    public static FileItem? SelectKeep(IEnumerable<FileItem> files, DuplicateKeepStrategy strategy)
    {
        var list = files.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        return strategy switch
        {
            DuplicateKeepStrategy.Newest => list
                .OrderByDescending(file => file.LastModified)
                .ThenBy(file => file.FullPath.Length)
                .First(),
            DuplicateKeepStrategy.Oldest => list
                .OrderBy(file => file.LastModified)
                .ThenBy(file => file.FullPath.Length)
                .First(),
            _ => list
                .OrderBy(file => file.FullPath.Length)
                .ThenBy(file => file.FullPath, StringComparer.OrdinalIgnoreCase)
                .First()
        };
    }

    public static List<FileItem> SelectToDelete(IEnumerable<FileItem> files, DuplicateKeepStrategy strategy)
    {
        var list = files.ToList();
        var keep = SelectKeep(list, strategy);
        if (keep == null)
        {
            return new List<FileItem>();
        }

        return list.Where(file => !ReferenceEquals(file, keep) && !file.FullPath.Equals(keep.FullPath, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static string ComputeSampleHash(string path)
    {
        try
        {
            using var sha = SHA256.Create();
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
            var buffer = new byte[SampleBytes];
            var headLength = (int)Math.Min(SampleBytes, stream.Length);
            stream.ReadExactly(buffer, 0, headLength);
            sha.TransformBlock(buffer, 0, headLength, null, 0);

            if (stream.Length > SampleBytes * 2)
            {
                stream.Seek(-SampleBytes, SeekOrigin.End);
                stream.ReadExactly(buffer, 0, SampleBytes);
                sha.TransformBlock(buffer, 0, SampleBytes, null, 0);
            }

            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        }
        catch (Exception)
        {
            return "sample-error-" + Guid.NewGuid().ToString("N");
        }
    }

    private static string ComputeFullHash(string path)
    {
        try
        {
            using var sha = SHA256.Create();
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
            return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }
        catch (Exception)
        {
            return "full-error-" + Guid.NewGuid().ToString("N");
        }
    }
}