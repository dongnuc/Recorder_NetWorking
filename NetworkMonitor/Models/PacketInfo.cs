namespace NetworkMonitor
{
    public class PacketInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PacketInfo"/> class.
        /// </summary>
        public PacketInfo()
        {
        }

        /// <summary>
        /// Gets or sets the timestamp of when the packet was captured.
        /// </summary>
        public string Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the detailed type of the packet (e.g., "HTTP Request (GET /index.html HTTP/1.1)").
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the short protocol name (e.g., "TCP", "UDP", "HTTP").
        /// </summary>
        public string Protocol { get; set; }

        /// <summary>
        /// Gets or sets the source address and port (e.g., "127.0.0.1:12345").
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Gets or sets the destination address and port (e.g., "127.0.0.1:80").
        /// </summary>
        public string Destination { get; set; }

        /// <summary>
        /// Gets or sets a summary of the captured packet data.
        /// </summary>
        public string CapturedData { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the packet has a non-empty payload.
        /// </summary>
        public bool HasPayload { get; set; }

        /// <summary>
        /// Gets or sets the TCP flags (e.g., "ACK", "FIN", "SYN", etc.) for TCP packets.
        /// </summary>
        public string TcpFlags { get; set; }

        /// <summary>
        /// Gets or sets the HTTP request URI for HTTP request packets.
        /// </summary>
        public string HttpRequestUri { get; set; }

        /// <summary>
        /// Gets or sets the HTTP headers for HTTP packets.
        /// </summary>
        public string HttpHeaders { get; set; }

        /// <summary>
        /// GET, POST, PUT, DELETE, etc. for HTTP packets.
        /// </summary>
        public string HttpMethods { get; set; }


        /// <summary>
        /// Gets or sets the HTTP body for HTTP packets.
        /// </summary>
        public string HttpBody { get; set; }

        /// <summary>
        /// Gets or sets the connection state based on TCP flags.
        /// </summary>
        public string ConnectionState { get; set; }
    }
}
