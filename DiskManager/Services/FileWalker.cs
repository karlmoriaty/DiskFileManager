using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace DiskManager.Services;

public static class FileWalker
{
    public static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception)
        {
            return true;
        }
    }

    public static void WalkFiles(
        string path,
        CancellationToken cancellationToken,
        Action<string> onFile,
        ScanFilters? filters = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        var stack = new Stack<string>();
        stack.Push(path);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = stack.Pop();

            try
            {
                foreach (var file in Directory.EnumerateFiles(current, "*", ScanOptions.Create()))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    onFile(file);
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
                    if (filters != null && filters.IsExcludedDirectory(directory))
                    {
                        continue;
                    }

                    if (IsReparsePoint(directory))
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
    }
}