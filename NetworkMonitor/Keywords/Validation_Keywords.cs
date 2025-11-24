namespace NetworkMonitor.Keywords
{
    /// <summary>
    /// Contains all validation-related string constants used throughout the application.
    /// </summary>
    public static class Validation_Keywords
    {
        // Error Messages
        public const string InvalidPortsFormat = "Invalid ports format. Use 'all', 'common', 'targeted', or comma-separated ints.";

        // Interface Keywords
        public const string LoopbackKeywordLower = "loopback";
        public const string LoopbackInterfaceKeyword = "lo";
    }
}
