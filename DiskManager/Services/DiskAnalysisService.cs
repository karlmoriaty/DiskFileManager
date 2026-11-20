using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiskManager.Models;

namespace DiskManager.Services;

public class DiskAnalysisService
{
    public List<DiskInfo> GetAllDisks() => DiskEnumerator.GetReadyDisks();

    public Task<List<FileItem>> FindLargeFilesAsync(
        string path,
        long minSizeBytes,
        int maxResults = 100,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.Run(() => FindLargeFiles(path, minSizeBytes, maxResults, progress, cancellationToken), cancellationToken);

    public List<FileItem> FindLargeFiles(
        string path,
        long minSizeBytes,
        int maxResults = 100,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var heap = new PriorityQueue<FileItem, long>();
        var scanned = 0;

        FileWalker.WalkFiles(path, cancellationToken, file =>
        {
            scanned++;
            if (scanned % 500 == 0)
            {
                progress?.Report(scanned);
            }

            try
            {
                var info = new FileInfo(file);
                if (info.Length < minSizeBytes)
                {
                    return;
                }

                var item = new FileItem
                {
                    FullPath = file,
                    Size = info.Length,
                    LastModified = info.LastWriteTime,
                    IsDirectory = false
                };

                if (heap.Count < maxResults)
                {
                    heap.Enqueue(item, info.Length);
                }
                else if (heap.TryPeek(out _, out var smallest) && info.Length > smallest)
                {
                    heap.Dequeue();
                    heap.Enqueue(item, info.Length);
                }
            }
            catch (Exception)
            {
            }
        });

        progress?.Report(scanned);

        return heap.UnorderedItems
            .Select(entry => entry.Element)
            .OrderByDescending(item => item.Size)
            .ToList();
    }

    public static long GetDirectorySize(string path, CancellationToken cancellationToken = default)
    {
        long size = 0;
        FileWalker.WalkFiles(path, cancellationToken, file =>
        {
            try
            {
                size += new FileInfo(file).Length;
            }
            catch (Exception)
            {
            }
        });
        return size;
    }
}