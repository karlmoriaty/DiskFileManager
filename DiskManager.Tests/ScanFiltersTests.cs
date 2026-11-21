using System;
using System.IO;
using DiskManager.Services;

namespace DiskManager.Tests;

public class ScanFiltersTests : IDisposable
{
    private readonly string _root;

    public ScanFiltersTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "DiskManagerTests", "filters", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (Exception)
        {
        }
    }

    [Fact]
    public void DefaultFilters_ExcludeBuildDirectories()
    {
        var filters = ScanFilters.Create(true, null, null);

        Assert.True(filters.IsExcludedDirectory(Path.Combine(@"C:\Projects\App\node_modules")));
        Assert.True(filters.IsExcludedDirectory(Path.Combine(@"C:\Projects\App\.git")));
        Assert.True(filters.IsExcludedDirectory(Path.Combine(@"C:\Projects\App\bin")));
        Assert.False(filters.IsExcludedDirectory(Path.Combine(@"C:\Projects\App\src")));
    }

    [Fact]
    public void DefaultFilters_ExcludeSystemDirectories()
    {
        var filters = ScanFilters.Create(true, null, null);

        Assert.True(filters.IsExcludedDirectory(@"C:\Windows"));
        Assert.True(filters.IsExcludedDirectory(@"C:\Windows\System32"));
        Assert.False(filters.IsExcludedDirectory(@"C:\Windows10Backup"));
    }

    [Fact]
    public void CustomNamesAndPaths_AreApplied()
    {
        var custom = Path.Combine(_root, "skip-me");
        Directory.CreateDirectory(custom);

        var filters = ScanFilters.Create(false, $"{custom};caches", null);

        Assert.True(filters.IsExcludedDirectory(custom));
        Assert.True(filters.IsExcludedDirectory(Path.Combine(_root, "caches")));
        Assert.False(filters.IsExcludedDirectory(Path.Combine(_root, "keep")));
    }

    [Theory]
    [InlineData(".tmp", ".tmp")]
    [InlineData("tmp;log", ".log")]
    public void ExtensionFilters_AreNormalized(string configured, string probe)
    {
        var filters = ScanFilters.Create(false, null, configured);

        Assert.True(filters.IsExcludedFile(Path.Combine(_root, "file" + probe)));
        Assert.False(filters.IsExcludedFile(Path.Combine(_root, "file.txt")));
    }

    [Fact]
    public void EmptyFilters_ExcludeNothing()
    {
        Assert.False(ScanFilters.Empty.IsExcludedDirectory(@"C:\Windows"));
        Assert.False(ScanFilters.Empty.IsExcludedFile(@"C:\a.tmp"));
    }

    [Fact]
    public void WalkFiles_SkipsExcludedDirectories()
    {
        var root = Path.Combine(_root, "walk");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "node_modules"));
        File.WriteAllText(Path.Combine(root, "src", "a.txt"), "a");
        File.WriteAllText(Path.Combine(root, "node_modules", "b.txt"), "b");

        var visited = new List<string>();
        var filters = ScanFilters.Create(true, null, null);
        FileWalker.WalkFiles(root, default, path => visited.Add(path), filters);

        Assert.Contains(visited, path => path.EndsWith("a.txt", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(visited, path => path.EndsWith("b.txt", StringComparison.OrdinalIgnoreCase));
    }
}