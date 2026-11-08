using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DiskManager.Models;
using Timer = System.Threading.Timer;

namespace DiskManager.Services
{
    public class DiskMonitorService
    {
        private readonly string? _dataDirectory;
        private readonly string? _historyFilePath;
        private readonly bool _filePersistenceEnabled;
        private Timer? _monitorTimer;
        private bool _isMonitoring;
        private readonly List<DiskSpaceHistory> _inMemoryHistory = new();

        public event EventHandler<DiskInfo>? DiskSpaceChanged;
        public event EventHandler<List<DiskSpaceHistory>>? HistoryUpdated;

        public DiskMonitorService()
        {
            try
            {
                _dataDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DiskManager",
                    "Data");

                if (!Directory.Exists(_dataDirectory))
                {
                    Directory.CreateDirectory(_dataDirectory);
                }

                _historyFilePath = Path.Combine(_dataDirectory, "DiskSpaceHistory.json");
                _filePersistenceEnabled = true;
                
                var loadedHistory = LoadHistoryFromFile();
                _inMemoryHistory.AddRange(loadedHistory);
            }
            catch (Exception)
            {
                _filePersistenceEnabled = false;
            }
        }

        public void StartMonitoring(int intervalMinutes = 60)
        {
            if (_isMonitoring) return;

            _isMonitoring = true;
            var interval = TimeSpan.FromMinutes(intervalMinutes);
            _monitorTimer = new Timer(OnMonitorTick, null, TimeSpan.Zero, interval);
        }

        public void StopMonitoring()
        {
            _isMonitoring = false;
            _monitorTimer?.Dispose();
            _monitorTimer = null;
        }

        private void OnMonitorTick(object? state)
        {
            try
            {
                var disks = GetAllDisks();
                SaveDiskSpaceHistory(disks);

                foreach (var disk in disks)
                {
                    DiskSpaceChanged?.Invoke(this, disk);
                }
            }
            catch (Exception)
            {
            }
        }

        public List<DiskInfo> GetAllDisks()
        {
            var disks = new List<DiskInfo>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                {
                    disks.Add(new DiskInfo
                    {
                        DriveLetter = drive.Name,
                        Name = drive.VolumeLabel,
                        TotalSize = drive.TotalSize,
                        UsedSpace = drive.TotalSize - drive.AvailableFreeSpace,
                        FreeSpace = drive.AvailableFreeSpace
                    });
                }
            }
            return disks;
        }

        private void SaveDiskSpaceHistory(List<DiskInfo> disks)
        {
            try
            {
                var timestamp = DateTime.Now;

                foreach (var disk in disks)
                {
                    _inMemoryHistory.Add(new DiskSpaceHistory
                    {
                        Timestamp = timestamp,
                        DriveLetter = disk.DriveLetter,
                        TotalSize = disk.TotalSize,
                        UsedSpace = disk.UsedSpace,
                        FreeSpace = disk.FreeSpace
                    });
                }

                var thirtyDaysAgo = DateTime.Now.AddDays(-30);
                var historyToKeep = _inMemoryHistory.Where(h => h.Timestamp >= thirtyDaysAgo).ToList();
                _inMemoryHistory.Clear();
                _inMemoryHistory.AddRange(historyToKeep);

                if (_filePersistenceEnabled && !string.IsNullOrEmpty(_historyFilePath))
                {
                    SaveHistoryToFile(_inMemoryHistory);
                }

                HistoryUpdated?.Invoke(this, _inMemoryHistory);
            }
            catch (Exception)
            {
            }
        }

        private List<DiskSpaceHistory> LoadHistoryFromFile()
        {
            if (!_filePersistenceEnabled || string.IsNullOrEmpty(_historyFilePath))
            {
                return new List<DiskSpaceHistory>();
            }

            try
            {
                if (File.Exists(_historyFilePath))
                {
                    var json = File.ReadAllText(_historyFilePath);
                    return JsonSerializer.Deserialize<List<DiskSpaceHistory>>(json) ?? new List<DiskSpaceHistory>();
                }
            }
            catch (Exception)
            {
            }
            return new List<DiskSpaceHistory>();
        }

        private void SaveHistoryToFile(List<DiskSpaceHistory> history)
        {
            if (!_filePersistenceEnabled || string.IsNullOrEmpty(_historyFilePath))
            {
                return;
            }

            try
            {
                var json = JsonSerializer.Serialize(history, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(_historyFilePath, json);
            }
            catch (Exception)
            {
            }
        }

        public List<DiskSpaceHistory> LoadHistory()
        {
            return new List<DiskSpaceHistory>(_inMemoryHistory);
        }

        public List<DiskSpaceHistory> GetHistoryForDrive(string driveLetter, int days = 30)
        {
            var sinceDate = DateTime.Now.AddDays(-days);
            return _inMemoryHistory
                .Where(h => h.DriveLetter.Equals(driveLetter, StringComparison.OrdinalIgnoreCase) && 
                           h.Timestamp >= sinceDate)
                .OrderBy(h => h.Timestamp)
                .ToList();
        }

        public DiskSpaceTrend GetTrendForDrive(string driveLetter, int days = 7)
        {
            var history = GetHistoryForDrive(driveLetter, days);
            if (history.Count < 2)
            {
                return new DiskSpaceTrend
                {
                    IsIncreasing = false,
                    DailyChangeBytes = 0,
                    EstimatedDaysUntilFull = -1
                };
            }

            var first = history.First();
            var last = history.Last();
            var daysDiff = (last.Timestamp - first.Timestamp).TotalDays;
            var usedChange = last.UsedSpace - first.UsedSpace;
            var dailyChange = daysDiff > 0 ? usedChange / daysDiff : 0;

            var estimatedDaysUntilFull = -1;
            if (dailyChange > 0 && last.FreeSpace > 0)
            {
                estimatedDaysUntilFull = (int)(last.FreeSpace / dailyChange);
            }

            return new DiskSpaceTrend
            {
                IsIncreasing = dailyChange > 0,
                DailyChangeBytes = (long)dailyChange,
                EstimatedDaysUntilFull = estimatedDaysUntilFull
            };
        }

        public bool IsMonitoring => _isMonitoring;
    }

    public class DiskSpaceHistory
    {
        public DateTime Timestamp { get; set; }
        public string DriveLetter { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public long UsedSpace { get; set; }
        public long FreeSpace { get; set; }
    }

    public class DiskSpaceTrend
    {
        public bool IsIncreasing { get; set; }
        public long DailyChangeBytes { get; set; }
        public int EstimatedDaysUntilFull { get; set; }
        public string DailyChangeFormatted => FormatSize(Math.Abs(DailyChangeBytes)) + (IsIncreasing ? " /天 (增加)" : " /天 (减少)");

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
}
