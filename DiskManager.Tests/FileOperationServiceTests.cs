using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiskManager.Models;
using DiskManager.Services;

namespace DiskManager.Tests;

public class FileOperationServiceTests : IDisposable
{
    private readonly string _root;
    private readonly LogService _log = new();
    private readonly FakeRecycleBin _recycleBin = new();
    private readonly PathGuard _openGuard = new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    public FileOperationServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "DiskManagerTests", Guid.NewGuid().ToString("N"));
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

    private FileOperationService CreateService(PathGuard? guard = null)
        => new(guard ?? _openGuard, _recycleBin, _log);

    private (string Source, string Destination) CreateSourceTree()
    {
        var source = Path.Combine(_root, "src");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        File.WriteAllText(Path.Combine(source, "a.txt"), "a");
        File.WriteAllText(Path.Combine(source, "nested", "b.txt"), "b");

        var destination = Path.Combine(_root, "dest");
        Directory.CreateDirectory(destination);
        return (source, destination);
    }

    [Fact]
    public async Task CopyAsync_CopiesDirectoryRecursively()
    {
        var (source, destination) = CreateSourceTree();

        var result = await CreateService().CopyAsync(new[] { source }, destination);

        Assert.True(result.AllSucceeded);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal("a", await File.ReadAllTextAsync(Path.Combine(destination, "src", "a.txt")));
        Assert.Equal("b", await File.ReadAllTextAsync(Path.Combine(destination, "src", "nested", "b.txt")));
    }

    [Fact]
    public async Task MoveAsync_MovesDirectoryAndRemovesSource()
    {
        var (source, destination) = CreateSourceTree();

        var result = await CreateService().MoveAsync(new[] { source }, destination);

        Assert.True(result.AllSucceeded);
        Assert.False(Directory.Exists(source));
        Assert.True(File.Exists(Path.Combine(destination, "src", "nested", "b.txt")));
    }

    [Fact]
    public async Task CopyAsync_RenamePolicyCreatesUniqueFolder()
    {
        var (source, destination) = CreateSourceTree();
        var service = CreateService();

        await service.CopyAsync(new[] { source }, destination, ConflictPolicy.Rename);
        var result = await service.CopyAsync(new[] { source }, destination, ConflictPolicy.Rename);

        Assert.True(result.AllSucceeded);
        Assert.True(Directory.Exists(Path.Combine(destination, "src")));
        Assert.True(Directory.Exists(Path.Combine(destination, "src (1)")));
    }

    [Fact]
    public async Task CopyAsync_SkipPolicyMarksSkipped()
    {
        var (source, destination) = CreateSourceTree();
        var service = CreateService();

        await service.CopyAsync(new[] { source }, destination);
        var result = await service.CopyAsync(new[] { source }, destination, ConflictPolicy.Skip);

        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task DeleteAsync_PermanentRemovesFiles()
    {
        var file = Path.Combine(_root, "victim.txt");
        await File.WriteAllTextAsync(file, "x");

        var result = await CreateService().DeleteAsync(new[] { file }, DeleteMode.Permanent);

        Assert.True(result.AllSucceeded);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task DeleteAsync_RecycleModeUsesRecycleBinService()
    {
        var file = Path.Combine(_root, "victim.txt");
        await File.WriteAllTextAsync(file, "x");

        var result = await CreateService().DeleteAsync(new[] { file }, DeleteMode.RecycleBin);

        Assert.True(result.AllSucceeded);
        Assert.Contains(file, _recycleBin.Recycled);
    }

    [Fact]
    public void DeletePath_RejectsProtectedPath()
    {
        var file = Path.Combine(_root, "protected.txt");
        File.WriteAllText(file, "x");
        var guard = new PathGuard(new[] { _root }, Array.Empty<string>(), Array.Empty<string>());

        var deleted = CreateService(guard).DeletePath(file, DeleteMode.Permanent);

        Assert.False(deleted);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void CountFilesInDirectory_CountsRecursivelyWithoutReparsePoints()
    {
        var (source, _) = CreateSourceTree();

        Assert.Equal(2, FileOperationService.CountFilesInDirectory(source));
    }

    [Fact]
    public void GetUniquePath_ReturnsAvailableName()
    {
        var file = Path.Combine(_root, "dup.txt");
        File.WriteAllText(file, "x");

        var unique = FileOperationService.GetUniquePath(file, false);

        Assert.Equal(Path.Combine(_root, "dup (1).txt"), unique);
    }
}