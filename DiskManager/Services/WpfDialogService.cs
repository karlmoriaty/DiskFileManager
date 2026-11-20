using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;

namespace DiskManager.Services;

public sealed class WpfDialogService : IDialogService
{
    public string? PickFolder(string description)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = description,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public IReadOnlyList<string> PickFiles(string title)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = title,
            Multiselect = true,
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileNames : new List<string>();
    }

    public bool Confirm(string message, string title)
        => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public void Info(string message, string title = "提示")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void Error(string message, string title = "错误")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public void OpenInExplorer(string path)
    {
        try
        {
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        catch (System.Exception ex)
        {
            Error($"无法打开文件位置: {ex.Message}");
        }
    }
}