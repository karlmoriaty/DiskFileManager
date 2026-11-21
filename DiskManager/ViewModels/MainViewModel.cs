using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
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

    private readonly SettingsService _settingsService;
    private readonly SelectionSummary _largeFilesSummary;
    private readonly SelectionSummary _fileOperationsSummary;
    private string? _restoredCleanupDrive;
    private long _cleanupTotalBytes;
    private string _lastCleanupScanText = "尚未扫描";
    private int _selectedTabIndex;
    private bool _isDetailPanelOpen;

    private const long PermanentConfirmThresholdBytes = 1024L * 1024 * 1024;
    private string? _selectedRecentTarget;

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
    private string _largeFileSearchText = string.Empty;
    private string _fileOperationSearchText = string.Empty;
    private string _customExclusions = string.Empty;
    private bool _useDefaultExclusions = true;
    private string _excludedExtensions = ".tmp;.log";
    private ICollectionView? _largeFilesView;
    private ICollectionView? _fileOperationsView;

    public MainViewModel(
        IDialogService? dialogs = null,
        IRecycleBinService? recycleBin = null,
        LogService? log = null,
        PathGuard? guard = null,
        DiskMonitorService? monitor = null,
        SettingsService? settingsService = null,
        AppSettings? settings = null,
        bool autoRefreshDisks = true)
    {
        _dialogs = dialogs ?? new WpfDialogService();
        _recycleBin = recycleBin ?? new RecycleBinService();
        _log = log ?? new LogService();
        _guard = guard ?? PathGuard.CreateDefault();
        _files = new FileOperationService(_guard, _recycleBin, _log);
        _cleanup = new CleanupService(_guard, _recycleBin);
        _sync = SynchronizationContext.Current;
        _settingsService = settingsService ?? new SettingsService();
        var saved = settings ?? _settingsService.Load();
        _largeFileScanPath = saved.LargeFileScanPath;
        _minFileSizeText = saved.MinFileSizeText;
        _duplicateScanPath = saved.DuplicateScanPath;
        _selectedDeleteModeIndex = saved.DeleteModeIndex;
        _selectedKeepStrategyIndex = saved.KeepStrategyIndex;
        _selectedConflictIndex = saved.ConflictIndex;
        _selectedTabIndex = saved.SelectedTabIndex;
        _restoredCleanupDrive = saved.CleanupDrive;
        _useDefaultExclusions = saved.UseDefaultExclusions;
        _customExclusions = saved.CustomExclusions;
        _excludedExtensions = saved.ExcludedExtensions;

        foreach (var target in saved.RecentTargets
                     .Where(Directory.Exists)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Take(5))
        {
            RecentTargets.Add(target);
        }

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

        RefreshDisksCommand = new AsyncRelayCommand(RefreshDisksAsync);
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
        BrowseLargeFilePathCommand = new RelayCommand(BrowseLargeFilePath);
        BrowseDuplicatePathCommand = new RelayCommand(BrowseDuplicatePath);
        SelectAllLargeFilesCommand = new RelayCommand(SelectAllLargeFiles);
        SelectAllFileOperationsCommand = new RelayCommand(SelectAllFileOperations);
        RemoveSelectedFileOperationsCommand = new RelayCommand(RemoveSelectedFileOperations);
        ToggleAllDuplicateFilesCommand = new RelayCommand(ToggleAllDuplicateFiles);
        ExportLargeFilesCommand = new RelayCommand(ExportLargeFiles);
        ExportDuplicatesCommand = new RelayCommand(ExportDuplicates);
        OpenRecycleBinCommand = new RelayCommand(() => _dialogs.OpenRecycleBin());
        OpenLogFolderCommand = new RelayCommand(OpenLogFolder);
        OpenAboutCommand = new RelayCommand(() => _dialogs.ShowAbout());
        ToggleDetailPanelCommand = new RelayCommand(() => IsDetailPanelOpen = !IsDetailPanelOpen);
        BrowseTargetFolderCommand = new RelayCommand(BrowseTargetFolder);

        _largeFilesSummary = new SelectionSummary(LargeFiles);
        _fileOperationsSummary = new SelectionSummary(FileOperationItems);
        _largeFilesSummary.Changed += (_, _) => OnPropertyChanged(nameof(LargeFilesSummary));
        _fileOperationsSummary.Changed += (_, _) => OnPropertyChanged(nameof(FileOperationsSummary));

        _log.LogAdded += OnLogAdded;
        _monitor.SnapshotCaptured += OnSnapshotCaptured;
        if (_ownsMonitor)
        {
            _monitor.StartMonitoring(30);
        }

        if (autoRefreshDisks)
        {
            _ = RefreshDisksAsync();
        }

        LoadLogs();
    }

    public ObservableCollection<DiskInfo> Disks { get; } = new();
    public ObservableCollection<OperationLog> Logs { get; } = new();
    public ObservableCollection<FileItem> LargeFiles { get; } = new();
    public ObservableCollection<DuplicateFileGroup> DuplicateGroups { get; } = new();
    public ObservableCollection<CleanupSuggestion> CleanupSuggestions { get; } = new();
    public ObservableCollection<FileItem> FileOperationItems { get; } = new();
    public ObservableCollection<string> Drives { get; } = new();

    public AsyncRelayCommand RefreshDisksCommand { get; }
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
    public RelayCommand BrowseLargeFilePathCommand { get; }
    public RelayCommand BrowseDuplicatePathCommand { get; }
    public RelayCommand SelectAllLargeFilesCommand { get; }
    public RelayCommand SelectAllFileOperationsCommand { get; }
    public RelayCommand RemoveSelectedFileOperationsCommand { get; }
    public RelayCommand ToggleAllDuplicateFilesCommand { get; }
    public RelayCommand ExportLargeFilesCommand { get; }
    public RelayCommand ExportDuplicatesCommand { get; }
    public RelayCommand OpenRecycleBinCommand { get; }
    public RelayCommand OpenLogFolderCommand { get; }
    public RelayCommand OpenAboutCommand { get; }
    public RelayCommand ToggleDetailPanelCommand { get; }
    public RelayCommand BrowseTargetFolderCommand { get; }

    public IReadOnlyList<string> DeleteModeOptions { get; } = new[] { "移到回收站（可恢复）", "永久删除（不可恢复）" };
    public IReadOnlyList<string> KeepStrategyOptions { get; } = new[] { "保留路径最短的副本", "保留最新的副本", "保留最旧的副本" };
    public IReadOnlyList<string> ConflictOptions { get; } = new[] { "覆盖同名文件", "跳过同名文件", "重命名新文件" };

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public bool IsDetailPanelOpen
    {
        get => _isDetailPanelOpen;
        set => SetProperty(ref _isDetailPanelOpen, value);
    }

    public string VersionText { get; } =
        "v" + (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0");

    public string LargeFilesSummary => LargeFiles.Count == 0
        ? "尚未扫描"
        : $"共 {_largeFilesSummary.TotalCount} 个，合计 {Utils.SizeFormatter.Format(_largeFilesSummary.TotalSize)}；已选 {_largeFilesSummary.SelectedCount} 个 / {Utils.SizeFormatter.Format(_largeFilesSummary.SelectedSize)}";

    public string FileOperationsSummary => FileOperationItems.Count == 0
        ? "列表为空"
        : $"共 {_fileOperationsSummary.TotalCount} 项，合计 {Utils.SizeFormatter.Format(_fileOperationsSummary.TotalSize)}；已选 {_fileOperationsSummary.SelectedCount} 项 / {Utils.SizeFormatter.Format(_fileOperationsSummary.SelectedSize)}";

    public string CleanupTotalFormatted => Utils.SizeFormatter.Format(_cleanupTotalBytes);

    public string LastCleanupScanText
    {
        get => _lastCleanupScanText;
        private set => SetProperty(ref _lastCleanupScanText, value);
    }

    public ObservableCollection<string> RecentTargets { get; } = new();

    public string? SelectedRecentTarget
    {
        get => _selectedRecentTarget;
        set => SetProperty(ref _selectedRecentTarget, value);
    }

    public ICollectionView LargeFilesView
    {
        get
        {
            if (_largeFilesView == null)
            {
                _largeFilesView = CollectionViewSource.GetDefaultView(LargeFiles);
                _largeFilesView.Filter = FilterLargeFile;
            }

            return _largeFilesView;
        }
    }

    public ICollectionView FileOperationsView
    {
        get
        {
            if (_fileOperationsView == null)
            {
                _fileOperationsView = CollectionViewSource.GetDefaultView(FileOperationItems);
                _fileOperationsView.Filter = FilterFileOperation;
            }

            return _fileOperationsView;
        }
    }

    public string LargeFileSearchText
    {
        get => _largeFileSearchText;
        set
        {
            if (SetProperty(ref _largeFileSearchText, value))
            {
                LargeFilesView.Refresh();
            }
        }
    }

    public string FileOperationSearchText
    {
        get => _fileOperationSearchText;
        set
        {
            if (SetProperty(ref _fileOperationSearchText, value))
            {
                FileOperationsView.Refresh();
            }
        }
    }

    public bool UseDefaultExclusions
    {
        get => _useDefaultExclusions;
        set => SetProperty(ref _useDefaultExclusions, value);
    }

    public string CustomExclusions
    {
        get => _customExclusions;
        set => SetProperty(ref _customExclusions, value);
    }

    public string ExcludedExtensions
    {
        get => _excludedExtensions;
        set => SetProperty(ref _excludedExtensions, value);
    }

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

    internal async Task RefreshDisksAsync()
    {
        StatusText = "正在检测磁盘...";
        var disks = await Task.Run(() => _diskAnalysis.GetAllDisks());

        Disks.Clear();
        foreach (var disk in disks)
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
            : _restoredCleanupDrive != null && Drives.Contains(_restoredCleanupDrive)
                ? _restoredCleanupDrive
                : Drives.FirstOrDefault();
        _restoredCleanupDrive = null;

        UpdateTrend();
        StatusText = $"就绪，已检测到 {Disks.Count} 个磁盘";
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
                LargeFileScanPath, minSizeMb * 1024 * 1024, 100, progress, BuildScanFilters(), token);

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

        if (!ConfirmDeletion(
                selected.Count,
                selected.Sum(file => file.Size),
                $"确定要删除选中的 {selected.Count} 个文件吗？\n方式: {DeleteModeOptions[SelectedDeleteModeIndex]}"))
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
            ShowPermissionHint();
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

            var groups = await _duplicates.FindDuplicatesAsync(DuplicateScanPath, progress, BuildScanFilters(), token);

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

        var plan = new List<(DuplicateFileGroup Group, FileItem Keep, List<FileItem> Delete)>();
        var manualSelected = 0;

        foreach (var group in DuplicateGroups)
        {
            var selected = group.Files.Where(file => file.IsSelected).ToList();
            FileItem? keep;
            List<FileItem> delete;

            if (selected.Count == 0 || selected.Count >= group.Files.Count)
            {
                keep = DuplicateFileService.SelectKeep(group.Files, EffectiveKeepStrategy);
                delete = keep == null
                    ? new List<FileItem>()
                    : group.Files.Where(file => !ReferenceEquals(file, keep)).ToList();
            }
            else
            {
                delete = selected;
                keep = group.Files.FirstOrDefault(file => !selected.Contains(file));
                manualSelected += selected.Count;
            }

            if (keep != null && delete.Count > 0)
            {
                plan.Add((group, keep, delete));
            }
        }

        var targets = plan.SelectMany(item => item.Delete).Distinct().ToList();

        if (targets.Count == 0)
        {
            _dialogs.Info("没有需要删除的副本。");
            return;
        }

        var previewLines = plan.Take(5).Select(item =>
            $"- {item.Group.Name}: 保留 {item.Keep.FullPath}，删除 {item.Delete.Count} 个");
        var preview = string.Join(Environment.NewLine, previewLines);
        if (plan.Count > 5)
        {
            preview += $"{Environment.NewLine}… 其余 {plan.Count - 5} 组略";
        }

        if (!ConfirmDeletion(
                targets.Count,
                targets.Sum(file => file.Size),
                $"将删除 {targets.Count} 个副本，涉及 {plan.Count} 组（每组至少保留一个{(manualSelected > 0 ? $"，手动选择 {manualSelected} 个" : string.Empty)}）。\n\n预览:\n{preview}\n\n方式: {DeleteModeOptions[SelectedDeleteModeIndex]}"))
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
            ShowPermissionHint();
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
            _cleanupTotalBytes = total;
            OnPropertyChanged(nameof(CleanupTotalFormatted));
            LastCleanupScanText = $"上次扫描: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
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

        if (!ConfirmDeletion(
                selected.Count,
                selected.Sum(file => file.Size),
                $"确定要删除选中的 {selected.Count} 个文件吗？\n方式: {DeleteModeOptions[SelectedDeleteModeIndex]}"))
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
            ShowPermissionHint();
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
        AddPaths(files);
    }

    private void AddFolder()
    {
        var folder = _dialogs.PickFolder("选择要操作的文件夹");
        if (!string.IsNullOrEmpty(folder))
        {
            AddPaths(new[] { folder });
        }
    }

    public void AddPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                AddDirectoryItem(path);
            }
            else if (File.Exists(path))
            {
                AddFileItem(path);
            }
        }
    }

    private void AddFileItem(string path)
    {
        try
        {
            var info = new FileInfo(path);
            FileOperationItems.Add(new FileItem
            {
                FullPath = path,
                Size = info.Length,
                LastModified = info.LastWriteTime,
                IsDirectory = false
            });
        }
        catch (Exception ex)
        {
            _log.Log("添加文件", path, false, ex.Message);
        }
    }

    private void AddDirectoryItem(string path)
    {
        try
        {
            var info = new DirectoryInfo(path);
            var item = new FileItem
            {
                FullPath = path,
                Size = 0,
                LastModified = info.LastWriteTime,
                IsDirectory = true
            };

            FileOperationItems.Add(item);

            _ = Task.Run(() =>
            {
                var size = DiskAnalysisService.GetDirectorySize(path);
                Post(() => item.Size = size);
            });
        }
        catch (Exception ex)
        {
            _log.Log("添加文件夹", path, false, ex.Message);
        }
    }

    private void ClearList() => FileOperationItems.Clear();

    private ScanFilters BuildScanFilters()
        => ScanFilters.Create(UseDefaultExclusions, CustomExclusions, ExcludedExtensions);

    public void SortLargeFiles(string property) => ApplySort(LargeFilesView, property);

    public void SortFileOperations(string property) => ApplySort(FileOperationsView, property);

    private static void ApplySort(ICollectionView view, string property)
    {
        var direction = ListSortDirection.Ascending;
        if (view.SortDescriptions.Count > 0
            && view.SortDescriptions[0].PropertyName == property
            && view.SortDescriptions[0].Direction == ListSortDirection.Ascending)
        {
            direction = ListSortDirection.Descending;
        }

        using (view.DeferRefresh())
        {
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(property, direction));
        }
    }

    private bool FilterLargeFile(object item)
        => item is FileItem file && MatchesSearch(file, LargeFileSearchText);

    private bool FilterFileOperation(object item)
        => item is FileItem file && MatchesSearch(file, FileOperationSearchText);

    private static bool MatchesSearch(FileItem file, string text)
        => string.IsNullOrWhiteSpace(text)
           || file.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
           || file.FullPath.Contains(text, StringComparison.OrdinalIgnoreCase);

    private void BrowseLargeFilePath()
    {
        var folder = _dialogs.PickFolder("选择大文件扫描目录");
        if (!string.IsNullOrEmpty(folder))
        {
            LargeFileScanPath = folder;
        }
    }

    private void BrowseDuplicatePath()
    {
        var folder = _dialogs.PickFolder("选择重复文件扫描目录");
        if (!string.IsNullOrEmpty(folder))
        {
            DuplicateScanPath = folder;
        }
    }

    private void BrowseTargetFolder()
    {
        var folder = _dialogs.PickFolder("选择默认目标文件夹");
        if (!string.IsNullOrEmpty(folder))
        {
            AddRecentTarget(folder);
        }
    }

    private string? ResolveTargetFolder()
    {
        if (!string.IsNullOrWhiteSpace(SelectedRecentTarget) && Directory.Exists(SelectedRecentTarget))
        {
            return SelectedRecentTarget;
        }

        var folder = _dialogs.PickFolder("选择目标文件夹");
        if (string.IsNullOrEmpty(folder))
        {
            return null;
        }

        AddRecentTarget(folder);
        return folder;
    }

    internal void AddRecentTarget(string folder)
    {
        var existing = RecentTargets.FirstOrDefault(item =>
            string.Equals(item, folder, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            RecentTargets.Remove(existing);
        }

        RecentTargets.Insert(0, folder);
        while (RecentTargets.Count > 5)
        {
            RecentTargets.RemoveAt(RecentTargets.Count - 1);
        }

        SelectedRecentTarget = folder;
    }

    private void SelectAllLargeFiles()
    {
        var select = LargeFiles.Any(item => !item.IsSelected);
        foreach (var item in LargeFiles)
        {
            item.IsSelected = select;
        }
    }

    private void SelectAllFileOperations()
    {
        var select = FileOperationItems.Any(item => !item.IsSelected);
        foreach (var item in FileOperationItems)
        {
            item.IsSelected = select;
        }
    }

    private void RemoveSelectedFileOperations()
    {
        foreach (var item in FileOperationItems.Where(item => item.IsSelected).ToList())
        {
            FileOperationItems.Remove(item);
        }
    }

    private void ToggleAllDuplicateFiles()
    {
        var files = DuplicateGroups.SelectMany(group => group.Files).ToList();
        var select = files.Any(file => !file.IsSelected);
        foreach (var file in files)
        {
            file.IsSelected = select;
        }
    }

    private void ExportLargeFiles()
    {
        if (LargeFiles.Count == 0)
        {
            _dialogs.Info("没有可导出的结果，请先扫描大文件。");
            return;
        }

        var path = _dialogs.PickSaveFile("导出大文件结果", "large-files.csv", "CSV 文件|*.csv");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            var lines = new List<string> { "路径,大小(字节),大小,修改时间" };
            lines.AddRange(LargeFiles.Select(item =>
                $"{EscapeCsv(item.FullPath)},{item.Size},{EscapeCsv(item.SizeFormatted)},{EscapeCsv(item.LastModified.ToString("yyyy-MM-dd HH:mm:ss"))}"));

            File.WriteAllLines(path, lines, new System.Text.UTF8Encoding(true));
            StatusText = $"已导出 {LargeFiles.Count} 条到 {path}";
            _log.Log("导出大文件", path, true);
        }
        catch (Exception ex)
        {
            _dialogs.Error(ex.Message);
            _log.Log("导出大文件", path, false, ex.Message);
        }
    }

    private void ExportDuplicates()
    {
        if (DuplicateGroups.Count == 0)
        {
            _dialogs.Info("没有可导出的结果，请先扫描重复文件。");
            return;
        }

        var path = _dialogs.PickSaveFile("导出重复文件结果", "duplicates.csv", "CSV 文件|*.csv");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            var lines = new List<string> { "哈希,单文件大小,总浪费空间,路径" };
            foreach (var group in DuplicateGroups)
            {
                foreach (var file in group.Files)
                {
                    lines.Add($"{EscapeCsv(group.FileHash)},{group.FileSize},{group.TotalWastedSpace},{EscapeCsv(file.FullPath)}");
                }
            }

            File.WriteAllLines(path, lines, new System.Text.UTF8Encoding(true));
            StatusText = $"已导出 {DuplicateGroups.Count} 组到 {path}";
            _log.Log("导出重复文件", path, true);
        }
        catch (Exception ex)
        {
            _dialogs.Error(ex.Message);
            _log.Log("导出重复文件", path, false, ex.Message);
        }
    }

    private static string EscapeCsv(string value)
        => "\"" + value.Replace("\"", "\"\"") + "\"";

    private void OpenLogFolder()
    {
        var directory = _log.GetLogDirectory();
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            _dialogs.Info("日志目录尚未创建。");
            return;
        }

        _dialogs.OpenFolder(directory);
    }

    private void SaveSettings()
    {
        _settingsService.Save(new AppSettings
        {
            LargeFileScanPath = LargeFileScanPath,
            MinFileSizeText = MinFileSizeText,
            DuplicateScanPath = DuplicateScanPath,
            CleanupDrive = SelectedCleanupDrive,
            DeleteModeIndex = SelectedDeleteModeIndex,
            KeepStrategyIndex = SelectedKeepStrategyIndex,
            ConflictIndex = SelectedConflictIndex,
            SelectedTabIndex = SelectedTabIndex,
            UseDefaultExclusions = UseDefaultExclusions,
            CustomExclusions = CustomExclusions,
            ExcludedExtensions = ExcludedExtensions,
            RecentTargets = RecentTargets.ToList()
        });
    }

    private async Task BatchCopyAsync()
    {
        var selected = FileOperationItems.Where(file => file.IsSelected).ToList();
        if (selected.Count == 0)
        {
            _dialogs.Info("请先选择要复制的文件！");
            return;
        }

        var target = ResolveTargetFolder();
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
            ShowPermissionHint();
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

        var target = ResolveTargetFolder();
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
            ShowPermissionHint();
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

        if (!ConfirmDeletion(
                selected.Count,
                selected.Sum(file => file.Size),
                $"确定要删除选中的 {selected.Count} 个文件/文件夹吗？\n方式: {DeleteModeOptions[SelectedDeleteModeIndex]}"))
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
            ShowPermissionHint();
        });
    }

    internal bool ConfirmDeletion(int count, long totalSize, string message)
    {
        if (EffectiveDeleteMode != DeleteMode.Permanent || totalSize < PermanentConfirmThresholdBytes)
        {
            return _dialogs.Confirm(message, "确认删除");
        }

        var sizeText = Utils.SizeFormatter.Format(totalSize);
        if (!_dialogs.Confirm(
                $"{message}{Environment.NewLine}{Environment.NewLine}即将永久删除 {count} 项，共 {sizeText}，不会进入回收站。是否继续？",
                "永久删除确认"))
        {
            return false;
        }

        return _dialogs.Confirm(
            $"再次确认：{sizeText} 数据将永久丢失且无法恢复。{Environment.NewLine}确定删除吗？",
            "最终确认");
    }

    private void ShowPermissionHint()
    {
        if (_files.LastRunHadPermissionIssue)
        {
            _dialogs.Info("部分操作因权限不足失败。可关闭程序后使用“以管理员身份打开”重试。", "权限提示");
        }
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

        try
        {
            _sync.Post(_ =>
            {
                try
                {
                    action();
                }
                catch (Exception)
                {
                }
            }, null);
        }
        catch (Exception)
        {
        }
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

        _largeFilesSummary.Dispose();
        _fileOperationsSummary.Dispose();
        SaveSettings();
    }
}