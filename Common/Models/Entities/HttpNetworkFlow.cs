namespace NetworkMonitor.Models
{
    /// <summary>
    /// Represents an HTTP network flow with all relevant fields.
    /// </summary>
    public class HttpNetworkFlow
    {

        public int Stage { get; set; }
        /// <summary>
        /// Gets or sets the timestamp when the packet was captured.
        /// </summary>
        public string? Time { get; set; }

        /// <summary>
        /// Gets or sets informational description of the packet (e.g., "HTTP Request", "HTTP Response").
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
        /// Gets or sets the HTTP request URI (for requests).
        /// </summary>
        public string? URI { get; set; }

        /// <summary>
        /// Gets or sets the HTTP Host header value.
        /// </summary>
        public string? Host { get; set; }

        /// <summary>
        /// Gets or sets the HTTP method (GET, POST, PUT, DELETE, etc.).
        /// </summary>
        public string? Method { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code (for responses).
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Gets or sets the HTTP version (e.g., "HTTP/1.1").
        /// </summary>
        public string? HttpVersion { get; set; }

        /// <summary>
        /// Gets or sets the HTTP headers.
        /// </summary>
        public string? HttpHeaders { get; set; }

        /// <summary>
        /// Gets or sets the HTTP body.
        /// </summary>
        public string? HttpBody { get; set; }

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
