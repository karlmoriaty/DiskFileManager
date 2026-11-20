using System.Collections.Generic;

namespace DiskManager.Services;

public interface IDialogService
{
    string? PickFolder(string description);
    IReadOnlyList<string> PickFiles(string title);
    bool Confirm(string message, string title);
    void Info(string message, string title = "提示");
    void Error(string message, string title = "错误");
    void OpenInExplorer(string path);
}