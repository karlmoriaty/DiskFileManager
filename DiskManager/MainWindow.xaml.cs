using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using DiskManager.ViewModels;

namespace DiskManager;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
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

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}