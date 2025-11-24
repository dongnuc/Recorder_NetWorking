namespace NetworkMonitor.Keywords
{
    /// <summary>
    /// Contains all service-related string constants used throughout the application.
    /// </summary>
    public static class Service_Keywords
    {
        // Service Messages
        public const string SnifferAppliedFilter = "[Sniffer] Applied filter: {0}";
        public const string SnifferStartedCapture = "[Sniffer] Started capture on: {0}";
        public const string SnifferStoppedCapture = "[Sniffer] Stopped capture and closed device";
        public const string OpenDeviceError = "[Open device error] {0}: {1}";
        public const string FilterError = "[Filter error] {0}: {1} - continuing without filter";
        public const string StartCaptureError = "[StartCapture error] {0}: {1}";
        public const string StopCloseError = "[Stop/Close error] {0}: {1}";
        public const string HandlerError = "[Handler error {0}] {1}: {2}";

        // Service Configuration
        public const int DefaultReadTimeout = 1000;
        public const int DefaultSleepInterval = 100;

        // Ports Mode
        public const string PortsModeAll = "all";
        public const string PortsModeCommon = "common";
        public const string PortsModeTargeted = "targeted";
        public const string PortsModeCustom = "custom";
    }
}
