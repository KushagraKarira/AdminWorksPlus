using System;

namespace AdminWorks.Models
{
    public enum LogLevel
    {
        Info,
        Exec,
        Success,
        Warning,
        Error
    }

    public record LogEntry(DateTime Timestamp, LogLevel Level, string Message)
    {
        public string FormattedTime => Timestamp.ToString("HH:mm:ss");
        public string Header => $"[{FormattedTime}] [{Level.ToString().ToUpper().PadRight(7)}]";
    }
}
