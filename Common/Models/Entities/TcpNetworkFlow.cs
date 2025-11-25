namespace NetworkMonitor.Models
{
    /// <summary>
    /// Represents a TCP network flow with all relevant fields.
    /// </summary>
    public class TcpNetworkFlow
    {
        public int Stage { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the packet was captured.
        /// </summary>
        public string? Time { get; set; }

        /// <summary>
        /// Gets or sets informational description of the packet (e.g., "TCP", "HTTP Request", "HTTP Response").
        /// </summary>
        public string? Info { get; set; }

        /// <summary>
        /// Gets or sets the source address and port.
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// Gets or sets the destination address and port.
        /// </summary>
        public string? Destination { get; set; }

        /// <summary>
        /// Gets or sets the TCP flags (e.g., "SYN, ACK").
        /// </summary>
        public string? Flags { get; set; }

        /// <summary>
        /// Gets or sets the connection state based on TCP flags.
        /// </summary>
        public string? State { get; set; }

        /// <summary>
        /// Gets or sets the TCP payload data.
        /// </summary>
        public string? Data { get; set; }

        /// <summary>
        /// Gets or sets the role of the source (Server or Client).
        /// </summary>
        public string? SourceRole { get; set; }

        /// <summary>
        /// Gets or sets the role of the destination (Server or Client).
        /// </summary>
        public string? DestinationRole { get; set; }
    }
}
