using System;
using System.IO;
using DiskManager.Utils;

namespace DiskManager.Models;

public class FileItem : ObservableObject
{
    private bool _isSelected;

    public string FullPath { get; set; } = string.Empty;
    public string Name => Path.GetFileName(FullPath.TrimEnd(Path.DirectorySeparatorChar));
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string Extension => Path.GetExtension(FullPath).ToLowerInvariant();
    public bool IsDirectory { get; set; }
    public string SizeFormatted => SizeFormatter.Format(Size);

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}