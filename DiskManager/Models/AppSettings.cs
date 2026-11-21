namespace DiskManager.Models;

public sealed class AppSettings
{
    public string LargeFileScanPath { get; set; } = "C:\\";
    public string MinFileSizeText { get; set; } = "100";
    public string DuplicateScanPath { get; set; } = "C:\\";
    public string? CleanupDrive { get; set; }
    public int DeleteModeIndex { get; set; }
    public int KeepStrategyIndex { get; set; } = 1;
    public int ConflictIndex { get; set; } = 2;
    public int SelectedTabIndex { get; set; }
    public bool UseDefaultExclusions { get; set; } = true;
    public string CustomExclusions { get; set; } = string.Empty;
    public string ExcludedExtensions { get; set; } = ".tmp;.log";
}