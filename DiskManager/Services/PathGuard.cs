using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DiskManager.Services;

public sealed class PathGuard
{
    private readonly string[] _protectedRoots;
    private readonly string[] _protectedSelf;
    private readonly string[] _allowedSubtrees;

    public PathGuard(
        IEnumerable<string> protectedRoots,
        IEnumerable<string> protectedSelf,
        IEnumerable<string> allowedSubtrees)
    {
        _protectedRoots = protectedRoots.Select(Normalize).Where(p => p.Length > 0).ToArray();
        _protectedSelf = protectedSelf.Select(Normalize).Where(p => p.Length > 0).ToArray();
        _allowedSubtrees = allowedSubtrees.Select(Normalize).Where(p => p.Length > 0).ToArray();
    }

    public static PathGuard CreateDefault()
        => new(BuildDefaultRoots(), BuildDefaultSelf(), BuildDefaultAllowed());

    private static IEnumerable<string> BuildDefaultRoots()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        var roots = new List<string> { windows, programFiles, programFilesX86, programData };

        if (!string.IsNullOrWhiteSpace(windows))
        {
            roots.Add(Path.Combine(windows, "System32"));
            roots.Add(Path.Combine(windows, "SysWOW64"));
            roots.Add(Path.Combine(windows, "WinSxS"));
            roots.Add(Path.Combine(windows, "Boot"));
            roots.Add(Path.Combine(windows, "Recovery"));
        }

        foreach (var drive in GetReadyDrives())
        {
            roots.Add(Path.Combine(drive, "System Volume Information"));
            roots.Add(Path.Combine(drive, "$Recycle.Bin"));
        }

        return roots;
    }

    private static IEnumerable<string> BuildDefaultSelf()
    {
        var result = new List<string>();
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
        {
            result.Add(profile);
        }

        result.AddRange(GetReadyDrives());
        return result;
    }

    private static IEnumerable<string> BuildDefaultAllowed()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrWhiteSpace(windows))
        {
            return Array.Empty<string>();
        }

        return new[]
        {
            Path.Combine(windows, "Temp"),
            Path.Combine(windows, "SoftwareDistribution", "Download"),
            Path.Combine(windows, "Logs", "CBS")
        };
    }

    private static IEnumerable<string> GetReadyDrives()
    {
        var drives = new List<string>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                var root = drive.RootDirectory.FullName;
                if (!string.IsNullOrWhiteSpace(root))
                {
                    drives.Add(root);
                }
            }
        }
        catch (Exception)
        {
        }

        return drives;
    }

    public static string Normalize(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full);
            if (root != null && string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
            {
                return full;
            }

            return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public bool IsProtected(string path)
    {
        var full = Normalize(path);
        if (full.Length == 0)
        {
            return true;
        }

        if (_allowedSubtrees.Any(allowed => IsStrictlyInside(full, allowed)))
        {
            return false;
        }

        if (_protectedRoots.Any(root => IsSameOrInside(full, root)))
        {
            return true;
        }

        return _protectedSelf.Any(root => string.Equals(full, root, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsSameOrInside(string path, string root)
    {
        if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? root
            : root + Path.DirectorySeparatorChar;

        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsStrictlyInside(string path, string root)
        => IsSameOrInside(path, root) && !string.Equals(path, root, StringComparison.OrdinalIgnoreCase);
}