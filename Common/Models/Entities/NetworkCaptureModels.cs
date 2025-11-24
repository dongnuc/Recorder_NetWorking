namespace Common.Models.Entities
{
    /// <summary>
    /// Base class for network capture data
    /// </summary>
    public abstract class NetworkCaptureBase
    {
        /// <summary>
        /// Information about the capture (e.g., "HTTP Request (POST /books HTTP/1.1)")
        /// </summary>
        public string Info { get; set; } = string.Empty;

        /// <summary>
        /// Source address and port (e.g., "::1:52387" or "Client")
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Destination address and port (e.g., "::1:5000" or "Server")
        /// </summary>
        public string Destination { get; set; } = string.Empty;

        /// <summary>
        /// TCP flags or HTTP status indicator
        /// </summary>
        public string? Flags { get; set; }

        /// <summary>
        /// Connection state
        /// </summary>
        public string? State { get; set; }
    }

    /// <summary>
    /// TCP network capture data
    /// </summary>
    public class TcpCaptureData : NetworkCaptureBase
    {
        /// <summary>
        /// TCP payload data
        /// </summary>
        public string? Data { get; set; }
    }

    /// <summary>
    /// HTTP network capture data
    /// </summary>
    public class HttpCaptureData : NetworkCaptureBase
    {
        /// <summary>
        /// Request URI (e.g., "/books")
        /// </summary>
        public string? URI { get; set; }

        /// <summary>
        /// Host header value (e.g., "localhost:5000")
        /// </summary>
        public string? Host { get; set; }

        /// <summary>
        /// HTTP method (e.g., "POST", "GET")
        /// </summary>
        public string? Method { get; set; }

        /// <summary>
        /// HTTP status code (e.g., "201 Created")
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// HTTP protocol version (e.g., "HTTP/1.1")
        /// </summary>
        public string? HttpVersion { get; set; }

        /// <summary>
        /// HTTP headers as formatted string
        /// </summary>
        public string? HttpHeaders { get; set; }

        /// <summary>
        /// HTTP body/payload
        /// </summary>
        public string? HttpBody { get; set; }
    }
}
