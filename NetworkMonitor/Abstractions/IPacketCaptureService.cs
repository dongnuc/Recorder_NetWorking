using PacketDotNet;
using SharpPcap;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkMonitor.Abstractions
{
    /// <summary>
    /// Defines the contract for packet capture service.
    /// </summary>
    public interface IPacketCaptureService
    {
        /// <summary>
        /// Event raised when a packet is captured.
        /// </summary>
        event EventHandler<PacketCapturedEventArgs>? PacketCaptured;

        /// <summary>
        /// Event raised when a log message needs to be written.
        /// </summary>
        event EventHandler<LogMessageEventArgs>? LogMessage;

        /// <summary>
        /// Gets or sets whether to log captured packets via LogMessage event.
        /// When enabled, each captured packet will be logged with its details including TCP flags.
        /// </summary>
        bool LogCapturedPackets { get; set; }

        /// <summary>
        /// Starts capturing packets on the specified device.
        /// </summary>
        /// <param name="device">The capture device to use.</param>
        /// <param name="portsMode">The ports mode (all, common, targeted, or custom).</param>
        /// <param name="customPorts">Custom port list if portsMode is custom.</param>
        /// <param name="cancellationToken">Cancellation token to stop capturing.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StartCaptureAsync(ICaptureDevice device, string portsMode, string? customPorts, CancellationToken cancellationToken);

        /// <summary>
        /// Stops packet capture.
        /// </summary>
        void StopCapture();

        /// <summary>
        /// Gets all captured packets as formatted strings.
        /// </summary>
        /// <param name="format">The format to use: "summary", "detailed", or "json". Default is "summary".</param>
        /// <returns>List of formatted packet strings.</returns>
        List<string> GetCapturedPacketsAsStrings(string format = "summary");

        /// <summary>
        /// Gets the most recent captured packets as formatted strings.
        /// </summary>
        /// <param name="count">Number of recent packets to retrieve.</param>
        /// <param name="format">The format to use: "summary", "detailed", or "json". Default is "summary".</param>
        /// <returns>List of formatted packet strings.</returns>
        List<string> GetRecentPacketsAsStrings(int count, string format = "summary");

        /// <summary>
        /// Clears all stored captured packets.
        /// </summary>
        void ClearCapturedPackets();

        /// <summary>
        /// Gets the count of captured packets currently stored.
        /// </summary>
        int GetCapturedPacketCount();
    }

    /// <summary>
    /// Event arguments for packet captured event.
    /// </summary>
    public class PacketCapturedEventArgs : EventArgs
    {
        public string SourceIp { get; set; } = string.Empty;
        public int SourcePort { get; set; }
        public string DestinationIp { get; set; } = string.Empty;
        public int DestinationPort { get; set; }
        public string? DecodedPayload { get; set; }
        public string ProtocolLabel { get; set; } = string.Empty;
        public Packet Packet { get; set; } = null!;
        public TcpPacket? TcpPacket { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Event arguments for log message event.
    /// </summary>
    public class LogMessageEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
        public bool IsError { get; set; }
    }
}
