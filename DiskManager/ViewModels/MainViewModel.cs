using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiskManager.Models;
using DiskManager.Services;

namespace DiskManager.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly DiskAnalysisService _diskAnalysis = new();
    private readonly DuplicateFileService _duplicates = new();
    private readonly DiskMonitorService _monitor;
    private readonly LogService _log;
    private readonly PathGuard _guard;
    private readonly IRecycleBinService _recycleBin;
    private readonly FileOperationService _files;
    private readonly CleanupService _cleanup;
    private readonly IDialogService _dialogs;
    private readonly SynchronizationContext? _sync;
    private readonly bool _ownsMonitor;

    private CancellationTokenSource? _cts;
    private Task? _activeTask;
    private bool _isBusy;
    private bool _isProgressIndeterminate;
    private double _progressValue;
    private string _statusText = "就绪";
    private string _trendText = string.Empty;
    private string _largeFileScanPath = "C:\\";
    private string _minFileSizeText = "100";
    private string _duplicateScanPath = "C:\\";
    private string? _selectedCleanupDrive;
    private int _selectedDeleteModeIndex;
    private int _selectedKeepStrategyIndex = 1;
    private int _selectedConflictIndex = 2;

    public MainViewModel(
        IDialogService? dialogs = null,
        IRecycleBinService? recycleBin = null,
        LogService? log = null,
        PathGuard? guard = null,
        DiskMonitorService? monitor = null)
    {
        _dialogs = dialogs ?? new WpfDialogService();
        _recycleBin = recycleBin ?? new RecycleBinService();
        _log = log ?? new LogService();
        _guard = guard ?? PathGuard.CreateDefault();
        _files = new FileOperationService(_guard, _recycleBin, _log);
        _cleanup = new CleanupService(_guard, _recycleBin);
        _sync = SynchronizationContext.Current;

        if (monitor == null)
        {
            _monitor = new DiskMonitorService();
            _ownsMonitor = true;
        }
        else
        {
            _monitor = monitor;
            _ownsMonitor = false;
        }

        RefreshDisksCommand = new RelayCommand(RefreshDisks);
        ScanLargeFilesCommand = new AsyncRelayCommand(ScanLargeFilesAsync);
        DeleteLargeFilesCommand = new AsyncRelayCommand(DeleteLargeFilesAsync);
        OpenFileLocationCommand = new RelayCommand(OpenFileLocation);
        ScanDuplicatesCommand = new AsyncRelayCommand(ScanDuplicatesAsync);
        DeleteDuplicatesCommand = new AsyncRelayCommand(DeleteDuplicatesAsync);
        ScanCleanupCommand = new AsyncRelayCommand(ScanCleanupAsync);
        DeleteCleanupCommand = new AsyncRelayCommand(DeleteCleanupAsync);
        ToggleCleanupSelectionCommand = new RelayCommand(ToggleCleanupSelection);
        AddFilesCommand = new RelayCommand(AddFiles);
        AddFolderCommand = new RelayCommand(AddFolder);
        ClearListCommand = new RelayCommand(ClearList);
        BatchCopyCommand = new AsyncRelayCommand(BatchCopyAsync);
        BatchMoveCommand = new AsyncRelayCommand(BatchMoveAsync);
        BatchDeleteCommand = new AsyncRelayCommand(BatchDeleteAsync);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);

        _log.LogAdded += OnLogAdded;
        _monitor.SnapshotCaptured += OnSnapshotCaptured;
        if (_ownsMonitor)
        {
            _monitor.StartMonitoring(30);
        }

        RefreshDisks();
        LoadLogs();
    }

    public ObservableCollection<DiskInfo> Disks { get; } = new();
    public ObservableCollection<OperationLog> Logs { get; } = new();
    public ObservableCollection<FileItem> LargeFiles { get; } = new();
    public ObservableCollection<DuplicateFileGroup> DuplicateGroups { get; } = new();
    public ObservableCollection<CleanupSuggestion> CleanupSuggestions { get; } = new();
    public ObservableCollection<FileItem> FileOperationItems { get; } = new();
    public ObservableCollection<string> Drives { get; } = new();

    public RelayCommand RefreshDisksCommand { get; }
    public AsyncRelayCommand ScanLargeFilesCommand { get; }
    public AsyncRelayCommand DeleteLargeFilesCommand { get; }
    public RelayCommand OpenFileLocationCommand { get; }
    public AsyncRelayCommand ScanDuplicatesCommand { get; }
    public AsyncRelayCommand DeleteDuplicatesCommand { get; }
    public AsyncRelayCommand ScanCleanupCommand { get; }
    public AsyncRelayCommand DeleteCleanupCommand { get; }
    public RelayCommand ToggleCleanupSelectionCommand { get; }
    public RelayCommand AddFilesCommand { get; }
    public RelayCommand AddFolderCommand { get; }
    public RelayCommand ClearListCommand { get; }
    public AsyncRelayCommand BatchCopyCommand { get; }
    public AsyncRelayCommand BatchMoveCommand { get; }
    public AsyncRelayCommand BatchDeleteCommand { get; }
    public RelayCommand CancelCommand { get; }

    public IReadOnlyList<string> DeleteModeOptions { get; } = new[] { "移到回收站（可恢复）", "永久删除（不可恢复）" };
    public IReadOnlyList<string> KeepStrategyOptions { get; } = new[] { "保留路径最短的副本", "保留最新的副本", "保留最旧的副本" };
    public IReadOnlyList<string> ConflictOptions { get; } = new[] { "覆盖同名文件", "跳过同名文件", "重命名新文件" };

    public string LargeFileScanPath
    {
        get => _largeFileScanPath;
        set => SetProperty(ref _largeFileScanPath, value);
    }

    public string MinFileSizeText
    {
        get => _minFileSizeText;
        set => SetProperty(ref _minFileSizeText, value);
    }

    public string DuplicateScanPath
    {
        get => _duplicateScanPath;
        set => SetProperty(ref _duplicateScanPath, value);
    }

    public string? SelectedCleanupDrive
    {
        get => _selectedCleanupDrive;
        set => SetProperty(ref _selectedCleanupDrive, value);
    }

    public int SelectedDeleteModeIndex
    {
        get => _selectedDeleteModeIndex;
        set => SetProperty(ref _selectedDeleteModeIndex, value);
    }

    public int SelectedKeepStrategyIndex
    {
        get => _selectedKeepStrategyIndex;
        set => SetProperty(ref _selectedKeepStrategyIndex, value);
    }

    public int SelectedConflictIndex
    {
        get => _selectedConflictIndex;
        set => SetProperty(ref _selectedConflictIndex, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string TrendText
    {
        get => _trendText;
        set => SetProperty(ref _trendText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                CancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        private set => SetProperty(ref _isProgressIndeterminate, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    private DeleteMode EffectiveDeleteMode
        => SelectedDeleteModeIndex == 1 ? DeleteMode.Permanent : DeleteMode.RecycleBin;

    private DuplicateKeepStrategy EffectiveKeepStrategy
        => SelectedKeepStrategyIndex switch
        {
            1 => DuplicateKeepStrategy.Newest,
            2 => DuplicateKeepStrategy.Oldest,
            _ => DuplicateKeepStrategy.ShortestPath
        };

    private ConflictPolicy EffectiveConflict
        => SelectedConflictIndex switch
        {
            0 => ConflictPolicy.Overwrite,
            1 => ConflictPolicy.Skip,
            _ => ConflictPolicy.Rename
        };

    private void RefreshDisks()
    {
        Disks.Clear();
        foreach (var disk in _diskAnalysis.GetAllDisks())
        {
            Disks.Add(disk);
        }

        var previous = SelectedCleanupDrive;
        Drives.Clear();
        foreach (var disk in Disks)
        {
            Drives.Add(disk.DriveLetter);
        }

        SelectedCleanupDrive = previous != null && Drives.Contains(previous)
            ? previous
            : Drives.FirstOrDefault();

        UpdateTrend();
        _log.Log("刷新磁盘信息", $"发现 {Disks.Count} 个磁盘", true);
    }

    private void UpdateTrend()
    {
        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var history = _monitor.GetHistoryForDrive(systemDrive, 7);
        if (history.Count < 2)
        {
            TrendText = "数据不足，无法计算趋势。请保持程序运行以收集更多数据。";
            return;
        }

        var trend = _monitor.GetTrendForDrive(systemDrive, 7);
        if (trend.IsIncreasing)
        {
            TrendText = $"磁盘使用趋势: {trend.DailyChangeFormatted}\n警告: 预计 {trend.EstimatedDaysUntilFull} 天后磁盘将满。";
        }
        else
        {
            TrendText = $"磁盘使用趋势: {trend.DailyChangeFormatted}\n良好: 磁盘空间正在增加或保持稳定。";
        }
    }

    private async Task ScanLargeFilesAsync()
    {
        if (!Directory.Exists(LargeFileScanPath))
        {
            _dialogs.Error("请输入有效的路径！");
            return;
        }

        if (!long.TryParse(MinFileSizeText, out var minSizeMb) || minSizeMb < 0)
        {
            _dialogs.Error("请输入有效的文件大小！");
            return;
        }

        await RunBusyAsync("正在扫描大文件...", async token =>
        {
            var progress = new Progress<int>(count => StatusText = $"正在扫描大文件... 已扫描 {count} 个文件");
            var items = await _diskAnalysis.FindLargeFilesAsync(
                LargeFileScanPath, minSizeMb * 1024 * 1024, 100, progress, token);

            LargeFiles.Clear();
            foreach (var item in items)
            {
                LargeFiles.Add(item);
            }

            StatusText = $"扫描完成，找到 {items.Count} 个大文件";
            _log.Log("扫描大文件",
                $"在 {LargeFileScanPath} 找到 {items.Count} 个大于 {minSizeMb}MB 的文件", true);
        }, indeterminate: true);
    }

    private async Task DeleteLargeFilesAsync()
    {
        var selected = LargeFiles.Where(file => file.IsSelected).ToList();
        if (selected.Count == 0)
        {
            _dialogs.Info("请先选择要删除的文件！");
            return;
        }

        if (!_dialogs.Confirm(
                $"确定要删除选中的 {selected.Count} 个文件吗？\n方式: {DeleteModeOptions[SelectedDeleteModeIndex]}",
                "确认删除"))
        {
            return;
        }

        await RunBusyAsync("正在删除文件...", async token =>
        {
            var progress = new Progress<int>(value =>
            {
                ProgressValue = value;
                StatusText = $"正在删除文件... {value}%";
            });

            var result = await _files.DeleteAsync(
                selected.Select(file => file.FullPath).ToList(), EffectiveDeleteMode, progress, token);

            foreach (var item in selected.ToList())
            {
                if (!File.Exists(item.FullPath) && !Directory.Exists(item.FullPath))
                {
                    LargeFiles.Remove(item);
                }
            }

            StatusText = $"删除完成，{result.Summary}";
            _dialogs.Info($"删除完成。\n{result.Summary}");
        });
    }

    private void OpenFileLocation(object? parameter)
    {
        if (parameter is not FileItem item)
        {
            _dialogs.Info("请先选择一个文件！");
            return;
        }

        _dialogs.OpenInExplorer(item.FullPath);
    }

    private async Task ScanDuplicatesAsync()
    {
        if (!Directory.Exists(DuplicateScanPath))
        {
            _dialogs.Error("请输入有效的路径！");
            return;
        }

        await RunBusyAsync("正在扫描重复文件...", async token =>
        {
            var progress = new Progress<int>(value =>
            {
                ProgressValue = value;
                StatusText = $"正在扫描重复文件... {value}%";
            });

            var groups = await _duplicates.FindDuplicatesAsync(DuplicateScanPath, progress, token);

            DuplicateGroups.Clear();
            foreach (var group in groups)
            {
                DuplicateGroups.Add(group);
            }

            var wasted = groups.Sum(group => group.TotalWastedSpace);
            var skipped = _duplicates.LastSkippedCloudFileCount;
            var suffix = skipped > 0 ? $"，跳过 {skipped} 个云端占位文件" : string.Empty;
            StatusText = $"扫描完成，找到 {groups.Count} 组重复文件，可节省 {Utils.SizeFormatter.Format(wasted)}{suffix}";
            _log.Log("扫描重复文件",
                $"在 {DuplicateScanPath} 找到 {groups.Count} 组重复文件，可节省 {Utils.SizeFormatter.Format(wasted)}", true);
        });
    }

    private async Task DeleteDuplicatesAsync()
    {
        if (DuplicateGroups.Count == 0)
        {
            _dialogs.Info("没有找到重复文件！");
            return;
        }

        var targets = DuplicateGroups
            .SelectMany(group => DuplicateFileService.SelectToDelete(group.Files, EffectiveKeepStrategy))
            .ToList();

        if (targets.Count == 0)
        {
            _dialogs.Info("没有需要删除的副本。");
            return;
        }

        if (!_dialogs.Confirm(
                $"将删除 {targets.Count} 个副本（每组保留一个）。\n方式: {DeleteModeOptions[SelectedDeleteModeIndex]}",
                "确认删除"))
        {
            return;
        }

        var targetSet = new HashSet<FileItem>(targets);

        await RunBusyAsync("正在删除重复文件...", async token =>
        {
            var progress = new Progress<int>(value =>
            {
                ProgressValue = value;
                StatusText = $"正在删除重复文件... {value}%";
            });

            var result = await _files.DeleteAsync(
                targets.Select(file => file.FullPath).ToList(), EffectiveDeleteMode, progress, token);

            foreach (var group in DuplicateGroups.ToList())
            {
                foreach (var file in group.Files.ToList())
                {
                    if (targetSet.Contains(file) && !File.Exists(file.FullPath))
                    {
                        group.Files.Remove(file);
                    }
                }

                group.NotifyFilesChanged();
                if (group.Files.Count <= 1)
                {
                    DuplicateGroups.Remove(group);
                }
            }

            StatusText = $"删除重复文件完成，{result.Summary}";
            _dialogs.Info($"删除完成。\n{result.Summary}");
        });
    }

    private async Task ScanCleanupAsync()
    {
        await RunBusyAsync("正在扫描清理建议...", async token =>
        {
            var progress = new Progress<int>(value =>
            {
                ProgressValue = value;
                StatusText = $"正在扫描清理建议... {value}%";
            });

            var suggestions = await _cleanup.GetCleanupSuggestionsAsync(SelectedCleanupDrive, progress, token);

            CleanupSuggestions.Clear();
            foreach (var suggestion in suggestions)
            {
                CleanupSuggestions.Add(suggestion);
            }

            var total = suggestions.Sum(suggestion => suggestion.TotalSize);
            StatusText = $"扫描完成，可清理 {Utils.SizeFormatter.Format(total)}";
            _log.Log("扫描清理建议", $"找到 {suggestions.Count} 项清理建议", true);
        });
    }

    private async Task DeleteCleanupAsync(object? parameter)
    {
        if (parameter is not CleanupSuggestion suggestion)
        {
            return;
        }

        if (suggestion.Kind == CleanupKind.RecycleBin)
        {
            if (suggestion.ItemCount == 0)
            {
                _dialogs.Info("回收站已经是空的。");
                return;
            }

            if (!_dialogs.Confirm("确定要清空回收站吗？此操作不可恢复。", "确认清空"))
            {
                return;
            }

            await RunBusyAsync("正在清空回收站...", async token =>
            {
                var driveRoot = CleanupService.NormalizeDriveRoot(SelectedCleanupDrive);
                var ok = await Task.Run(() => _recycleBin.Empty(driveRoot), token);
                _log.Log("清空回收站", ok ? "回收站已清空" : "清空回收站失败", ok);
                if (ok)
                {
                    CleanupSuggestions.Remove(suggestion);
                    StatusText = "回收站已清空";
                }
                else
                {
                    StatusText = "清空回收站失败";
                    _dialogs.Error("清空回收站失败，请确认权限。");
                }
            }, indeterminate: true);
            return;
        }

        var selected = suggestion.Files.Where(file => file.IsSelected).ToList();
        if (selected.Count == 0)
        {
            _dialogs.Info("请先选择要删除的文件！");
            return;
        }

        if (!_dialogs.Confirm(
                $"确定要删除选中的 {selected.Count} 个文件吗？\n方式: {DeleteModeOptions[SelectedDeleteModeIndex]}",
                "确认删除"))
        {
            return;
        }

        await RunBusyAsync("正在删除文件...", async token =>
        {
            var progress = new Progress<int>(value =>
            {
                ProgressValue = value;
                StatusText = $"正在删除文件... {value}%";
            });

            var result = await _files.DeleteAsync(
                selected.Select(file => file.FullPath).ToList(), EffectiveDeleteMode, progress, token);

            foreach (var file in selected.ToList())
            {
                if (!File.Exists(file.FullPath))
                {
                    suggestion.Files.Remove(file);
                    suggestion.TotalSize -= file.Size;
                }
            }

            suggestion.NotifyFilesChanged();
            StatusText = $"删除完成，{result.Summary}";
            _dialogs.Info($"删除完成。\n{result.Summary}");
        });
    }

    private void ToggleCleanupSelection(object? parameter)
    {
        if (parameter is not CleanupSuggestion suggestion || !suggestion.CanSelectFiles)
        {
            return;
        }

        var select = suggestion.Files.Any(file => !file.IsSelected);
        foreach (var file in suggestion.Files)
        {
            file.IsSelected = select;
        }
    }

    private void AddFiles()
    {
        var files = _dialogs.PickFiles("选择要操作的文件");
        foreach (var fileName in files)
        {
            try
            {
                var info = new FileInfo(fileName);
                FileOperationItems.Add(new FileItem
                {
                    FullPath = fileName,
                    Size = info.Length,
                    LastModified = info.LastWriteTime,
                    IsDirectory = false
                });
            }
            catch (Exception ex)
            {
                _log.Log("添加文件", fileName, false, ex.Message);
            }
        }
    }

    private void AddFolder()
    {
        var folder = _dialogs.PickFolder("选择要操作的文件夹");
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        try
        {
            var info = new DirectoryInfo(folder);
            FileOperationItems.Add(new FileItem
            {
                FullPath = folder,
                Size = 0,
                LastModified = info.LastWriteTime,
                IsDirectory = true
            });
        }
        catch (Exception ex)
        {
            _log.Log("添加文件夹", folder, false, ex.Message);
        }
    }

    private void ClearList() => FileOperationItems.Clear();

    private async Task BatchCopyAsync()
    {
        var selected = FileOperationItems.Where(file => file.IsSelected).ToList();
        if (selected.Count == 0)
        {
            _dialogs.Info("请先选择要复制的文件！");
            return;
        }

        var target = _dialogs.PickFolder("选择目标文件夹");
        if (string.IsNullOrEmpty(target))
        {
            return;
        }

        await RunBusyAsync("正在复制文件...", async token =>
        {
            var progress = new Progress<int>(value =>
            {
                ProgressValue = value;
                StatusText = $"正在复制文件... {value}%";
            });

            var result = await _files.CopyAsync(
                selected.Select(file => file.FullPath).ToList(), target, EffectiveConflict, progress, token);

            StatusText = $"复制完成，{result.Summary}";
            _dialogs.Info(result.AllSucceeded
                ? $"复制完成。\n{result.Summary}"
                : $"部分文件处理失败。\n{result.Summary}");
        });
    }

    private async Task BatchMoveAsync()
    {
        var selected = FileOperationItems.Where(file => file.IsSelected).ToList();
        if (selected.Count == 0)
        {
            _dialogs.Info("请先选择要移动的文件！");
            return;
        }

        var target = _dialogs.PickFolder("选择目标文件夹");
        if (string.IsNullOrEmpty(target))
        {
            return;
        }

        await RunBusyAsync("正在移动文件...", async token =>
        {
            var progress = new Progress<int>(value =>
            {
                ProgressValue = value;
                StatusText = $"正在移动文件... {value}%";
            });

            var result = await _files.MoveAsync(
                selected.Select(file => file.FullPath).ToList(), target, EffectiveConflict, progress, token);

            if (result.Succeeded > 0)
            {
                foreach (var item in selected.OfType<FileItem>().ToList())
                {
                    if (!File.Exists(item.FullPath) && !Directory.Exists(item.FullPath))
                    {
                        FileOperationItems.Remove(item);
                    }
                }
            }

            StatusText = $"移动完成，{result.Summary}";
            _dialogs.Info(result.AllSucceeded
                ? $"移动完成。\n{result.Summary}"
                : $"部分文件处理失败。\n{result.Summary}");
        });
    }

    private async Task BatchDeleteAsync()
    {
        var selected = FileOperationItems.Where(file => file.IsSelected).ToList();
        if (selected.Count == 0)
        {
            _dialogs.Info("请先选择要删除的文件！");
            return;
        }

        if (!_dialogs.Confirm(
                $"确定要删除选中的 {selected.Count} 个文件/文件夹吗？\n方式: {DeleteModeOptions[SelectedDeleteModeIndex]}",
                "确认删除"))
        {
            return;
        }

        await RunBusyAsync("正在删除文件...", async token =>
        {
            var progress = new Progress<int>(value =>
            {
                ProgressValue = value;
                StatusText = $"正在删除文件... {value}%";
            });

            var result = await _files.DeleteAsync(
                selected.Select(file => file.FullPath).ToList(), EffectiveDeleteMode, progress, token);

            foreach (var item in selected.ToList())
            {
                if (!File.Exists(item.FullPath) && !Directory.Exists(item.FullPath))
                {
                    FileOperationItems.Remove(item);
                }
            }

            StatusText = $"删除完成，{result.Summary}";
            _dialogs.Info($"删除完成。\n{result.Summary}");
        });
    }

    private void Cancel() => _cts?.Cancel();

    public async Task CancelAndWaitAsync(TimeSpan timeout)
    {
        var active = Interlocked.Exchange(ref _cts, null);
        active?.Cancel();

        var task = _activeTask;
        if (task != null)
        {
            try
            {
                await Task.WhenAny(task, Task.Delay(timeout));
            }
            catch (Exception)
            {
            }
        }

        active?.Dispose();
    }

    private async Task RunBusyAsync(string status, Func<CancellationToken, Task> work, bool indeterminate = false)
    {
        if (IsBusy)
        {
            _dialogs.Info("当前已有任务在执行，请稍候。");
            return;
        }

        IsBusy = true;
        StatusText = status;
        IsProgressIndeterminate = indeterminate;
        ProgressValue = 0;
        var active = new CancellationTokenSource();
        _cts = active;

        try
        {
            var workTask = work(active.Token);
            _activeTask = workTask;
            await workTask;
        }
        catch (OperationCanceledException)
        {
            StatusText = "操作已取消";
        }
        catch (Exception ex)
        {
            StatusText = "操作失败";
            _dialogs.Error(ex.Message);
            _log.Log(status, status, false, ex.Message);
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
            ProgressValue = 0;
            _activeTask = null;
            Interlocked.CompareExchange(ref _cts, null, active);
            active.Dispose();
        }
    }

    private void OnLogAdded(object? sender, OperationLog log)
        => Post(() =>
        {
            Logs.Insert(0, log);
            while (Logs.Count > 200)
            {
                Logs.RemoveAt(Logs.Count - 1);
            }
        });

    private void OnSnapshotCaptured(object? sender, IReadOnlyList<DiskInfo> disks)
        => Post(UpdateTrend);

    private void LoadLogs()
    {
        Logs.Clear();
        foreach (var log in _log.GetRecentLogs(100))
        {
            Logs.Add(log);
        }
    }

    private void Post(Action action)
    {
        if (_sync == null)
        {
            action();
            return;
        }

        _sync.Post(_ => action(), null);
    }

    public void Dispose()
    {
        _log.LogAdded -= OnLogAdded;
        _monitor.SnapshotCaptured -= OnSnapshotCaptured;
        if (_ownsMonitor)
        {
            _monitor.StopMonitoring();
        }

        var active = Interlocked.Exchange(ref _cts, null);
        active?.Cancel();
        active?.Dispose();
    }
}