using NetworkMonitor.Abstractions;
using NetworkMonitor.Keywords;
using PacketDotNet;
using System.Text;
using System.Text.Json;

namespace NetworkMonitor.Services
{
    /// <summary>
    /// Service responsible for formatting packet data as strings.
    /// </summary>
    public static class PacketFormatter
    {
        /// <summary>
        /// Formats a captured packet event args as a string representation.
        /// </summary>
        /// <param name="args">The packet captured event arguments.</param>
        /// <returns>A formatted string representation of the packet.</returns>
        public static string FormatPacket(PacketCapturedEventArgs args)
        {
            if (args == null)
                return string.Empty;

            var sb = new StringBuilder();
            
            // Basic packet info
            sb.AppendLine($"Protocol: {args.ProtocolLabel}");
            sb.AppendLine($"Source: {args.SourceIp}:{args.SourcePort}");
            sb.AppendLine($"Destination: {args.DestinationIp}:{args.DestinationPort}");
            
            // TCP flags if available
            if (args.TcpPacket != null)
            {
                sb.AppendLine($"TCP Flags: {GetTcpFlags(args.TcpPacket)}");
            }
            
            // Payload information
            if (!string.IsNullOrEmpty(args.DecodedPayload))
            {
                sb.AppendLine($"Payload: {args.DecodedPayload}");
            }
            else
            {
                sb.AppendLine("Payload: (empty)");
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Formats a captured packet as a single-line summary.
        /// </summary>
        /// <param name="args">The packet captured event arguments.</param>
        /// <returns>A single-line summary of the packet.</returns>
        public static string FormatPacketSummary(PacketCapturedEventArgs args)
        {
            if (args == null)
                return string.Empty;

            var flags = args.TcpPacket != null ? $" [{GetTcpFlags(args.TcpPacket)}]" : "";
            var payloadIndicator = !string.IsNullOrEmpty(args.DecodedPayload) ? " (has payload)" : "";
            
            return $"{args.ProtocolLabel}{flags}: {args.SourceIp}:{args.SourcePort} -> {args.DestinationIp}:{args.DestinationPort}{payloadIndicator}";
        }

        /// <summary>
        /// Formats a captured packet as JSON-like string.
        /// </summary>
        /// <param name="args">The packet captured event arguments.</param>
        /// <returns>A JSON-like string representation of the packet.</returns>
        public static string FormatPacketAsJson(PacketCapturedEventArgs args)
        {
            if (args == null)
                return "{}";

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"protocol\": \"{EscapeJson(args.ProtocolLabel)}\",");
            sb.AppendLine($"  \"source\": \"{EscapeJson(args.SourceIp)}:{args.SourcePort}\",");
            sb.AppendLine($"  \"destination\": \"{EscapeJson(args.DestinationIp)}:{args.DestinationPort}\",");
            
            if (args.TcpPacket != null)
            {
                sb.AppendLine($"  \"tcpFlags\": \"{EscapeJson(GetTcpFlags(args.TcpPacket))}\",");
            }
            
            var payload = !string.IsNullOrEmpty(args.DecodedPayload) ? args.DecodedPayload : "";
            sb.AppendLine($"  \"payload\": \"{EscapeJson(payload)}\"");
            sb.AppendLine("}");
            
            return sb.ToString();
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

            return flags.Count > 0 ? string.Join(", ", flags) : "None";
        }

        /// <summary>
        /// Escapes special characters for JSON string formatting using proper JSON encoding.
        /// </summary>
        private static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            // Use System.Text.Json for proper JSON escaping
            return JsonSerializer.Serialize(str).Trim('"');
        }
    }
}
