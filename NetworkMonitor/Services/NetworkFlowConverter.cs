using NetworkMonitor.Abstractions;
using NetworkMonitor.Keywords;
using NetworkMonitor.Models;
using PacketDotNet;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NetworkMonitor.Services
{
    /// <summary>
    /// Service responsible for converting packet data into structured network flow objects.
    /// </summary>
    public static class NetworkFlowConverter
    {
        // Compiled regex patterns for better performance
        private static readonly Regex HttpRequestRegex = new(@"^(\S+)\s+(\S+)\s+HTTP/([0-9.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex HttpResponseRegex = new(@"^HTTP/([0-9.]+)\s+(\d+)\s*(.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Converts a packet to a TCP network flow object.
        /// </summary>
        public static TcpNetworkFlow ToTcpNetworkFlow(PacketCapturedEventArgs args, int? monitoredPort = null)
        {
            if (args == null)
                return new TcpNetworkFlow();

            var timestamp = args.Timestamp.ToString(Logging_Keywords.TimestampFormat);
            var source = $"{args.SourceIp}:{args.SourcePort}";
            var destination = $"{args.DestinationIp}:{args.DestinationPort}";
            
            // Determine roles based on monitored port
            var (sourceRole, destinationRole) = DetermineRoles(args.SourcePort, args.DestinationPort, monitoredPort);

            // Get TCP flags and state
            var (flags, state) = GetTcpFlagsAndState(args.TcpPacket);

            var flow = new TcpNetworkFlow
            {
                Time = timestamp,
                Info = "TCP",  // Simple "TCP" for TCP-only packets
                Source = source,
                Destination = destination,
                Flags = flags,
                State = state,
                Data = args.DecodedPayload,
                SourceRole = sourceRole,
                DestinationRole = destinationRole
            };

            return flow;
        }

        /// <summary>
        /// Converts a packet to an HTTP network flow object.
        /// </summary>
        public static HttpNetworkFlow ToHttpNetworkFlow(PacketCapturedEventArgs args, int? monitoredPort = null)
        {
            if (args == null)
                return new HttpNetworkFlow();

            var timestamp = args.Timestamp.ToString(Logging_Keywords.TimestampFormat);
            var source = $"{args.SourceIp}:{args.SourcePort}";
            var destination = $"{args.DestinationIp}:{args.DestinationPort}";

            // Determine roles based on monitored port
            var (sourceRole, destinationRole) = DetermineRoles(args.SourcePort, args.DestinationPort, monitoredPort);

            // Get TCP flags and state
            var (flags, state) = GetTcpFlagsAndState(args.TcpPacket);

            // Parse HTTP data
            var httpData = ParseHttpData(args.DecodedPayload);

            // Determine if this is a request or response
            string info;
            if (!string.IsNullOrEmpty(httpData.Method))
            {
                info = "HTTP Request";
            }
            else if (!string.IsNullOrEmpty(httpData.Status))
            {
                info = "HTTP Response";
            }
            else
            {
                info = "HTTP";
            }

            var flow = new HttpNetworkFlow
            {
                Time = timestamp,
                Info = info,
                Source = source,
                Destination = destination,
                Flags = flags,
                State = state,
                URI = httpData.Uri,
                Host = httpData.Host,
                Method = httpData.Method,
                Status = httpData.Status,
                HttpVersion = httpData.HttpVersion,
                HttpHeaders = httpData.Headers,
                HttpBody = httpData.Body,
                SourceRole = sourceRole,
                DestinationRole = destinationRole
            };

            return flow;
        }

        /// <summary>
        /// Converts a network flow object to JSON string.
        /// </summary>
        public static string ToJson<T>(T flow) where T : class
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
            };
            return JsonSerializer.Serialize(flow, options);
        }

        /// <summary>
        /// Gets TCP flags as a comma-separated string.
        /// </summary>
        private static string GetTcpFlags(TcpPacket tcp)
        {
            if (tcp == null)
                return string.Empty;

            var flags = new List<string>();

            // Check TCP flags using boolean properties
            if (tcp.Finished) flags.Add(Network_Keywords.TcpFlagFIN);
            if (tcp.Synchronize) flags.Add(Network_Keywords.TcpFlagSYN);
            if (tcp.Reset) flags.Add(Network_Keywords.TcpFlagRST);
            if (tcp.Push) flags.Add(Network_Keywords.TcpFlagPSH);
            if (tcp.Acknowledgment) flags.Add(Network_Keywords.TcpFlagACK);
            if (tcp.Urgent) flags.Add(Network_Keywords.TcpFlagURG);

            // Check ECE and CWR flags using raw flags value
            // According to RFC 3168: ECE is bit 6 (0x40) and CWR is bit 7 (0x80)
            ushort flagsValue = tcp.Flags;
            if ((flagsValue & 0x40) != 0) flags.Add(Network_Keywords.TcpFlagECE); // ECE - Bit 6
            if ((flagsValue & 0x80) != 0) flags.Add(Network_Keywords.TcpFlagCWR); // CWR - Bit 7

            return flags.Count > 0 ? string.Join(", ", flags) : string.Empty;
        }

        /// <summary>
        /// Determines the connection state based on TCP flags.
        /// </summary>
        private static string? DetermineConnectionState(TcpPacket tcp)
        {
            if (tcp == null)
                return null;

            if (tcp.Synchronize && !tcp.Acknowledgment)
                return "Client connecting to server (SYN)";
            if (tcp.Synchronize && tcp.Acknowledgment)
                return "Server responding (SYN-ACK)";
            if (tcp.Finished && tcp.Acknowledgment)
                return "Closing connection (FIN-ACK)";
            if (tcp.Finished)
                return "Initiating connection close (FIN)";
            if (tcp.Reset)
                return "Connection reset (RST)";
            if (tcp.Push && tcp.Acknowledgment)
                return "Data transfer in progress";
            if (tcp.Acknowledgment)
                return "Connection established";

            return "Unknown state";
        }

        /// <summary>
        /// Determines server/client roles based on port and monitored port.
        /// </summary>
        private static (string?, string?) DetermineRoles(int sourcePort, int destinationPort, int? monitoredPort)
        {
            if (!monitoredPort.HasValue)
                return (null, null);

            string sourceRole = sourcePort == monitoredPort.Value ? "Server" : "Client";
            string destinationRole = destinationPort == monitoredPort.Value ? "Server" : "Client";
            return (sourceRole, destinationRole);
        }

        /// <summary>
        /// Gets TCP flags and connection state from a TCP packet.
        /// </summary>
        private static (string?, string?) GetTcpFlagsAndState(TcpPacket? tcp)
        {
            if (tcp == null)
                return (null, null);

            string? flags = GetTcpFlags(tcp);
            string? state = DetermineConnectionState(tcp);
            return (flags, state);
        }

        /// <summary>
        /// Parses HTTP request/response data.
        /// </summary>
        private static HttpData ParseHttpData(string? payload)
        {
            var httpData = new HttpData();

            if (string.IsNullOrEmpty(payload))
                return httpData;

            try
            {
                // Split into lines
                var lines = payload.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                if (lines.Length == 0)
                    return httpData;

                var firstLine = lines[0];

                // Check if it's a request or response
                if (firstLine.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
                {
                    // HTTP Response
                    httpData.StatusLine = firstLine;
                    ParseHttpResponse(lines, httpData);
                }
                else
                {
                    // HTTP Request
                    httpData.RequestLine = firstLine;
                    ParseHttpRequest(lines, httpData);
                }
            }
            catch
            {
                // If parsing fails, return what we have
            }

            return httpData;
        }

        /// <summary>
        /// Parses HTTP request data.
        /// </summary>
        private static void ParseHttpRequest(string[] lines, HttpData httpData)
        {
            if (lines.Length == 0)
                return;

            var firstLine = lines[0];
            
            // Parse request line: METHOD URI HTTP/VERSION
            var requestMatch = HttpRequestRegex.Match(firstLine);
            if (requestMatch.Success)
            {
                httpData.Method = requestMatch.Groups[1].Value;
                httpData.Uri = requestMatch.Groups[2].Value;
                httpData.HttpVersion = $"HTTP/{requestMatch.Groups[3].Value}";
            }

            // Parse headers and body
            ParseHeadersAndBody(lines, httpData);
        }

        /// <summary>
        /// Parses HTTP response data.
        /// </summary>
        private static void ParseHttpResponse(string[] lines, HttpData httpData)
        {
            if (lines.Length == 0)
                return;

            var firstLine = lines[0];
            
            // Parse status line: HTTP/VERSION STATUS_CODE STATUS_MESSAGE
            var responseMatch = HttpResponseRegex.Match(firstLine);
            if (responseMatch.Success)
            {
                httpData.HttpVersion = $"HTTP/{responseMatch.Groups[1].Value}";
                httpData.Status = $"{responseMatch.Groups[2].Value} {responseMatch.Groups[3].Value}".Trim();
            }

            // Parse headers and body
            ParseHeadersAndBody(lines, httpData);
        }

        /// <summary>
        /// Parses HTTP headers and body from lines.
        /// </summary>
        private static void ParseHeadersAndBody(string[] lines, HttpData httpData)
        {
            var headerLines = new List<string>();
            var bodyLines = new List<string>();
            bool inBody = false;

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];

                if (!inBody)
                {
                    // Empty line indicates end of headers
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        inBody = true;
                        continue;
                    }

                    headerLines.Add(line);

                    // Extract Host header if present (case-insensitive)
                    if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                    {
                        int colonIndex = line.IndexOf(':');
                        if (colonIndex >= 0 && colonIndex < line.Length - 1)
                        {
                            httpData.Host = line.Substring(colonIndex + 1).Trim();
                        }
                    }
                }
                else
                {
                    bodyLines.Add(line);
                }
            }

            if (headerLines.Count > 0)
            {
                httpData.Headers = string.Join("; ", headerLines);
            }

            if (bodyLines.Count > 0)
            {
                httpData.Body = string.Join("\n", bodyLines).Trim();
            }
        }

        /// <summary>
        /// Internal class to hold parsed HTTP data.
        /// </summary>
        private class HttpData
        {
            public string? RequestLine { get; set; }
            public string? StatusLine { get; set; }
            public string? Method { get; set; }
            public string? Uri { get; set; }
            public string? Status { get; set; }
            public string? HttpVersion { get; set; }
            public string? Host { get; set; }
            public string? Headers { get; set; }
            public string? Body { get; set; }
        }
    }
}
