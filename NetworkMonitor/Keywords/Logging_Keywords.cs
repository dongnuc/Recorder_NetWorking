namespace NetworkMonitor.Keywords
{
    /// <summary>
    /// Contains all logging-related string constants used throughout the application.
    /// </summary>
    public static class Logging_Keywords
    {
        // Log Format Patterns
        public const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";
        public const string LogLineFormat = "[{0}]: [{1}] detected from [{2}] to [{3}]: {4}";
        public const string FullHeaderFormat = "[{0}] {1} {2} -> {3}";

        // Log Separators
        public const string LogSeparator = "--------------------------------------------------------------------------------";
        public const int LogSeparatorLength = 80;

        // Truncation Messages - disabled (no suffixes)
        public const string TruncatedSuffix = "";
        public const string HexPrefix = "hex:";
        public const string HexSuffix = "";

        // Debug Messages
        public const string DebugPreviewPrefix = "DEBUG preview: ";

        // Payload Display - unlimited
        public const int MaxBodyDisplayLength = int.MaxValue;
        public const int MaxDebugPreviewLength = int.MaxValue;
        public const int MaxFirstLineLength = int.MaxValue;
        public const int MaxHexPreviewLength = int.MaxValue;
    }
}
