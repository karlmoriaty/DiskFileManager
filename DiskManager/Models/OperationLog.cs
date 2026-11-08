using System;

namespace DiskManager.Models
{
    public class OperationLog
    {
        public DateTime Timestamp { get; set; }
        public string Operation { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
