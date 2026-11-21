using System;
using System.IO;
using DiskManager.Services;
using DiskManager.ViewModels;

namespace DiskManager.Tests;

public class P2PolishTests : IDisposable
{
    private readonly string _root;

    public P2PolishTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "DiskManagerTests", "p2", Guid.NewGuid().ToString("N"));
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
            new LogService(Path.Combine(_root, "logs")),
            new PathGuard(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()),
            new DiskMonitorService(Path.Combine(_root, "monitor")),
            new SettingsService(Path.Combine(_root, "settings")));

    [Fact]
    public void ConfirmDeletion_LargePermanentDeleteRequiresTwoConfirmations()
    {
        var dialogs = new FakeDialogService();
        using var viewModel = CreateViewModel(dialogs);
        viewModel.SelectedDeleteModeIndex = 1;

        var confirmed = viewModel.ConfirmDeletion(1, 2L * 1024 * 1024 * 1024, "删除?");

        Assert.True(confirmed);
        Assert.Equal(2, dialogs.ConfirmCalls);
    }

    [Fact]
    public void ConfirmDeletion_RecycleBinRequiresOneConfirmation()
    {
        var dialogs = new FakeDialogService();
        using var viewModel = CreateViewModel(dialogs);
        viewModel.SelectedDeleteModeIndex = 0;

        var confirmed = viewModel.ConfirmDeletion(1, 5L * 1024 * 1024 * 1024, "删除?");

        Assert.True(confirmed);
        Assert.Equal(1, dialogs.ConfirmCalls);
    }

    [Fact]
    public void ConfirmDeletion_SmallPermanentDeleteRequiresOneConfirmation()
    {
        var dialogs = new FakeDialogService();
        using var viewModel = CreateViewModel(dialogs);
        viewModel.SelectedDeleteModeIndex = 1;

        var confirmed = viewModel.ConfirmDeletion(1, 1024, "删除?");

        Assert.True(confirmed);
        Assert.Equal(1, dialogs.ConfirmCalls);
    }

    [Fact]
    public void ConfirmDeletion_SecondConfirmationCancelled_Aborts()
    {
        var dialogs = new FakeDialogService();
        dialogs.ConfirmResults.Enqueue(true);
        dialogs.ConfirmResults.Enqueue(false);
        using var viewModel = CreateViewModel(dialogs);
        viewModel.SelectedDeleteModeIndex = 1;

        var confirmed = viewModel.ConfirmDeletion(1, 3L * 1024 * 1024 * 1024, "删除?");

        Assert.False(confirmed);
        Assert.Equal(2, dialogs.ConfirmCalls);
    }

    [Fact]
    public void AddRecentTarget_KeepsFiveUniqueEntries()
    {
        using var viewModel = CreateViewModel();

        for (var index = 0; index < 7; index++)
        {
            viewModel.AddRecentTarget(Path.Combine(_root, "target" + index));
        }

        Assert.Equal(5, viewModel.RecentTargets.Count);
        Assert.Equal(Path.Combine(_root, "target6"), viewModel.RecentTargets[0]);
        Assert.Equal(Path.Combine(_root, "target2"), viewModel.RecentTargets[4]);
    }

    [Fact]
    public void AddRecentTarget_MovesExistingEntryToFront()
    {
        using var viewModel = CreateViewModel();
        var first = Path.Combine(_root, "first");
        var second = Path.Combine(_root, "second");

        viewModel.AddRecentTarget(first);
        viewModel.AddRecentTarget(second);
        viewModel.AddRecentTarget(first);

        Assert.Equal(2, viewModel.RecentTargets.Count);
        Assert.Equal(first, viewModel.RecentTargets[0]);
    }

    [Fact]
    public void OpenAboutCommand_DelegatesToDialogService()
    {
        var dialogs = new FakeDialogService();
        using var viewModel = CreateViewModel(dialogs);

        viewModel.OpenAboutCommand.Execute(null);

        Assert.Equal(1, dialogs.ShowAboutCalls);
    }
}