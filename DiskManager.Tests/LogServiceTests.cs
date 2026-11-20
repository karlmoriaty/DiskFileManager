using System.Linq;
using System.Threading.Tasks;
using DiskManager.Services;

namespace DiskManager.Tests;

public class LogServiceTests
{
    [Fact]
    public void Log_StoresEntryAndExposesIt()
    {
        var service = new LogService();

        service.Log("扫描", "测试详情", true);

        Assert.Contains(service.GetRecentLogs(10),
            entry => entry.Operation == "扫描" && entry.Details == "测试详情" && entry.IsSuccess);
    }

    [Fact]
    public async Task ConcurrentWritingAndReading_DoesNotThrow()
    {
        var service = new LogService();

        var writers = Enumerable.Range(0, 6).Select(worker => Task.Run(() =>
        {
            for (var index = 0; index < 120; index++)
            {
                service.Log("并发", $"{worker}-{index}", true);
            }
        }));

        var readers = Enumerable.Range(0, 4).Select(reader => Task.Run(() =>
        {
            for (var index = 0; index < 120; index++)
            {
                _ = service.GetRecentLogs(50);
            }
        }));

        await Task.WhenAll(writers.Concat(readers));

        Assert.True(service.GetRecentLogs(5000).Count > 0);
    }

    [Fact]
    public void InMemoryBuffer_IsCapped()
    {
        var service = new LogService();

        for (var index = 0; index < 5100; index++)
        {
            service.Log("容量", index.ToString(), true);
        }

        Assert.Equal(5000, service.GetRecentLogs(10000).Count);
    }
}