using System;
using System.IO;
using DiskManager.Models;
using DiskManager.Services;
using DiskManager.ViewModels;

namespace DiskManager.Tests;

public class MainViewModelTests : IDisposable
{
    private readonly string _dataDirectory;

    public MainViewModelTests()
    {
        _dataDirectory = Path.Combine(Path.GetTempPath(), "DiskManagerTests", "vm", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dataDirectory, true);
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
            new DiskMonitorService(_dataDirectory));

    [Fact]
    public void Constructor_InitialisesDisksAndStatus()
    {
        using var viewModel = CreateViewModel();

        Assert.Equal("就绪", viewModel.StatusText);
        Assert.NotEmpty(viewModel.Drives);
        Assert.NotNull(viewModel.SelectedCleanupDrive);
        Assert.Equal(0, viewModel.SelectedDeleteModeIndex);
        Assert.NotNull(viewModel.DeleteModeOptions);
        Assert.Equal(3, viewModel.KeepStrategyOptions.Count);
    }

    [Fact]
    public void ToggleCleanupSelection_SelectsAndClearsAllFiles()
    {
        using var viewModel = CreateViewModel();
        var suggestion = new CleanupSuggestion { Kind = CleanupKind.TempFiles };
        suggestion.Files.Add(new FileItem { FullPath = @"C:\temp\a.tmp" });
        suggestion.Files.Add(new FileItem { FullPath = @"C:\temp\b.tmp" });

        viewModel.ToggleCleanupSelectionCommand.Execute(suggestion);
        Assert.All(suggestion.Files, file => Assert.True(file.IsSelected));

        viewModel.ToggleCleanupSelectionCommand.Execute(suggestion);
        Assert.All(suggestion.Files, file => Assert.False(file.IsSelected));
    }

    [Fact]
    public void ToggleCleanupSelection_IgnoresRecycleBinSuggestion()
    {
        using var viewModel = CreateViewModel();
        var suggestion = new CleanupSuggestion { Kind = CleanupKind.RecycleBin, ItemCount = 3 };

        viewModel.ToggleCleanupSelectionCommand.Execute(suggestion);

        Assert.Empty(suggestion.Files);
    }

    [Fact]
    public void OpenFileLocation_DelegatesToDialogService()
    {
        var dialogs = new FakeDialogService();
        using var viewModel = CreateViewModel(dialogs);
        var item = new FileItem { FullPath = @"C:\temp\a.txt" };

        viewModel.OpenFileLocationCommand.Execute(item);

        Assert.Contains(@"C:\temp\a.txt", dialogs.OpenedPaths);
    }

    [Fact]
    public void OpenFileLocation_WithoutSelection_ShowsInfo()
    {
        var dialogs = new FakeDialogService();
        using var viewModel = CreateViewModel(dialogs);

        viewModel.OpenFileLocationCommand.Execute(null);

        Assert.Single(dialogs.Infos);
    }

    [Fact]
    public async Task CancelAndWaitAsync_CompletesWhenIdle()
    {
        using var viewModel = CreateViewModel();

        await viewModel.CancelAndWaitAsync(TimeSpan.FromMilliseconds(200));

        Assert.False(viewModel.IsBusy);
    }
}