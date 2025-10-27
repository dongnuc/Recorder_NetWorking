namespace Common.Models.Entities
{
    public enum LogLevel
    {
        Debug,
        Information,
        Warning,
        Error,
        Critical
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; }
        public string Source { get; set; }
        public int ThreadId { get; set; }

        public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss.fff");

        public string LevelText => Level.ToString().ToUpper();

        public override string ToString()
        {
            return $"[{FormattedTimestamp}] [{LevelText}] {Message}";
        }
    }
}
