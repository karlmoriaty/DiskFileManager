using System.Collections.Generic;
using System.Linq;

namespace DiskManager.Models;

public sealed class OperationResult
{
    private readonly List<string> _errors = new();

    public int Succeeded { get; private set; }
    public int Failed { get; private set; }
    public int Skipped { get; private set; }
    public bool Cancelled { get; private set; }
    public IReadOnlyList<string> Errors => _errors;

    public int Total => Succeeded + Failed + Skipped;
    public bool AllSucceeded => Failed == 0 && !Cancelled;

    public void MarkSucceeded() => Succeeded++;

    public void MarkSkipped() => Skipped++;

    public void MarkFailed(string error)
    {
        Failed++;
        if (_errors.Count < 100)
        {
            _errors.Add(error);
        }
    }

    public void MarkCancelled()
    {
        Cancelled = true;
    }

    public string Summary
    {
        get
        {
            var text = $"成功 {Succeeded}，失败 {Failed}，跳过 {Skipped}";
            if (Cancelled)
            {
                text += "，已取消";
            }

            if (_errors.Count > 0)
            {
                text += $"；首个错误: {_errors.First()}";
            }

            return text;
        }
    }
}