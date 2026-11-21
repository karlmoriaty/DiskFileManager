using System;
using System.Collections.ObjectModel;
using System.IO;
using DiskManager.Models;
using DiskManager.Services;
using DiskManager.Utils;
using DiskManager.ViewModels;

namespace DiskManager.Tests;

public class UxPolishTests : IDisposable
{
    private readonly string _root;

    public UxPolishTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "DiskManagerTests", "ux", Guid.NewGuid().ToString("N"));
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

    private MainViewModel CreateViewModel(FakeDialogService? dialogs = null)
        => new(
            dialogs ?? new FakeDialogService(),
            new FakeRecycleBin(),
            new LogService(),
            new PathGuard(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()),
            new DiskMonitorService(Path.Combine(_root, "monitor")),
            new SettingsService(Path.Combine(_root, "settings")));

    [Fact]
    public void SettingsService_RoundTripsValues()
    {
        var service = new SettingsService(_root);
        service.Save(new AppSettings
        {
            LargeFileScanPath = @"D:\Media",
            MinFileSizeText = "250",
            DuplicateScanPath = @"E:\Data",
            CleanupDrive = @"D:\",
            DeleteModeIndex = 1,
            KeepStrategyIndex = 2,
            ConflictIndex = 0,
            SelectedTabIndex = 3
        });

        var loaded = new SettingsService(_root).Load();

        Assert.Equal(@"D:\Media", loaded.LargeFileScanPath);
        Assert.Equal("250", loaded.MinFileSizeText);
        Assert.Equal(1, loaded.DeleteModeIndex);
        Assert.Equal(3, loaded.SelectedTabIndex);
    }

    [Fact]
    public void SelectionSummary_TracksSelectionAndSize()
    {
        var items = new ObservableCollection<FileItem>
        {
            new() { FullPath = @"C:\a.txt", Size = 100 },
            new() { FullPath = @"C:\b.txt", Size = 200 }
        };

        using var summary = new SelectionSummary(items);
        Assert.Equal(2, summary.TotalCount);
        Assert.Equal(300, summary.TotalSize);
        Assert.Equal(0, summary.SelectedCount);

        items[0].IsSelected = true;
        Assert.Equal(1, summary.SelectedCount);
        Assert.Equal(100, summary.SelectedSize);

        items.Add(new FileItem { FullPath = @"C:\c.txt", Size = 50, IsSelected = true });
        Assert.Equal(3, summary.TotalCount);
        Assert.Equal(2, summary.SelectedCount);
        Assert.Equal(150, summary.SelectedSize);
    }

    [Fact]
    public void SelectionSummary_RaisesChangedOnSizeUpdate()
    {
        var items = new ObservableCollection<FileItem> { new() { FullPath = @"C:\a.txt", Size = 0 } };
        using var summary = new SelectionSummary(items);
        var raised = 0;
        summary.Changed += (_, _) => raised++;

        items[0].Size = 500;

        Assert.True(raised > 0);
        Assert.Equal(500, summary.TotalSize);
    }

    [Fact]
    public void MainViewModel_SelectAllFileOperations_Toggles()
    {
        using var viewModel = CreateViewModel();
        viewModel.FileOperationItems.Add(new FileItem { FullPath = @"C:\a.txt" });
        viewModel.FileOperationItems.Add(new FileItem { FullPath = @"C:\b.txt" });

        viewModel.SelectAllFileOperationsCommand.Execute(null);
        Assert.All(viewModel.FileOperationItems, item => Assert.True(item.IsSelected));

        viewModel.SelectAllFileOperationsCommand.Execute(null);
        Assert.All(viewModel.FileOperationItems, item => Assert.False(item.IsSelected));
    }

    [Fact]
    public void MainViewModel_RemoveSelectedFileOperations_RemovesOnlySelected()
    {
        using var viewModel = CreateViewModel();
        var keep = new FileItem { FullPath = @"C:\keep.txt" };
        var remove = new FileItem { FullPath = @"C:\remove.txt", IsSelected = true };
        viewModel.FileOperationItems.Add(keep);
        viewModel.FileOperationItems.Add(remove);

        viewModel.RemoveSelectedFileOperationsCommand.Execute(null);

        Assert.Single(viewModel.FileOperationItems);
        Assert.Same(keep, viewModel.FileOperationItems[0]);
    }

    [Fact]
    public void MainViewModel_AddPaths_AddsFiles()
    {
        using var viewModel = CreateViewModel();
        var file = Path.Combine(_root, "a.txt");
        File.WriteAllText(file, "content");

        viewModel.AddPaths(new[] { file });

        var item = Assert.Single(viewModel.FileOperationItems);
        Assert.Equal(file, item.FullPath);
        Assert.Equal(7, item.Size);
        Assert.False(item.IsDirectory);
    }

    [Fact]
    public void MainViewModel_ExportLargeFiles_WritesCsv()
    {
        var dialogs = new FakeDialogService();
        var exportPath = Path.Combine(_root, "large.csv");
        dialogs.SaveFileToReturn = exportPath;
        using var viewModel = CreateViewModel(dialogs);
        viewModel.LargeFiles.Add(new FileItem
        {
            FullPath = @"C:\a.txt",
            Size = 1024,
            LastModified = new DateTime(2024, 1, 2, 3, 4, 5)
        });

        viewModel.ExportLargeFilesCommand.Execute(null);

        Assert.True(File.Exists(exportPath));
        var content = File.ReadAllText(exportPath);
        Assert.Contains(@"C:\a.txt", content);
        Assert.Contains("2024-01-02 03:04:05", content);
    }

    [Fact]
    public void MainViewModel_ToggleAllDuplicateFiles_Toggles()
    {
        using var viewModel = CreateViewModel();
        var group = new DuplicateFileGroup { FileHash = "abc", FileSize = 10 };
        group.Files.Add(new FileItem { FullPath = @"C:\a.bin" });
        group.Files.Add(new FileItem { FullPath = @"C:\b.bin" });
        viewModel.DuplicateGroups.Add(group);

        viewModel.ToggleAllDuplicateFilesCommand.Execute(null);
        Assert.All(group.Files, file => Assert.True(file.IsSelected));

        viewModel.ToggleAllDuplicateFilesCommand.Execute(null);
        Assert.All(group.Files, file => Assert.False(file.IsSelected));
    }

    [Fact]
    public void UsageToBrushConverter_ReturnsRedForHighUsage()
    {
        var converter = new UsageToBrushConverter();
        var high = (System.Windows.Media.Brush)converter.Convert(
            95d, typeof(object), string.Empty, System.Globalization.CultureInfo.InvariantCulture);
        var low = (System.Windows.Media.Brush)converter.Convert(
            30d, typeof(object), string.Empty, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(System.Windows.Media.Brushes.IndianRed, high);
        Assert.Equal(System.Windows.Media.Brushes.SeaGreen, low);
    }
}