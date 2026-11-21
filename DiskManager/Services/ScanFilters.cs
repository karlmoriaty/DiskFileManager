using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DiskManager.Services;

public sealed class ScanFilters
{
    private static readonly string[] DefaultNames =
    {
        "node_modules",
        ".git",
        ".vs",
        "obj",
        "bin",
        "packages",
        ".nuget",
        "__pycache__"
    };

    private readonly HashSet<string> _excludedNames;
    private readonly string[] _excludedFullPaths;
    private readonly HashSet<string> _excludedExtensions;

    private ScanFilters(HashSet<string> names, string[] fullPaths, HashSet<string> extensions)
    {
        _excludedNames = names;
        _excludedFullPaths = fullPaths;
        _excludedExtensions = extensions;
    }

    public static ScanFilters Empty { get; } = new(
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        Array.Empty<string>(),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    public static ScanFilters Create(bool useDefaults, string? customDirectories, string? excludedExtensions)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fullPaths = new List<string>();

        if (useDefaults)
        {
            foreach (var name in DefaultNames)
            {
                names.Add(name);
            }

            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            foreach (var path in new[] { windows, programFiles, programFilesX86, programData })
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    fullPaths.Add(path);
                }
            }

            foreach (var drive in Directory.GetLogicalDrives())
            {
                fullPaths.Add(Path.Combine(drive, "$Recycle.Bin"));
                fullPaths.Add(Path.Combine(drive, "System Volume Information"));
            }
        }

        foreach (var custom in SplitList(customDirectories))
        {
            if (custom.IndexOf(Path.DirectorySeparatorChar) >= 0
                || custom.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                if (Directory.Exists(custom))
                {
                    fullPaths.Add(custom);
                }
            }
            else
            {
                names.Add(custom);
            }
        }

        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in SplitList(excludedExtensions))
        {
            extensions.Add(extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension);
        }

        var normalized = fullPaths
            .Select(PathGuard.Normalize)
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ScanFilters(names, normalized, extensions);
    }

    public bool HasDirectoryFilters => _excludedNames.Count > 0 || _excludedFullPaths.Length > 0;

    public bool HasExtensionFilters => _excludedExtensions.Count > 0;

    public bool IsExcludedDirectory(string path)
    {
        if (!HasDirectoryFilters)
        {
            return false;
        }

        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        if (name.Length > 0 && _excludedNames.Contains(name))
        {
            return true;
        }

        foreach (var fullPath in _excludedFullPaths)
        {
            if (PathGuard.IsSameOrInside(path, fullPath))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsExcludedFile(string path)
        => _excludedExtensions.Count > 0 && _excludedExtensions.Contains(Path.GetExtension(path));

    private static IEnumerable<string> SplitList(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        return text
            .Split(new[] { ';', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0);
    }
}