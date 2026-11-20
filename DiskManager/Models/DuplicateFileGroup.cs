using System;
using System.Collections.ObjectModel;
using DiskManager.Utils;

namespace DiskManager.Models;

public class DuplicateFileGroup : ObservableObject
{
    public string FileHash { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public ObservableCollection<FileItem> Files { get; } = new();

    public long TotalWastedSpace => FileSize * Math.Max(0, Files.Count - 1);
    public string Name => Files.Count > 0 ? Files[0].Name : "未知文件";
    public string FileSizeFormatted => SizeFormatter.Format(FileSize);
    public string TotalWastedSpaceFormatted => SizeFormatter.Format(TotalWastedSpace);

    public void NotifyFilesChanged()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(TotalWastedSpace));
        OnPropertyChanged(nameof(TotalWastedSpaceFormatted));
    }
}