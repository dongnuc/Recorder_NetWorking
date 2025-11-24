using System.Text.RegularExpressions;

namespace NetworkMonitor.Keywords
{
    /// <summary>
    /// Contains all network-related string constants used throughout the application.
    /// </summary>
    public static class Network_Keywords
    {
        // Protocol Names
        public const string ProtocolTCP = "TCP";
        public const string ProtocolUDP = "UDP";
        public const string ProtocolHTTP = "HTTP";
        public const string ProtocolUnknown = "Unknown";

        // TCP Flags
        public const string TcpFlagFIN = "FIN";
        public const string TcpFlagSYN = "SYN";
        public const string TcpFlagRST = "RST";
        public const string TcpFlagPSH = "PSH";
        public const string TcpFlagACK = "ACK";
        public const string TcpFlagURG = "URG";
        /// <summary>
        /// ECE (ECN-Echo): Indicates that the TCP peer is ECN capable during 3-way handshake.
        /// Used for explicit congestion notification. Bit 6 (0x40).
        /// </summary>
        public const string TcpFlagECE = "ECE";
        /// <summary>
        /// CWR (Congestion Window Reduced): Indicates that the sender reduced its sending rate.
        /// Used in response to receiving a packet with the ECE flag set. Bit 7 (0x80).
        /// </summary>
        public const string TcpFlagCWR = "CWR";

        // HTTP Methods
        public const string HttpMethodGET = "GET";
        public const string HttpMethodPOST = "POST";
        public const string HttpMethodPUT = "PUT";
        public const string HttpMethodDELETE = "DELETE";
        public const string HttpMethodHEAD = "HEAD";
        public const string HttpMethodOPTIONS = "OPTIONS";
        public const string HttpMethodPATCH = "PATCH";
        public const string HttpMethodCONNECT = "CONNECT";

        // HTTP Protocol Identifier
        public const string HttpProtocolPrefix = "HTTP/";

        // HTTP Headers
        public const string HttpHeaderHost = "Host";
        public const string HttpHeaderUserAgent = "User-Agent";
        public const string HttpHeaderContentType = "Content-Type";

        // IP Addresses
        public const string UnknownIpAddress = "(unknown)";

        // Payload Status
        public const string NoPayload = "No payload";
        public const string NonTextPayload = "Non-text payload";

        // Port Ranges
        public const int MinPort = 1;
        public const int MaxPort = 65535;

        // Common Ports
        public static readonly int[] CommonPorts = { 80, 443, 8000, 8080, 8888 };
        public static readonly int[] TargetedPorts = { 5000, 8080 };

        // Filter Strings
        public const string FilterTCP = "tcp";
        public const string FilterUDP = "udp";
        public const string FilterTcpOrUdp = "tcp or udp";
        public const string FilterTcpPort = "tcp port {0}";
        public const string FilterUdpPort = "udp port {0}";
        public const string FilterOrSeparator = " or ";

        // Regex for HTTP Request Parsing
        private static readonly Regex HttpRequestRegex = new Regex(
    @"\b(GET|POST|PUT|DELETE|HEAD|OPTIONS|PATCH|CONNECT)\s+(\S+)\s+HTTP/([0-9.]+)",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
