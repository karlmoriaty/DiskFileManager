using System.Collections.ObjectModel;
using DiskManager.Utils;

namespace DiskManager.Models;

public class CleanupSuggestion : ObservableObject
{
    private long _totalSize;
    private int _itemCount;

    public CleanupKind Kind { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsSafeToDelete { get; init; }
    public ObservableCollection<FileItem> Files { get; } = new();

    public long TotalSize
    {
        get => _totalSize;
        set
        {
            if (SetProperty(ref _totalSize, value))
            {
                OnPropertyChanged(nameof(TotalSizeFormatted));
            }
        }
    }

    public int ItemCount
    {
        get => _itemCount;
        set
        {
            if (SetProperty(ref _itemCount, value))
            {
                OnPropertyChanged(nameof(FileCount));
                OnPropertyChanged(nameof(HasItems));
            }
        }
    }

    public int FileCount => Kind == CleanupKind.RecycleBin ? ItemCount : Files.Count;
    public bool HasItems => ItemCount > 0 || Files.Count > 0;
    public bool CanSelectFiles => Kind != CleanupKind.RecycleBin;
    public string TotalSizeFormatted => SizeFormatter.Format(TotalSize);
    public string ActionText => Kind == CleanupKind.RecycleBin ? "清空回收站" : "删除选中文件";

    public void NotifyFilesChanged()
    {
        OnPropertyChanged(nameof(FileCount));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(TotalSizeFormatted));
    }
}