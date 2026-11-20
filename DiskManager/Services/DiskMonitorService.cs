using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using DiskManager.Models;
using DiskManager.Utils;
using Timer = System.Threading.Timer;

namespace DiskManager.Services;

public class DiskMonitorService
{
    private readonly object _gate = new();
    private readonly string? _dataDirectory;
    private readonly string? _historyFilePath;
    private readonly bool _filePersistenceEnabled;
    private readonly List<DiskSpaceHistory> _history = new();
    private Timer? _monitorTimer;
    private volatile bool _isMonitoring;

    public event EventHandler<IReadOnlyList<DiskInfo>>? SnapshotCaptured;

    public DiskMonitorService(string? dataDirectory = null)
    {
        try
        {
            _dataDirectory = dataDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DiskManager",
                "Data");

            Directory.CreateDirectory(_dataDirectory);
            _historyFilePath = Path.Combine(_dataDirectory, "DiskSpaceHistory.json");
            _filePersistenceEnabled = true;

            var loaded = LoadHistoryFromFile();
            lock (_gate)
            {
                _history.AddRange(loaded);
            }
        }
        catch (Exception)
        {
            _filePersistenceEnabled = false;
        }
    }

    public bool IsMonitoring => _isMonitoring;

    public void StartMonitoring(int intervalMinutes = 60)
    {
        if (_isMonitoring)
        {
            return;
        }

        _isMonitoring = true;
        var interval = TimeSpan.FromMinutes(Math.Max(1, intervalMinutes));
        _monitorTimer = new Timer(_ => CaptureSnapshot(), null, TimeSpan.Zero, interval);
    }

    public void StopMonitoring()
    {
        _isMonitoring = false;
        var timer = Interlocked.Exchange(ref _monitorTimer, null);
        timer?.Dispose();
    }

    public void CaptureSnapshot()
    {
        try
        {
            var disks = DiskEnumerator.GetReadyDisks();
            RecordSnapshot(disks);
            try
            {
                SnapshotCaptured?.Invoke(this, disks);
            }
            catch (Exception)
            {
            }
        }
        catch (Exception)
        {
        }
    }

    public void RecordSnapshot(IReadOnlyList<DiskInfo> disks)
    {
        var timestamp = DateTime.Now;
        var cutoff = timestamp.AddDays(-30);

        lock (_gate)
        {
            foreach (var disk in disks)
            {
                _history.Add(new DiskSpaceHistory
                {
                    Timestamp = timestamp,
                    DriveLetter = disk.DriveLetter,
                    TotalSize = disk.TotalSize,
                    UsedSpace = disk.UsedSpace,
                    FreeSpace = disk.FreeSpace
                });
            }

            if (_history.Count > 0)
            {
                _history.RemoveAll(h => h.Timestamp < cutoff);
            }
        }

        SaveHistoryToFile();
    }

    public List<DiskSpaceHistory> LoadHistory()
    {
        lock (_gate)
        {
            return new List<DiskSpaceHistory>(_history);
        }
    }

    public List<DiskSpaceHistory> GetHistoryForDrive(string driveLetter, int days = 30)
    {
        var since = DateTime.Now.AddDays(-days);
        lock (_gate)
        {
            return _history
                .Where(h => string.Equals(h.DriveLetter, driveLetter, StringComparison.OrdinalIgnoreCase)
                            && h.Timestamp >= since)
                .OrderBy(h => h.Timestamp)
                .ToList();
        }
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

        var origin = history[0].Timestamp;
        var xs = history.Select(h => (h.Timestamp - origin).TotalDays).ToArray();
        var ys = history.Select(h => (double)h.UsedSpace).ToArray();
        var meanX = xs.Average();
        var meanY = ys.Average();

        var numerator = 0d;
        var denominator = 0d;
        for (var index = 0; index < xs.Length; index++)
        {
            numerator += (xs[index] - meanX) * (ys[index] - meanY);
            denominator += (xs[index] - meanX) * (xs[index] - meanX);
        }

        var slope = denominator > 1e-18 ? numerator / denominator : 0d;
        var estimated = -1;
        if (slope > 0)
        {
            var free = history[^1].FreeSpace;
            var raw = free / slope;
            estimated = raw >= int.MaxValue ? int.MaxValue : (int)raw;
        }

        return new DiskSpaceTrend
        {
            IsIncreasing = slope > 0,
            DailyChangeBytes = (long)slope,
            EstimatedDaysUntilFull = estimated
        };
    }

    private List<DiskSpaceHistory> LoadHistoryFromFile()
    {
        if (!_filePersistenceEnabled || string.IsNullOrEmpty(_historyFilePath))
        {
            return new List<DiskSpaceHistory>();
        }

        try
        {
            if (!File.Exists(_historyFilePath))
            {
                return new List<DiskSpaceHistory>();
            }

            var json = File.ReadAllText(_historyFilePath);
            return JsonSerializer.Deserialize<List<DiskSpaceHistory>>(json) ?? new List<DiskSpaceHistory>();
        }
        catch (Exception)
        {
            return new List<DiskSpaceHistory>();
        }
    }

    private void SaveHistoryToFile()
    {
        if (!_filePersistenceEnabled || string.IsNullOrEmpty(_historyFilePath))
        {
            return;
        }

        try
        {
            List<DiskSpaceHistory> snapshot;
            lock (_gate)
            {
                snapshot = new List<DiskSpaceHistory>(_history);
            }

            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = _historyFilePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _historyFilePath, true);
        }
        catch (Exception)
        {
        }
    }
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
    public string DailyChangeFormatted => SizeFormatter.Format(Math.Abs(DailyChangeBytes)) + (IsIncreasing ? " /天 (增加)" : " /天 (减少)");
}