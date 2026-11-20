using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiskManager.Models;
using DiskManager.Services;

namespace DiskManager.Tests;

public class DiskMonitorServiceTests : IDisposable
{
    private readonly string _dataDirectory;

    public DiskMonitorServiceTests()
    {
        _dataDirectory = Path.Combine(Path.GetTempPath(), "DiskManagerTests", "monitor", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dataDirectory, true);
        }
        catch (Exception)
        {
        }
    }

    [Fact]
    public void RecordSnapshot_TrendBecomesIncreasing()
    {
        var service = new DiskMonitorService(_dataDirectory);

        service.RecordSnapshot(new[] { new DiskInfo { DriveLetter = "C:", TotalSize = 1000, UsedSpace = 100, FreeSpace = 900 } });
        Thread.Sleep(40);
        service.RecordSnapshot(new[] { new DiskInfo { DriveLetter = "C:", TotalSize = 1000, UsedSpace = 200, FreeSpace = 800 } });

        var trend = service.GetTrendForDrive("C:", 7);

        Assert.True(trend.IsIncreasing);
        Assert.True(trend.DailyChangeBytes > 0);
        Assert.True(trend.EstimatedDaysUntilFull >= 0);
    }

    [Fact]
    public void Trend_WithSingleSample_IsUnknown()
    {
        var service = new DiskMonitorService(_dataDirectory);
        service.RecordSnapshot(new[] { new DiskInfo { DriveLetter = "C:", TotalSize = 1000, UsedSpace = 100, FreeSpace = 900 } });

        var trend = service.GetTrendForDrive("C:", 7);

        Assert.False(trend.IsIncreasing);
        Assert.Equal(-1, trend.EstimatedDaysUntilFull);
    }

    [Fact]
    public async Task ConcurrentSnapshotsAndReads_DoNotThrow()
    {
        var service = new DiskMonitorService(_dataDirectory);

        var writers = Enumerable.Range(0, 5).Select(worker => Task.Run(() =>
        {
            for (var index = 0; index < 40; index++)
            {
                service.RecordSnapshot(new[]
                {
                    new DiskInfo
                    {
                        DriveLetter = "C:",
                        TotalSize = 1000,
                        UsedSpace = 100 + worker + index,
                        FreeSpace = 900 - worker - index
                    }
                });
            }
        }));

        var readers = Enumerable.Range(0, 4).Select(reader => Task.Run(() =>
        {
            for (var index = 0; index < 120; index++)
            {
                _ = service.GetTrendForDrive("C:", 7);
                _ = service.LoadHistory();
            }
        }));

        await Task.WhenAll(writers.Concat(readers));

        Assert.True(service.LoadHistory().Count > 0);
    }

    [Fact]
    public void History_IsPersistedAndReloaded()
    {
        var first = new DiskMonitorService(_dataDirectory);
        first.RecordSnapshot(new[] { new DiskInfo { DriveLetter = "C:", TotalSize = 500, UsedSpace = 50, FreeSpace = 450 } });

        var second = new DiskMonitorService(_dataDirectory);

        Assert.Single(second.LoadHistory());
    }
}