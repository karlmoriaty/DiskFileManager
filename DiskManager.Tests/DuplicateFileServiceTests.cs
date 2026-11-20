using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiskManager.Models;
using DiskManager.Services;

namespace DiskManager.Tests;

public class DuplicateFileServiceTests : IDisposable
{
    private readonly string _root;

    public DuplicateFileServiceTests()
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

    [Fact]
    public async Task FindDuplicatesAsync_GroupsIdenticalFilesBySizeAndHash()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "same-content");
        File.WriteAllText(Path.Combine(_root, "b.txt"), "same-content");
        File.WriteAllText(Path.Combine(_root, "c.txt"), "same-content");
        File.WriteAllText(Path.Combine(_root, "d.txt"), "different-content");

        var groups = await new DuplicateFileService().FindDuplicatesAsync(_root);

        var group = Assert.Single(groups);
        Assert.Equal(3, group.Files.Count);
        Assert.Equal(2 * "same-content".Length, group.TotalWastedSpace);
    }

    [Fact]
    public async Task FindDuplicatesAsync_IgnoresZeroByteFiles()
    {
        File.WriteAllText(Path.Combine(_root, "empty1.txt"), string.Empty);
        File.WriteAllText(Path.Combine(_root, "empty2.txt"), string.Empty);

        var groups = await new DuplicateFileService().FindDuplicatesAsync(_root);

        Assert.Empty(groups);
    }

    [Fact]
    public void SelectKeep_HonoursNewestAndOldest()
    {
        var older = new FileItem { FullPath = @"C:\a\older.txt", LastModified = new DateTime(2020, 1, 1) };
        var newer = new FileItem { FullPath = @"C:\b\newer.txt", LastModified = new DateTime(2024, 1, 1) };
        var files = new[] { older, newer };

        Assert.Same(newer, DuplicateFileService.SelectKeep(files, DuplicateKeepStrategy.Newest));
        Assert.Same(older, DuplicateFileService.SelectKeep(files, DuplicateKeepStrategy.Oldest));
    }

    [Fact]
    public void SelectKeep_ShortestPathWins()
    {
        var deep = new FileItem { FullPath = @"C:\a\b\c\file.txt", LastModified = DateTime.Now };
        var shallow = new FileItem { FullPath = @"C:\file.txt", LastModified = DateTime.Now };

        Assert.Same(shallow, DuplicateFileService.SelectKeep(new[] { deep, shallow }, DuplicateKeepStrategy.ShortestPath));
    }

    [Fact]
    public void SelectToDelete_RemovesExactlyOneCopyPerGroup()
    {
        var files = new[]
        {
            new FileItem { FullPath = @"C:\a\file.txt", LastModified = DateTime.Now },
            new FileItem { FullPath = @"C:\b\file.txt", LastModified = DateTime.Now },
            new FileItem { FullPath = @"C:\c\file.txt", LastModified = DateTime.Now }
        };

        var toDelete = DuplicateFileService.SelectToDelete(files, DuplicateKeepStrategy.ShortestPath);

        Assert.Equal(2, toDelete.Count);
        Assert.DoesNotContain(toDelete, item => item.FullPath == @"C:\a\file.txt");
    }
}