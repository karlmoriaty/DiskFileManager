using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiskManager.Models;

namespace DiskManager.Services;

public sealed class FileOperationService
{
    private enum Outcome
    {
        Succeeded,
        Skipped,
        Failed
    }

    private readonly PathGuard _guard;
    private readonly IRecycleBinService _recycleBin;
    private readonly LogService _log;

    public FileOperationService(PathGuard guard, IRecycleBinService recycleBin, LogService log)
    {
        _guard = guard;
        _recycleBin = recycleBin;
        _log = log;
    }

    public Task<OperationResult> CopyAsync(
        IReadOnlyList<string> sources,
        string destinationFolder,
        ConflictPolicy conflict = ConflictPolicy.Rename,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.Run(() => RunTransfer(sources, destinationFolder, false, conflict, progress, cancellationToken), cancellationToken);

    public Task<OperationResult> MoveAsync(
        IReadOnlyList<string> sources,
        string destinationFolder,
        ConflictPolicy conflict = ConflictPolicy.Rename,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.Run(() => RunTransfer(sources, destinationFolder, true, conflict, progress, cancellationToken), cancellationToken);

    public Task<OperationResult> DeleteAsync(
        IReadOnlyList<string> paths,
        DeleteMode mode,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.Run(() => RunDelete(paths, mode, progress, cancellationToken), cancellationToken);

    public bool DeletePath(string path, DeleteMode mode)
    {
        if (_guard.IsProtected(path))
        {
            _log.Log("删除", $"拒绝操作受保护路径: {path}", false);
            return false;
        }

        var isDirectory = Directory.Exists(path);
        if (!isDirectory && !File.Exists(path))
        {
            return false;
        }

        try
        {
            if (mode == DeleteMode.RecycleBin)
            {
                return _recycleBin.Recycle(path, isDirectory);
            }

            if (isDirectory)
            {
                Directory.Delete(path, true);
            }
            else
            {
                File.Delete(path);
            }

            return true;
        }
        catch (Exception ex)
        {
            _log.Log("删除", path, false, ex.Message);
            return false;
        }
    }

    private OperationResult RunTransfer(
        IReadOnlyList<string> sources,
        string destinationFolder,
        bool isMove,
        ConflictPolicy conflict,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        var result = new OperationResult();
        if (!Directory.Exists(destinationFolder))
        {
            result.MarkFailed($"目标文件夹不存在: {destinationFolder}");
            return result;
        }

        if (_guard.IsProtected(destinationFolder))
        {
            result.MarkFailed($"拒绝写入受保护路径: {destinationFolder}");
            return result;
        }

        var total = Math.Max(1, CountAllEntries(sources, cancellationToken));
        var processed = 0;
        var throttled = new ThrottledProgress(progress);
        var accessor = new ProgressAccessor(() =>
        {
            processed++;
            throttled.Report((int)((double)processed / total * 100));
        });

        foreach (var source in sources)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                result.MarkCancelled();
                break;
            }

            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(source));
            if (string.IsNullOrEmpty(name))
            {
                result.MarkFailed($"无法确定源名称: {source}");
                continue;
            }

            if (Directory.Exists(source) && PathGuard.IsStrictlyInside(destinationFolder, source))
            {
                result.MarkFailed($"目标目录位于源目录内部，已跳过: {source}");
                continue;
            }

            var destination = Path.Combine(destinationFolder, name);
            var outcome = isMove
                ? MovePath(source, destination, conflict, cancellationToken, accessor)
                : CopyPath(source, destination, conflict, cancellationToken, accessor);

            switch (outcome)
            {
                case Outcome.Succeeded:
                    result.MarkSucceeded();
                    break;
                case Outcome.Skipped:
                    result.MarkSkipped();
                    break;
                default:
                    result.MarkFailed($"操作失败: {source}");
                    break;
            }
        }

        throttled.Report(100, true);
        _log.Log(isMove ? "批量移动" : "批量复制",
            $"目标 {destinationFolder}；{result.Summary}", result.AllSucceeded);
        return result;
    }

    private OperationResult RunDelete(
        IReadOnlyList<string> paths,
        DeleteMode mode,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        var result = new OperationResult();
        var total = Math.Max(1, CountAllEntries(paths, cancellationToken));
        var processed = 0;
        var throttled = new ThrottledProgress(progress);
        var accessor = new ProgressAccessor(() =>
        {
            processed++;
            throttled.Report((int)((double)processed / total * 100));
        });

        foreach (var path in paths)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                result.MarkCancelled();
                break;
            }

