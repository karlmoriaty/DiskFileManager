using System.Collections.Generic;
using DiskManager.Models;
using DiskManager.Services;

namespace DiskManager.Tests;

internal sealed class FakeRecycleBin : IRecycleBinService
{
    public List<string> Recycled { get; } = new();
    public int EmptyCalls { get; private set; }
    public RecycleBinInfo Info { get; set; } = new(0, 0);
    public string? LastQueryRoot { get; private set; }
    public string? LastEmptyRoot { get; private set; }

    public bool Recycle(string path, bool isDirectory)
    {
        Recycled.Add(path);
        if (isDirectory)
        {
            if (System.IO.Directory.Exists(path))
            {
                System.IO.Directory.Delete(path, true);
            }
        }
        else if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
        }

        return true;
    }

    public bool Empty(string? driveRoot = null)
    {
        EmptyCalls++;
        LastEmptyRoot = driveRoot;
        return true;
    }

    public RecycleBinInfo Query(string? driveRoot = null)
    {
        LastQueryRoot = driveRoot;
        return Info;
    }
}

internal sealed class FakeDialogService : IDialogService
{
    public List<string> Errors { get; } = new();
    public List<string> Infos { get; } = new();
    public int ConfirmCalls { get; private set; }
    public bool ConfirmResult { get; set; } = true;
    public string? FolderToReturn { get; set; }
    public IReadOnlyList<string> FilesToReturn { get; set; } = new List<string>();
    public List<string> OpenedPaths { get; } = new();

    public string? PickFolder(string description) => FolderToReturn;

    public IReadOnlyList<string> PickFiles(string title) => FilesToReturn;

    public bool Confirm(string message, string title)
    {
        ConfirmCalls++;
        return ConfirmResult;
    }

    public void Info(string message, string title = "提示") => Infos.Add(message);

    public void Error(string message, string title = "错误") => Errors.Add(message);

    public void OpenInExplorer(string path) => OpenedPaths.Add(path);
}