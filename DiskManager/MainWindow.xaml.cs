using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using DiskManager.Models;
using DiskManager.Services;
using FormsFolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using FormsDialogResult = System.Windows.Forms.DialogResult;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;
using WpfMessageBoxResult = System.Windows.MessageBoxResult;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfButton = System.Windows.Controls.Button;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace DiskManager;

public partial class MainWindow : Window
{
    private readonly DiskAnalysisService _diskAnalysisService;
    private readonly DuplicateFileService _duplicateFileService;
    private readonly CleanupService _cleanupService;
    private readonly FileOperationService _fileOperationService;
    private readonly LogService _logService;
    private readonly DiskMonitorService _diskMonitorService;

    private List<DiskInfo> _currentDisks = new();
    private List<FileItem> _currentLargeFiles = new();
    private List<DuplicateFileGroup> _currentDuplicates = new();
    private List<CleanupSuggestion> _currentCleanupSuggestions = new();
    private List<FileItem> _currentFileOperationItems = new();

    public MainWindow()
    {
        WriteStartupDiagnostic("MainWindow构造函数开始");
        
        try
        {
            WriteStartupDiagnostic("调用InitializeComponent");
            InitializeComponent();
            WriteStartupDiagnostic("InitializeComponent完成");

            WriteStartupDiagnostic("初始化DiskAnalysisService");
            _diskAnalysisService = new DiskAnalysisService();
            WriteStartupDiagnostic("DiskAnalysisService初始化完成");

            WriteStartupDiagnostic("初始化DuplicateFileService");
            _duplicateFileService = new DuplicateFileService();
            WriteStartupDiagnostic("DuplicateFileService初始化完成");

            WriteStartupDiagnostic("初始化CleanupService");
            _cleanupService = new CleanupService();
            WriteStartupDiagnostic("CleanupService初始化完成");

            WriteStartupDiagnostic("初始化LogService");
            _logService = new LogService();
            WriteStartupDiagnostic("LogService初始化完成");

            WriteStartupDiagnostic("初始化FileOperationService");
            _fileOperationService = new FileOperationService(_cleanupService, _logService);
            WriteStartupDiagnostic("FileOperationService初始化完成");

            WriteStartupDiagnostic("初始化DiskMonitorService");
            _diskMonitorService = new DiskMonitorService();
            WriteStartupDiagnostic("DiskMonitorService初始化完成");

            WriteStartupDiagnostic("注册事件处理器");
            Loaded += MainWindow_Loaded;
            _logService.LogAdded += LogService_LogAdded;
            WriteStartupDiagnostic("事件处理器注册完成");

            WriteStartupDiagnostic("启动磁盘监控");
            _diskMonitorService.StartMonitoring(30);
            WriteStartupDiagnostic("磁盘监控已启动");

            WriteStartupDiagnostic("MainWindow构造函数完成");
        }
        catch (Exception ex)
        {
            WriteStartupDiagnostic($"MainWindow构造函数异常: {ex}");
            throw;
        }
    }

