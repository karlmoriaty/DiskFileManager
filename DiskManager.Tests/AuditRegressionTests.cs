using System;
using System.IO;
using System.Threading.Tasks;
using DiskManager.Models;
using DiskManager.Services;

namespace DiskManager.Tests;

public class AuditRegressionTests : IDisposable
{
    private readonly string _root;
    private readonly LogService _log = new();
    private readonly FakeRecycleBin _recycleBin = new();
    private readonly PathGuard _openGuard = new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    public AuditRegressionTests()
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

    private FileOperationService Create(PathGuard? guard = null)
        => new(guard ?? _openGuard, _recycleBin, _log);

    [Fact]
    public async Task CopyAsync_RejectsProtectedDestinationFolder()
    {
        var source = Path.Combine(_root, "file.txt");
        File.WriteAllText(source, "x");
        var destination = Path.Combine(_root, "protected");
        Directory.CreateDirectory(destination);
        var guard = new PathGuard(new[] { destination }, Array.Empty<string>(), Array.Empty<string>());

        var result = await Create(guard).CopyAsync(new[] { source }, destination);

        Assert.False(result.AllSucceeded);
        Assert.False(File.Exists(Path.Combine(destination, "file.txt")));
    }

    [Fact]
    public async Task CopyAsync_RejectsDestinationInsideSource()
    {
        var source = Path.Combine(_root, "src");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.txt"), "a");
        var inner = Path.Combine(source, "inner");
        Directory.CreateDirectory(inner);

        var result = await Create().CopyAsync(new[] { source }, inner);

        Assert.False(result.AllSucceeded);
        Assert.False(Directory.Exists(Path.Combine(inner, "src")));
    }

    [Fact]
    public async Task CopyAsync_SkipPolicySkipsExistingDirectoryWithoutFailing()
    {
        var source = Path.Combine(_root, "src");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.txt"), "new");
        var destination = Path.Combine(_root, "dest");
        Directory.CreateDirectory(destination);
        var service = Create();

        var first = await service.CopyAsync(new[] { source }, destination);
        Assert.True(first.AllSucceeded);

        File.WriteAllText(Path.Combine(source, "a.txt"), "changed");
        var second = await service.CopyAsync(new[] { source }, destination, ConflictPolicy.Skip);

        Assert.Equal(0, second.Failed);
        Assert.Equal(1, second.Skipped);
        Assert.Equal("new", File.ReadAllText(Path.Combine(destination, "src", "a.txt")));
    }

    [Theory]
    [InlineData("C:", @"C:\")]
    [InlineData(@"D:\", @"D:\")]
    [InlineData("E", null)]
    [InlineData("ab:", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeDriveRoot_ProducesShellCompatibleRoot(string? input, string? expected)
    {
        Assert.Equal(expected, CleanupService.NormalizeDriveRoot(input));
    }

    [Fact]
    public void FileItem_Name_HandlesDriveRoot()
    {
        Assert.Equal("C:", new FileItem { FullPath = @"C:\" }.Name);
        Assert.Equal("a.txt", new FileItem { FullPath = @"C:\temp\a.txt" }.Name);
    }

    [Fact]
    public async Task CopyAsync_RejectsCopyingDirectoryIntoItself()
    {
        var source = Path.Combine(_root, "self");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.txt"), "a");

        var result = await Create().CopyAsync(new[] { source }, source);

        Assert.False(result.AllSucceeded);
        Assert.True(File.Exists(Path.Combine(source, "a.txt")));
        Assert.False(Directory.Exists(Path.Combine(source, "self")));
    }

    [Fact]
    public async Task MoveAsync_RejectsMovingDirectoryIntoItself()
    {
        var source = Path.Combine(_root, "self");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.txt"), "a");

        var result = await Create().MoveAsync(new[] { source }, source);

        Assert.False(result.AllSucceeded);
        Assert.True(File.Exists(Path.Combine(source, "a.txt")));
        Assert.False(Directory.Exists(Path.Combine(source, "self")));
    }

    [Fact]
    public async Task DeleteAsync_PermanentHandlesReadOnlyFile()
    {
        var file = Path.Combine(_root, "readonly.txt");
        await File.WriteAllTextAsync(file, "x");
        File.SetAttributes(file, FileAttributes.ReadOnly);

        var result = await Create().DeleteAsync(new[] { file }, DeleteMode.Permanent);

        Assert.True(result.AllSucceeded);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task DeleteAsync_PermanentHandlesReadOnlyTree()
    {
        var directory = Path.Combine(_root, "readonly-tree");
        Directory.CreateDirectory(Path.Combine(directory, "nested"));
        var file = Path.Combine(directory, "nested", "a.txt");
        await File.WriteAllTextAsync(file, "x");
        File.SetAttributes(file, FileAttributes.ReadOnly);

        var result = await Create().DeleteAsync(new[] { directory }, DeleteMode.Permanent);

        Assert.True(result.AllSucceeded);
        Assert.False(Directory.Exists(directory));
    }
}