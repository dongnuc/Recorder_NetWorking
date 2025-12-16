using Common.Interfaces.Logging;
using Common.Models.Entities;
using System.Diagnostics;

namespace Common.Logging
{
    public class LogManager : ISystemLogger
    {
        #region Fields

        private readonly List<LogEntry> _logEntries = new List<LogEntry>();
        private readonly object _lock = new object();
        private readonly string _logFilePath;
        private readonly string _sessionId;

        #endregion

        #region Properties

        public int MaxLogEntries { get; set; } = 1000;
        public string LogFilePath => _logFilePath;
        public string SessionId => _sessionId;

        /// <summary>
        /// ✅ UPDATE: Tính toán đường dẫn thư mục Log tập trung
        /// </summary>
        private static string LogFolderPath
        {
            get
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;

#if DEBUG
                // Khi Debug: Move từ bin/Debug/netX.0/ ra 3 cấp để về thư mục Project
                // Kết quả: TênProject/Logs/
                string debugPath = Path.Combine(basePath, "../../../../Logs");
                return Path.GetFullPath(debugPath);
#else
                // Khi Release/Publish: Để thư mục Logs ngay cạnh file .exe
                return Path.Combine(basePath, "Logs");
#endif
            }
        }

        #endregion

        public event Action<LogEntry>? OnLogAdded;

        public LogManager()
        {
            _sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // ✅ UPDATE: Sử dụng Property LogFolderPath đã sửa ở trên
            string logFolder = LogFolderPath;

            if (!Directory.Exists(logFolder))
            {
                Directory.CreateDirectory(logFolder);
            }

            string logFileName = $"UITestKit_{_sessionId}.log";
            _logFilePath = Path.Combine(logFolder, logFileName);

            // Write header...
            WriteToFile($"╔═══════════════════════════════════════════════════════════════════════════════╗");
            WriteToFile($"║ UITestKit Log Session");
            WriteToFile($"║ Session ID: {_sessionId}");
            WriteToFile($"║ Started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            WriteToFile($"║ Log File: {_logFilePath}");
            WriteToFile($"╚═══════════════════════════════════════════════════════════════════════════════╝");
            WriteToFile(string.Empty);

            Debug.WriteLine($"[LogManager] Log file created: {_logFilePath}");
        }

        #region ISystemLogger Implementation
        // (Giữ nguyên code cũ)
        public void LogInfomation(string message) => Log(message, LogLevel.Information);
        public void LogCritical(string message) => Log(message, LogLevel.Critical);
        public void LogDebug(string message) => Log(message, LogLevel.Debug);
        public void LogError(string message) => Log(message, LogLevel.Error);
        public void LogWarning(string message) => Log(message, LogLevel.Warning);
        #endregion

        #region Public Methods
        // (Giữ nguyên code cũ: GetAllLogs, GetLogsByLevel...)
        public LogEntry[] GetAllLogs() { lock (_lock) return _logEntries.ToArray(); }
        public LogEntry[] GetLogsByLevel(LogLevel level) { lock (_lock) return _logEntries.Where(log => log.Level == level).ToArray(); }

        public void ClearLogs()
        {
            lock (_lock) { _logEntries.Clear(); }
            WriteToFile(string.Empty);
            WriteToFile($">>> Logs cleared at {DateTime.Now:yyyy-MM-dd HH:mm:ss} <<<");
        }

        public void CloseSession()
        {
            WriteToFile(string.Empty);
            WriteToFile($"╔═══════════════════════════════════════════════════════════════════════════════╗");
            WriteToFile($"║ Log Session Ended: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            WriteToFile($"╚═══════════════════════════════════════════════════════════════════════════════╝");
        }
        #endregion

        #region Private Methods
        // (Giữ nguyên code cũ: Log, FormatLogLine, WriteToFile...)
        private void Log(string message, LogLevel level)
        {
            try
            {
                var stackTrace = new StackTrace(2, true);
                var frame = stackTrace.GetFrame(0);
                string source = frame?.GetMethod()?.DeclaringType?.Name ?? "Unknown";

                var logEntry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = level,
                    Message = message,
                    Source = source,
                    ThreadId = Thread.CurrentThread.ManagedThreadId
                };

                lock (_lock)
                {
                    _logEntries.Add(logEntry);
                    while (_logEntries.Count > MaxLogEntries) _logEntries.RemoveAt(0);
                }

                WriteToFile(FormatLogLine(logEntry));
                Debug.WriteLine(FormatLogLine(logEntry));
                if (level == LogLevel.Information)
                {
                    OnLogAdded?.Invoke(logEntry);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[LogManager] Error: {ex.Message}"); }
        }

        private string FormatLogLine(LogEntry logEntry)
        {
            return $"[{logEntry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{logEntry.Level,-11}] [Thread-{logEntry.ThreadId:D3}] [{logEntry.Source}] {logEntry.Message}";
        }

        private void WriteToFile(string line)
        {
            try
            {
                lock (_lock) { File.AppendAllText(_logFilePath, line + Environment.NewLine); }
            }
            catch (Exception ex) { Debug.WriteLine($"[LogManager] File write error: {ex.Message}"); }
        }
        #endregion

        #region Cleanup

        public static void CleanupOldLogs(int daysToKeep = 7)
        {
            try
            {
                // ✅ UPDATE: Sử dụng Property LogFolderPath để đồng bộ logic đường dẫn
                string logFolder = LogFolderPath;

                if (!Directory.Exists(logFolder))
                    return;

                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var logFiles = Directory.GetFiles(logFolder, "UITestKit_*.log");

                foreach (var file in logFiles)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        File.Delete(file);
                        Debug.WriteLine($"[LogManager] Deleted old log file: {file}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LogManager] Cleanup error: {ex.Message}");
            }
        }

        #endregion
    }
}