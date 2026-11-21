using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiskManager.Models;

namespace DiskManager.Services;

public class LogService
{
    private const int MaxInMemoryLogs = 5000;

    private readonly object _gate = new();
    private readonly List<OperationLog> _logs = new();
    private readonly bool _fileLoggingEnabled;
    private readonly string? _logDirectory;
    private string? _logFilePath;
    private DateTime _logDate;

    public event EventHandler<OperationLog>? LogAdded;

    public LogService()
    {
        try
        {
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DiskManager",
                "Logs");

            Directory.CreateDirectory(_logDirectory);
            _fileLoggingEnabled = true;
        }
        catch (Exception)
        {
            _fileLoggingEnabled = false;
        }
    }

    public void Log(string operation, string details, bool isSuccess, string? errorMessage = null)
    {
        var log = new OperationLog
        {
            Timestamp = DateTime.Now,
            Operation = operation,
            Details = details,
            IsSuccess = isSuccess,
            ErrorMessage = errorMessage ?? string.Empty
        };

        lock (_gate)
        {
            _logs.Add(log);
            if (_logs.Count > MaxInMemoryLogs)
            {
                _logs.RemoveRange(0, _logs.Count - MaxInMemoryLogs);
            }

            WriteLogToFileLocked(log);
        }

        try
        {
            LogAdded?.Invoke(this, log);
        }
        catch (Exception)
        {
        }
    }

    private void WriteLogToFileLocked(OperationLog log)
    {
        if (!_fileLoggingEnabled || string.IsNullOrEmpty(_logDirectory))
        {
            return;
        }

        try
        {
            var today = DateTime.Now.Date;
            if (_logFilePath == null || today != _logDate)
            {
                _logDate = today;
                _logFilePath = Path.Combine(_logDirectory, $"DiskManager_{today:yyyy-MM-dd}.log");
            }

            var status = log.IsSuccess ? "成功" : "失败";
            var logLine = $"[{log.Timestamp:yyyy-MM-dd HH:mm:ss}] [{status}] {log.Operation}: {log.Details}";
            if (!string.IsNullOrEmpty(log.ErrorMessage))
            {
                logLine += $" - 错误: {log.ErrorMessage}";
            }

            File.AppendAllText(_logFilePath, logLine + Environment.NewLine);
        }
        catch (Exception)
        {
        }
    }

    public List<OperationLog> GetRecentLogs(int count = 100)
    {
        lock (_gate)
        {
            return _logs
                .OrderByDescending(l => l.Timestamp)
                .Take(count)
                .ToList();
        }
    }

    public List<OperationLog> GetAllLogsFromFile()
    {
        var logs = new List<OperationLog>();
        lock (_gate)
        {
            try
            {
                if (!string.IsNullOrEmpty(_logFilePath) && File.Exists(_logFilePath))
                {
                    foreach (var line in File.ReadAllLines(_logFilePath))
                    {
                        var log = ParseLogLine(line);
                        if (log != null)
                        {
                            logs.Add(log);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        return logs.OrderByDescending(l => l.Timestamp).ToList();
    }

    private static OperationLog? ParseLogLine(string line)
    {
        try
        {
            var parts = line.Split(new[] { " [" }, StringSplitOptions.None);
            if (parts.Length < 3)
            {
                return null;
            }

            var timestampStr = parts[0].Trim('[', ']');
            if (!DateTime.TryParse(timestampStr, out var timestamp))
            {
                return null;
            }

            var statusPart = parts[1].Split(']')[0];
            var isSuccess = statusPart == "成功";

            var messagePart = string.Join(" [", parts.Skip(2));
            var operationAndDetails = messagePart.Split(new[] { "]: " }, StringSplitOptions.None);
            if (operationAndDetails.Length < 2)
            {
                return null;
            }

            var operation = operationAndDetails[0];
            var details = operationAndDetails[1];

            var errorMessage = string.Empty;
            var errorIndex = details.IndexOf(" - 错误: ", StringComparison.Ordinal);
            if (errorIndex >= 0)
            {
                errorMessage = details.Substring(errorIndex + " - 错误: ".Length);
                details = details.Substring(0, errorIndex);
            }

            return new OperationLog
            {
                Timestamp = timestamp,
                Operation = operation,
                Details = details,
                IsSuccess = isSuccess,
                ErrorMessage = errorMessage
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void ClearLogs()
    {
        lock (_gate)
        {
            _logs.Clear();
            try
            {
                if (!string.IsNullOrEmpty(_logFilePath) && File.Exists(_logFilePath))
                {
                    File.Delete(_logFilePath);
                }
            }
            catch (Exception)
            {
            }
        }
    }

    public string? GetLogFilePath()
    {
        lock (_gate)
        {
            return _logFilePath;
        }
    }

    public string? GetLogDirectory() => _logDirectory;
}