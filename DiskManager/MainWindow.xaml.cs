using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DiskManager.Models;
using DiskManager.Services;
using DiskManager.ViewModels;

namespace DiskManager;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly WindowLayoutService _layoutService = new();
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        ApplyLayout();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
    }

    private void ApplyLayout()
    {
        var layout = _layoutService.Load();
        Width = layout.Width;
        Height = layout.Height;

        if (layout.Left.HasValue && layout.Top.HasValue)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = layout.Left.Value;
            Top = layout.Top.Value;
        }

        if (layout.Maximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void SaveLayout()
    {
        try
        {
            var bounds = WindowState == WindowState.Normal
                ? new Rect(Left, Top, ActualWidth, ActualHeight)
                : RestoreBounds;

            _layoutService.Save(new WindowLayout
            {
                Width = bounds.Width,
                Height = bounds.Height,
                Left = bounds.Left,
                Top = bounds.Top,
                Maximized = WindowState == WindowState.Maximized
            });
        }
        catch (Exception)
        {
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SaveLayout();

        if (_allowClose)
        {
            base.OnClosing(e);
            return;
        }

        if (!_viewModel.IsBusy)
        {
            _allowClose = true;
            base.OnClosing(e);
            return;
        }

        var confirm = MessageBox.Show(
            "当前有任务正在执行。退出会取消该任务，未完成的文件操作可能留下不完整的结果。\n确定要退出吗？",
            "确认退出",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;

        if (!confirm)
        {
            e.Cancel = true;
            return;
        }

        e.Cancel = true;
        _ = CloseAfterCancelAsync();
    }

    private async Task CloseAfterCancelAsync()
    {
        await _viewModel.CancelAndWaitAsync(TimeSpan.FromSeconds(8));
        _allowClose = true;
        Close();
    }

    private void LargeFilesHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is GridViewColumnHeader header && header.Content is string title)
        {
            var property = title switch
            {
                "文件名" => nameof(FileItem.Name),
                "路径" => nameof(FileItem.FullPath),
                "大小" => nameof(FileItem.Size),
                "修改时间" => nameof(FileItem.LastModified),
                _ => null
            };

            if (property != null)
            {
                _viewModel.SortLargeFiles(property);
            }
        }
    }

    private void FileOperationsHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is GridViewColumnHeader header && header.Content is string title)
        {
            var property = title switch
            {
                "文件名" => nameof(FileItem.Name),
                "路径" => nameof(FileItem.FullPath),
                "大小" => nameof(FileItem.Size),
                "类型" => nameof(FileItem.IsDirectory),
                _ => null
            };

            if (property != null)
            {
                _viewModel.SortFileOperations(property);
            }
        }
    }

    private void LargeFilesListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LargeFilesListView.SelectedItem is FileItem item)
        {
            _viewModel.OpenFileLocationCommand.Execute(item);
        }
    }

    private void FileOperationListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FileOperationListView.SelectedItem is FileItem item)
        {
            _viewModel.OpenFileLocationCommand.Execute(item);
        }
    }

    private void FileOperationListView_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void FileOperationListView_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            _viewModel.AddPaths(paths);
        }

        e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}