            if (_guard.IsProtected(path))
            {
                result.MarkFailed($"拒绝操作受保护路径: {path}");
                continue;
            }

            var isDirectory = Directory.Exists(path);
            if (!isDirectory && !File.Exists(path))
            {
                result.MarkSkipped();
                continue;
            }

            try
            {
                var fileCount = isDirectory ? CountFilesInDirectory(path, cancellationToken) : 1;
                var ok = mode == DeleteMode.RecycleBin
                    ? _recycleBin.Recycle(path, isDirectory)
                    : DeletePermanent(path, isDirectory);

                if (ok)
                {
                    result.MarkSucceeded();
                    accessor.Advance(fileCount);
                }
                else
                {
                    result.MarkFailed($"删除失败: {path}");
                }
            }
            catch (Exception ex)
            {
                result.MarkFailed($"{path}: {ex.Message}");
                _log.Log("删除", path, false, ex.Message);
            }
        }

        throttled.Report(100, true);
        _log.Log("批量删除", $"{mode}；{result.Summary}", result.AllSucceeded);
        return result;
    }

    private static bool DeletePermanent(string path, bool isDirectory)
    {
        if (isDirectory)
        {
            Directory.Delete(path, true);
        }
        else
        {
            File.Delete(path);
        }

        return true;
    }

    private Outcome CopyPath(
        string source,
        string destination,
        ConflictPolicy conflict,
        CancellationToken cancellationToken,
        ProgressAccessor accessor)
    {
        if (Directory.Exists(source))
        {
            return CopyDirectory(source, destination, conflict, cancellationToken, accessor);
        }

        if (!File.Exists(source))
        {
            return Outcome.Failed;
        }

        if (_guard.IsProtected(destination))
        {
            _log.Log("复制", $"拒绝写入受保护路径: {destination}", false);
            return Outcome.Failed;
        }

        try
        {
            var target = ResolveFileTarget(source, destination, conflict);
            if (target == null)
            {
                accessor.Advance();
                return Outcome.Skipped;
            }

            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(source, target, conflict == ConflictPolicy.Overwrite);
            accessor.Advance();
            return Outcome.Succeeded;
        }
        catch (Exception ex)
        {
            accessor.Advance();
            _log.Log("复制", $"{source} -> {destination}", false, ex.Message);
            return Outcome.Failed;
        }
    }

    private Outcome MovePath(
        string source,
        string destination,
        ConflictPolicy conflict,
        CancellationToken cancellationToken,
        ProgressAccessor accessor)
    {
        if (_guard.IsProtected(source))
        {
            _log.Log("移动", $"拒绝操作受保护路径: {source}", false);
            return Outcome.Failed;
        }

        var isDirectory = Directory.Exists(source);
        if (!isDirectory && !File.Exists(source))
        {
            return Outcome.Failed;
        }

        if (_guard.IsProtected(destination))
        {
            _log.Log("移动", $"拒绝写入受保护路径: {destination}", false);
            return Outcome.Failed;
        }

        try
        {
            var target = ResolveFileTarget(source, destination, conflict);
            if (target == null)
            {
                if (isDirectory)
                {
                    accessor.Advance(CountFilesInDirectory(source, cancellationToken));
                }
                else
                {
                    accessor.Advance();
                }

                return Outcome.Skipped;
            }

            if (isDirectory)
            {
                return MoveDirectory(source, target, cancellationToken, accessor);
            }

            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            try
            {
                File.Move(source, target, conflict == ConflictPolicy.Overwrite);
                accessor.Advance();
            }
            catch (IOException)
            {
                File.Copy(source, target, true);
                File.Delete(source);
                accessor.Advance();
            }

            return Outcome.Succeeded;
        }
        catch (Exception ex)
        {
            _log.Log("移动", $"{source} -> {destination}", false, ex.Message);
            return Outcome.Failed;
        }
    }

    private Outcome MoveDirectory(
        string source,
        string target,
        CancellationToken cancellationToken,
        ProgressAccessor accessor)
    {
        var fileCount = CountFilesInDirectory(source, cancellationToken);
        try
        {
            Directory.Move(source, target);
            accessor.Advance(fileCount);
            return Outcome.Succeeded;
        }
        catch (IOException)
        {
            var outcome = CopyDirectory(source, target, ConflictPolicy.Overwrite, cancellationToken, accessor);
            if (outcome == Outcome.Succeeded)
            {
                Directory.Delete(source, true);
            }

            return outcome;
        }
    }

    private Outcome CopyDirectory(
        string source,
        string destination,
        ConflictPolicy conflict,
        CancellationToken cancellationToken,
        ProgressAccessor accessor)
    {
        if (_guard.IsProtected(destination))
        {
            _log.Log("复制", $"拒绝写入受保护路径: {destination}", false);
            return Outcome.Failed;
        }

        var targetDirectory = destination;
        if (Directory.Exists(targetDirectory))
        {
            if (conflict == ConflictPolicy.Skip)
            {
                accessor.Advance(CountFilesInDirectory(source, cancellationToken));
                return Outcome.Skipped;
            }

            if (conflict == ConflictPolicy.Rename)
            {
                targetDirectory = GetUniquePath(destination, true);
            }
        }

        try
        {
            Directory.CreateDirectory(targetDirectory);

            foreach (var file in Directory.EnumerateFiles(source, "*", ScanOptions.Create()))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileTarget = Path.Combine(targetDirectory, Path.GetFileName(file));
                if (conflict == ConflictPolicy.Skip && (File.Exists(fileTarget) || Directory.Exists(fileTarget)))
                {
                    accessor.Advance();
                    continue;
                }

                var resolved = ResolveFileTarget(file, fileTarget, conflict) ?? fileTarget;
                File.Copy(file, resolved, conflict == ConflictPolicy.Overwrite);
                accessor.Advance();
            }

            foreach (var directory in Directory.EnumerateDirectories(source, "*", ScanOptions.Create()))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (FileWalker.IsReparsePoint(directory))
                {
                    continue;
                }

                var childTarget = Path.Combine(targetDirectory, Path.GetFileName(directory));
                var outcome = CopyDirectory(directory, childTarget, conflict, cancellationToken, accessor);
                if (outcome == Outcome.Failed)
                {
                    return Outcome.Failed;
                }
            }

            return Outcome.Succeeded;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Log("复制目录", $"{source} -> {destination}", false, ex.Message);
            return Outcome.Failed;
        }
    }

    private static string? ResolveFileTarget(string source, string destination, ConflictPolicy conflict)
    {
        if (!File.Exists(destination) && !Directory.Exists(destination))
        {
            return destination;
        }

        return conflict switch
        {
            ConflictPolicy.Overwrite => destination,
            ConflictPolicy.Skip => null,
            _ => GetUniquePath(destination, Directory.Exists(destination))
        };
    }

    public static string GetUniquePath(string path, bool isDirectory)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = isDirectory ? string.Empty : Path.GetExtension(path);

        for (var index = 1; index < 10000; index++)
        {
            var candidate = Path.Combine(directory, $"{name} ({index}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{name} ({Guid.NewGuid():N}){extension}");
    }

    public static int CountFilesInDirectory(string path, CancellationToken cancellationToken = default)
    {
        if (File.Exists(path))
        {
            return 1;
        }

        if (!Directory.Exists(path))
        {
            return 0;
        }

        var count = 0;
        var stack = new Stack<string>();
        stack.Push(path);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = stack.Pop();

            try
            {
                foreach (var _ in Directory.EnumerateFiles(current, "*", ScanOptions.Create()))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    count++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
            }

            try
            {
                foreach (var directory in Directory.EnumerateDirectories(current, "*", ScanOptions.Create()))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (FileWalker.IsReparsePoint(directory))
                    {
                        continue;
                    }

                    stack.Push(directory);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
            }
        }

        return count;
    }

    private static int CountAllEntries(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var total = 0;
        foreach (var path in paths)
        {
            total += CountFilesInDirectory(path, cancellationToken);
        }

        return total;
    }

    private sealed class ProgressAccessor
    {
        private readonly Action _advance;

        public ProgressAccessor(Action advance) => _advance = advance;

        public void Advance(int count = 1)
        {
            for (var index = 0; index < count; index++)
            {
                _advance();
            }
        }
    }

    }