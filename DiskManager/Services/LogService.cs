using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiskManager.Models;

namespace DiskManager.Services
{
    public class LogService
    {
        private readonly string? _logDirectory;
        private readonly string? _logFilePath;
        private readonly List<OperationLog> _logs = new();
        private readonly bool _fileLoggingEnabled;

        public event EventHandler<OperationLog>? LogAdded;

        public LogService()
        {
            try
            {
                _logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DiskManager",
                    "Logs");

                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }

                var today = DateTime.Now.ToString("yyyy-MM-dd");
                _logFilePath = Path.Combine(_logDirectory, $"DiskManager_{today}.log");
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

            _logs.Add(log);
            WriteLogToFile(log);
            LogAdded?.Invoke(this, log);
        }

        private void WriteLogToFile(OperationLog log)
        {
            if (!_fileLoggingEnabled || string.IsNullOrEmpty(_logFilePath))
            {
                return;
            }

            try
            {
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
            return _logs.OrderByDescending(l => l.Timestamp).Take(count).ToList();
        }

        public List<OperationLog> GetAllLogsFromFile()
        {
            var logs = new List<OperationLog>();
            try
            {
                if (File.Exists(_logFilePath))
                {
                    var lines = File.ReadAllLines(_logFilePath);
                    foreach (var line in lines)
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
            return logs.OrderByDescending(l => l.Timestamp).ToList();
        }

        private OperationLog? ParseLogLine(string line)
        {
            try
            {
                var parts = line.Split(new[] { " [" }, StringSplitOptions.None);
                if (parts.Length < 3) return null;

                var timestampStr = parts[0].Trim('[', ']');
                if (!DateTime.TryParse(timestampStr, out var timestamp)) return null;

                var statusPart = parts[1].Split(']')[0];
                var isSuccess = statusPart == "成功";

                var messagePart = string.Join(" [", parts.Skip(2));
                var operationAndDetails = messagePart.Split(new[] { "]: " }, StringSplitOptions.None);
                if (operationAndDetails.Length < 2) return null;

                var operation = operationAndDetails[0];
                var details = operationAndDetails[1];

                var errorMessage = string.Empty;
                var errorIndex = details.IndexOf(" - 错误: ");
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
            _logs.Clear();
            try
            {
                if (File.Exists(_logFilePath))
                {
                    File.Delete(_logFilePath);
                }
            }
            catch (Exception)
            {
            }
        }

        public string? GetLogFilePath()
        {
            return _logFilePath;
        }
    }
}