    private void WriteStartupDiagnostic(string message)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] {message}");
            
            var tempPath = Path.GetTempPath();
            var diagnosticPath = Path.Combine(tempPath, "DiskManager_Diagnostics");
            if (!Directory.Exists(diagnosticPath))
            {
                Directory.CreateDirectory(diagnosticPath);
            }
            
            var logFile = Path.Combine(diagnosticPath, "mainwindow_startup.log");
            var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
            File.AppendAllText(logFile, logLine);
        }
        catch
        {
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshDiskInfo();
        LoadLogs();
    }

    private void RefreshDiskInfo()
    {
        _currentDisks = _diskAnalysisService.GetAllDisks();
        DiskListView.ItemsSource = _currentDisks;

        if (_currentDisks.Any())
        {
            var cDrive = _currentDisks.FirstOrDefault(d => d.DriveLetter.StartsWith("C:"));
            if (cDrive != null)
            {
                var trend = _diskMonitorService.GetTrendForDrive("C:");
                UpdateTrendDisplay(trend);
            }
        }

        _logService.Log("刷新磁盘信息", "成功刷新磁盘信息", true);
    }

    private void UpdateTrendDisplay(DiskSpaceTrend trend)
    {
        if (trend.EstimatedDaysUntilFull == -1)
        {
            TrendTextBlock.Text = "数据不足，无法计算趋势。请保持程序运行以收集更多数据。";
        }
        else
        {
            var trendText = $"磁盘使用趋势: {trend.DailyChangeFormatted}\n";
            if (trend.IsIncreasing)
            {
                trendText += $"⚠️ 警告: 磁盘空间正在减少！预计 {trend.EstimatedDaysUntilFull} 天后磁盘将满。";
            }
            else
            {
                trendText += "✅ 良好: 磁盘空间正在增加或保持稳定。";
            }
            TrendTextBlock.Text = trendText;
        }
    }

    private void LoadLogs()
    {
        var logs = _logService.GetRecentLogs(50);
        LogListView.ItemsSource = logs;
    }

    private void LogService_LogAdded(object? sender, OperationLog e)
    {
        Dispatcher.Invoke(() =>
        {
            LoadLogs();
        });
    }

    private async void ScanLargeFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var path = LargeFileScanPath.Text;
        if (!Directory.Exists(path))
        {
            WpfMessageBox.Show("请输入有效的路径！", "错误", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
            return;
        }

        if (!long.TryParse(MinFileSizeMB.Text, out var minSizeMB))
        {
            WpfMessageBox.Show("请输入有效的文件大小！", "错误", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
            return;
        }

        var minSizeBytes = minSizeMB * 1024 * 1024;
        StatusTextBlock.Text = "正在扫描大文件...";
        MainProgressBar.Visibility = Visibility.Visible;
        MainProgressBar.IsIndeterminate = true;

        try
        {
            _currentLargeFiles = await Task.Run(() => _diskAnalysisService.FindLargeFiles(path, minSizeBytes));
            LargeFilesListView.ItemsSource = _currentLargeFiles;
            StatusTextBlock.Text = $"扫描完成，找到 {_currentLargeFiles.Count} 个大文件";
            _logService.Log("扫描大文件", $"在 {path} 找到 {_currentLargeFiles.Count} 个大于 {minSizeMB}MB 的文件", true);
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = "扫描失败";
            WpfMessageBox.Show($"扫描失败: {ex.Message}", "错误", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
            _logService.Log("扫描大文件", $"扫描 {path} 失败", false, ex.Message);
        }
        finally
        {
            MainProgressBar.Visibility = Visibility.Collapsed;
            MainProgressBar.IsIndeterminate = false;
        }
    }

    private void DeleteLargeFileButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedFiles = LargeFilesListView.SelectedItems.Cast<FileItem>().ToList();
        if (!selectedFiles.Any())
        {
            WpfMessageBox.Show("请先选择要删除的文件！", "提示", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
            return;
        }

        var result = WpfMessageBox.Show(
            $"确定要删除选中的 {selectedFiles.Count} 个文件吗？此操作不可恢复！",
            "确认删除",
            WpfMessageBoxButton.YesNo,
            WpfMessageBoxImage.Warning);

        if (result != WpfMessageBoxResult.Yes) return;

        var successCount = 0;
        foreach (var file in selectedFiles)
        {
            if (_cleanupService.DeleteFile(file.FullPath))
            {
                successCount++;
                _currentLargeFiles.Remove(file);
            }
        }

        LargeFilesListView.ItemsSource = null;
        LargeFilesListView.ItemsSource = _currentLargeFiles;
        WpfMessageBox.Show($"成功删除 {successCount} 个文件！", "完成", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
    }

    private void OpenFileLocationButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedFile = LargeFilesListView.SelectedItem as FileItem;
        if (selectedFile == null)
        {
            WpfMessageBox.Show("请先选择一个文件！", "提示", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start("explorer.exe", $"/select,\"{selectedFile.FullPath}\"");
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"无法打开文件位置: {ex.Message}", "错误", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
        }
    }

    private async void ScanDuplicatesButton_Click(object sender, RoutedEventArgs e)
    {
        var path = DuplicateScanPath.Text;
        if (!Directory.Exists(path))
        {
            WpfMessageBox.Show("请输入有效的路径！", "错误", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
            return;
        }

        StatusTextBlock.Text = "正在扫描重复文件...";
        DuplicateScanProgressBar.Visibility = Visibility.Visible;
        DuplicateScanStatus.Visibility = Visibility.Visible;
        DuplicateScanStatus.Text = "正在收集文件...";

        var progress = new Progress<int>(p =>
        {
            DuplicateScanProgressBar.Value = p;
            DuplicateScanStatus.Text = p < 25 ? "正在收集文件..." : 
                                           p < 100 ? "正在计算哈希值..." : "完成";
        });

        try
        {
            _currentDuplicates = await _duplicateFileService.FindDuplicatesAsync(path, progress);
            DuplicateFilesTreeView.ItemsSource = _currentDuplicates;
            StatusTextBlock.Text = $"扫描完成，找到 {_currentDuplicates.Count} 组重复文件";
            
            var totalWasted = _currentDuplicates.Sum(d => d.TotalWastedSpace);
            _logService.Log("扫描重复文件", 
                $"在 {path} 找到 {_currentDuplicates.Count} 组重复文件，可节省 {FormatSize(totalWasted)}", true);
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = "扫描失败";
            WpfMessageBox.Show($"扫描失败: {ex.Message}", "错误", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
            _logService.Log("扫描重复文件", $"扫描 {path} 失败", false, ex.Message);
        }
        finally
        {
            DuplicateScanProgressBar.Visibility = Visibility.Collapsed;
            DuplicateScanStatus.Visibility = Visibility.Collapsed;
        }
    }

    private void DeleteDuplicatesButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_currentDuplicates.Any())
        {
            WpfMessageBox.Show("没有找到重复文件！", "提示", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
            return;
        }

        var result = WpfMessageBox.Show(
            "确定要删除重复文件吗？每组将保留一个文件，删除其他副本。此操作不可恢复！",
            "确认删除",
            WpfMessageBoxButton.YesNo,
            WpfMessageBoxImage.Warning);

        if (result != WpfMessageBoxResult.Yes) return;

        var deletedCount = 0;
        var savedSpace = 0L;

        foreach (var group in _currentDuplicates)
        {
            if (group.Files.Count <= 1) continue;

            var filesToDelete = group.Files.Skip(1).ToList();
            foreach (var file in filesToDelete)
            {
                if (_cleanupService.DeleteFile(file.FullPath))
                {
                    deletedCount++;
                    savedSpace += file.Size;
                }
            }
        }

        WpfMessageBox.Show($"成功删除 {deletedCount} 个重复文件，节省 {FormatSize(savedSpace)}！", 
            "完成", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
        
        ScanDuplicatesButton_Click(sender, e);
    }

    private void ScanCleanupButton_Click(object sender, RoutedEventArgs e)
    {
        StatusTextBlock.Text = "正在扫描清理建议...";
        MainProgressBar.Visibility = Visibility.Visible;
        MainProgressBar.IsIndeterminate = true;

        try
        {
            _currentCleanupSuggestions = _cleanupService.GetCleanupSuggestions("C:");
            CleanupSuggestionsItemsControl.ItemsSource = _currentCleanupSuggestions;
            
            var totalSize = _currentCleanupSuggestions.Sum(s => s.TotalSize);
            StatusTextBlock.Text = $"扫描完成，可清理 {FormatSize(totalSize)}";
            _logService.Log("扫描清理建议", $"找到 {_currentCleanupSuggestions.Count} 项清理建议", true);
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = "扫描失败";
            WpfMessageBox.Show($"扫描失败: {ex.Message}", "错误", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
        }
        finally
        {
            MainProgressBar.Visibility = Visibility.Collapsed;
            MainProgressBar.IsIndeterminate = false;
        }
    }

    private void CleanupSelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfCheckBox checkBox && checkBox.DataContext is CleanupSuggestion suggestion)
        {
            foreach (var file in suggestion.Files)
            {
                file.IsSelected = checkBox.IsChecked ?? false;
            }
        }
    }

    private void CleanupDeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton button && button.Tag is CleanupSuggestion suggestion)
        {
            var selectedFiles = suggestion.Files.Where(f => f.IsSelected).ToList();
            if (!selectedFiles.Any())
            {
                WpfMessageBox.Show("请先选择要删除的文件！", "提示", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
                return;
            }

            var result = WpfMessageBox.Show(
                $"确定要删除选中的 {selectedFiles.Count} 个文件吗？此操作不可恢复！",
                "确认删除",
                WpfMessageBoxButton.YesNo,
                WpfMessageBoxImage.Warning);

            if (result != WpfMessageBoxResult.Yes) return;

            var successCount = 0;
            foreach (var file in selectedFiles.ToList())
            {
                if (_cleanupService.DeleteFile(file.FullPath))
                {
                    successCount++;
                    suggestion.Files.Remove(file);
                    suggestion.TotalSize -= file.Size;
                }
            }

            CleanupSuggestionsItemsControl.ItemsSource = null;
            CleanupSuggestionsItemsControl.ItemsSource = _currentCleanupSuggestions;
            WpfMessageBox.Show($"成功删除 {successCount} 个文件！", "完成", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
        }
    }

    private void AddFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WpfOpenFileDialog
        {
            Multiselect = true,
            Title = "选择要操作的文件"
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var fileName in dialog.FileNames)
            {
                var fileInfo = new FileInfo(fileName);
                _currentFileOperationItems.Add(new FileItem
                {
                    FullPath = fileName,
                    Size = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    IsDirectory = false
                });
            }
            UpdateFileOperationList();
        }
    }

    private void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FormsFolderBrowserDialog();
        dialog.Description = "选择要操作的文件夹";
        
        if (dialog.ShowDialog() == FormsDialogResult.OK)
        {
            var dirInfo = new DirectoryInfo(dialog.SelectedPath);
            _currentFileOperationItems.Add(new FileItem
            {
                FullPath = dialog.SelectedPath,
                Size = 0,
                LastModified = dirInfo.LastWriteTime,
                IsDirectory = true
            });
            UpdateFileOperationList();
        }
    }

    private void ClearListButton_Click(object sender, RoutedEventArgs e)
    {
        _currentFileOperationItems.Clear();
        UpdateFileOperationList();
    }

    private void UpdateFileOperationList()
    {
        FileOperationListView.ItemsSource = null;
        FileOperationListView.ItemsSource = _currentFileOperationItems;
    }

    private async void BatchCopyButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = _currentFileOperationItems.Where(f => f.IsSelected).ToList();
        if (!selectedItems.Any())
        {
            WpfMessageBox.Show("请先选择要复制的文件！", "提示", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
            return;
        }

        using var dialog = new FormsFolderBrowserDialog();
        dialog.Description = "选择目标文件夹";
        
        if (dialog.ShowDialog() != FormsDialogResult.OK) return;

        StatusTextBlock.Text = "正在复制文件...";
        MainProgressBar.Visibility = Visibility.Visible;

        var progress = new Progress<int>(p => MainProgressBar.Value = p);
        var sourcePaths = selectedItems.Select(i => i.FullPath).ToList();

        try
        {
            var result = await _fileOperationService.BatchCopyAsync(sourcePaths, dialog.SelectedPath, progress);
            StatusTextBlock.Text = result ? "复制完成" : "复制过程中部分文件失败";
            WpfMessageBox.Show(result ? "复制完成！" : "复制过程中部分文件失败，请查看日志。", 
                "完成", WpfMessageBoxButton.OK, result ? WpfMessageBoxImage.Information : WpfMessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = "复制失败";
            WpfMessageBox.Show($"复制失败: {ex.Message}", "错误", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
        }
        finally
        {
            MainProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void BatchMoveButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = _currentFileOperationItems.Where(f => f.IsSelected).ToList();
        if (!selectedItems.Any())
        {
            WpfMessageBox.Show("请先选择要移动的文件！", "提示", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
            return;
        }

        using var dialog = new FormsFolderBrowserDialog();
        dialog.Description = "选择目标文件夹";
        
        if (dialog.ShowDialog() != FormsDialogResult.OK) return;

        StatusTextBlock.Text = "正在移动文件...";
        MainProgressBar.Visibility = Visibility.Visible;

        var progress = new Progress<int>(p => MainProgressBar.Value = p);
        var sourcePaths = selectedItems.Select(i => i.FullPath).ToList();

        try
        {
            var result = await _fileOperationService.BatchMoveAsync(sourcePaths, dialog.SelectedPath, progress);
            StatusTextBlock.Text = result ? "移动完成" : "移动过程中部分文件失败";
            WpfMessageBox.Show(result ? "移动完成！" : "移动过程中部分文件失败，请查看日志。", 
                "完成", WpfMessageBoxButton.OK, result ? WpfMessageBoxImage.Information : WpfMessageBoxImage.Warning);
            
            if (result)
            {
                _currentFileOperationItems.RemoveAll(i => i.IsSelected);
                UpdateFileOperationList();
            }
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = "移动失败";
            WpfMessageBox.Show($"移动失败: {ex.Message}", "错误", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
        }
        finally
        {
            MainProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void BatchDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = _currentFileOperationItems.Where(f => f.IsSelected).ToList();
        if (!selectedItems.Any())
        {
            WpfMessageBox.Show("请先选择要删除的文件！", "提示", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
            return;
        }

        var result = WpfMessageBox.Show(
            $"确定要删除选中的 {selectedItems.Count} 个文件/文件夹吗？此操作不可恢复！",
            "确认删除",
            WpfMessageBoxButton.YesNo,
            WpfMessageBoxImage.Warning);

        if (result != WpfMessageBoxResult.Yes) return;

        StatusTextBlock.Text = "正在删除文件...";
        MainProgressBar.Visibility = Visibility.Visible;

        var progress = new Progress<int>(p => MainProgressBar.Value = p);
        var filePaths = selectedItems.Select(i => i.FullPath).ToList();

        try
        {
            var deleteResult = await _fileOperationService.BatchDeleteAsync(filePaths, progress);
            StatusTextBlock.Text = deleteResult ? "删除完成" : "删除过程中部分文件失败";
            WpfMessageBox.Show(deleteResult ? "删除完成！" : "删除过程中部分文件失败，请查看日志。", 
                "完成", WpfMessageBoxButton.OK, deleteResult ? WpfMessageBoxImage.Information : WpfMessageBoxImage.Warning);
            
            if (deleteResult)
            {
                _currentFileOperationItems.RemoveAll(i => i.IsSelected);
                UpdateFileOperationList();
            }
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = "删除失败";
            WpfMessageBox.Show($"删除失败: {ex.Message}", "错误", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
        }
        finally
        {
            MainProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private void RefreshDiskButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshDiskInfo();
    }

    protected override void OnClosed(EventArgs e)
    {
        _diskMonitorService.StopMonitoring();
        base.OnClosed(e);
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
