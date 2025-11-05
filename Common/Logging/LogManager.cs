using Common.Interfaces.Logging;
using Common.Models.Entities;
using System.Diagnostics;

namespace Common.Logging
{
    public class LogManager : ISystemLogger
    {
        private static readonly Lazy<LogManager> _instance = new Lazy<LogManager>(() => new LogManager());
        public static LogManager Instance => _instance.Value;

        #region Fields

        private readonly List<LogEntry> _logEntries = new List<LogEntry>();
        private readonly object _lock = new object();
        private readonly string _logFilePath;

        #endregion

        #region Properties

        /// <summary>
        /// Maximum logs to keep in memory
        /// </summary>
        public int MaxLogEntries { get; set; } = 1000;

        /// <summary>
        /// Path to current log file
        /// </summary>
        public string LogFilePath => _logFilePath;

        #endregion
        public event Action<LogEntry> OnLogAdded;

        private LogManager()
        {
            // Setup log file
            string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(logFolder);

            string logFileName = $"UITestKit_{DateTime.Now:yyyyMMdd}.log";
            _logFilePath = Path.Combine(logFolder, logFileName);

            // Write header
            WriteToFile($"=== Log Session Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            WriteToFile($"User: {Environment.UserName}");
            WriteToFile(new string('=', 80));
        }

        public void LogInfomation(string message)
        {
            Log(message, LogLevel.Information);
        }

        public void LogCritical(string message)
        {
            Log(message, LogLevel.Critical);
        }

        public void LogDebug(string message)
        {
            Log(message, LogLevel.Debug);
        }

        public void LogError(string message)
        {
            Log(message, LogLevel.Error);
        }

        public void LogWarning(string message)
        {
            Log(message, LogLevel.Warning);
        }

        public LogEntry[] GetAllLogs()
        {
            lock (_lock)
            {
                return _logEntries.ToArray();
            }
        }

        public LogEntry[] GetLogsByLevel(LogLevel level)
        {
            lock (_lock)
            {
                return _logEntries.Where(log => log.Level == level).ToArray();
            }
        }

        public void ClearLogs()
        {
            lock (_lock)
            {
                _logEntries.Clear();
            }
        }
        #region Private Methods
        private void Log(string message, LogLevel level)
        {
            try
            {
                // Get calling method info
                var stackTrace = new StackTrace(2, true);
                var frame = stackTrace.GetFrame(0);
                string source = frame?.GetMethod()?.DeclaringType?.Name ?? "Unknown";

                // Create log entry
                var logEntry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = level,
                    Message = message,
                    Source = source,
                    ThreadId = Thread.CurrentThread.ManagedThreadId
                };

                // 1. Add to memory (thread-safe)
                lock (_lock)
                {
                    _logEntries.Add(logEntry);

                    // Trim if exceeds max
                    while (_logEntries.Count > MaxLogEntries)
                    {
                        _logEntries.RemoveAt(0);
                    }
                }

                // 2. Write to file
                WriteToFile(FormatLogLine(logEntry));

                // 3. Write to Debug console
                Debug.WriteLine(FormatLogLine(logEntry));

                // 4. Raise event for UI
                OnLogAdded?.Invoke(logEntry);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LogManager] Error: {ex.Message}");
            }
        }

        private string FormatLogLine(LogEntry logEntry)
        {
            return $"[{logEntry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] " +
                   $"[{logEntry.Level,-11}] " +
                   $"[Thread-{logEntry.ThreadId:D3}] " +
                   $"[{logEntry.Source}] " +
                   $"{logEntry.Message}";
        }

        private void WriteToFile(string line)
        {
            //try
            //{
            //    File.AppendAllText(_logFilePath, line + Environment.NewLine);
            //}
            //catch (Exception ex)
            //{
            //    Debug.WriteLine($"[LogManager] File write error: {ex.Message}");
            //}
        }

#endregion
    }

}

