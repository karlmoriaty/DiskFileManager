using System;
using System.Diagnostics;

namespace DiskManager.Services;

public sealed class ThrottledProgress
{
    private readonly IProgress<int>? _inner;
    private readonly int _minIntervalMs;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private int _lastReported = -1;

    public ThrottledProgress(IProgress<int>? inner, int minIntervalMs = 100)
    {
        _inner = inner;
        _minIntervalMs = minIntervalMs;
    }

    public void Report(int value, bool force = false)
    {
        if (_inner == null)
        {
            return;
        }

        var clamped = Math.Clamp(value, 0, 100);
        if (!force)
        {
            if (clamped == _lastReported && clamped != 100)
            {
                return;
            }

            if (_stopwatch.ElapsedMilliseconds < _minIntervalMs && clamped != 100)
            {
                return;
            }
        }

        _lastReported = clamped;
        _stopwatch.Restart();
        _inner.Report(clamped);
    }
}