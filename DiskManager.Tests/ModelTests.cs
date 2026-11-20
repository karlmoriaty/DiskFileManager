using System.ComponentModel;
using DiskManager.Models;
using DiskManager.Utils;

namespace DiskManager.Tests;

public class ModelTests
{
    [Fact]
    public void FileItem_RaisesPropertyChangedForIsSelected()
    {
        var item = new FileItem();
        var raised = new List<string?>();
        item.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        item.IsSelected = true;

        Assert.Contains(nameof(FileItem.IsSelected), raised);
        Assert.True(item.IsSelected);
    }

    [Fact]
    public void CleanupSuggestion_RecycleBinUsesItemCount()
    {
        var suggestion = new CleanupSuggestion
        {
            Kind = CleanupKind.RecycleBin,
            ItemCount = 7,
            TotalSize = 2048
        };

        Assert.Equal(7, suggestion.FileCount);
        Assert.True(suggestion.HasItems);
        Assert.False(suggestion.CanSelectFiles);
        Assert.Equal("清空回收站", suggestion.ActionText);
        Assert.Equal("2 KB", suggestion.TotalSizeFormatted);
    }

    [Fact]
    public void CleanupSuggestion_TempFilesUseFileCollection()
    {
        var suggestion = new CleanupSuggestion { Kind = CleanupKind.TempFiles };
        suggestion.Files.Add(new FileItem { FullPath = @"C:\temp\a.tmp" });

        Assert.Equal(1, suggestion.FileCount);
        Assert.True(suggestion.CanSelectFiles);
        Assert.Equal("删除选中文件", suggestion.ActionText);
    }

    [Fact]
    public void DuplicateFileGroup_ComputesWastedSpace()
    {
        var group = new DuplicateFileGroup { FileHash = "abc", FileSize = 100 };
        group.Files.Add(new FileItem { FullPath = @"C:\a.bin" });
        group.Files.Add(new FileItem { FullPath = @"C:\b.bin" });
        group.Files.Add(new FileItem { FullPath = @"C:\c.bin" });

        Assert.Equal(200, group.TotalWastedSpace);
        Assert.Equal("a.bin", group.Name);
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1 MB")]
    public void SizeFormatter_FormatsBytes(long bytes, string expected)
    {
        Assert.Equal(expected, SizeFormatter.Format(bytes));
    }

    [Fact]
    public void OperationResult_TracksOutcomes()
    {
        var result = new OperationResult();

        result.MarkSucceeded();
        result.MarkSkipped();
        result.MarkFailed("boom");

        Assert.Equal(3, result.Total);
        Assert.False(result.AllSucceeded);
        Assert.Contains("boom", result.Summary);
    }

    [Fact]
    public void ObservableObject_NotifiesOnlyOnChange()
    {
        var model = new TestObservable();
        var raised = 0;
        model.PropertyChanged += (_, _) => raised++;

        model.Value = 1;
        model.Value = 1;

        Assert.Equal(1, raised);
    }

    private sealed class TestObservable : ObservableObject
    {
        private int _value;

        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }
}