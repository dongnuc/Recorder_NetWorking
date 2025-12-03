﻿using NetworkMonitor.Models;
using PacketDotNet;
using SharpPcap;

namespace NetworkMonitor.Abstractions
{
    /// <summary>
    /// Defines the contract for packet capture service.
    /// </summary>
    public interface IPacketCaptureService
    {
        /// <summary>
        /// Starts capturing packets on the specified device.
        /// </summary>
        /// <param name="device">The capture device to use.</param>
        /// <param name="portsMode">The ports mode (all, common, targeted, or custom).</param>
        /// <param name="customPorts">Custom port list if portsMode is custom.</param>
        /// <param name="cancellationToken">Cancellation token to stop capturing.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StartCaptureAsync(ICaptureDevice device, string portsMode, string? customPorts, CancellationToken cancellationToken, TaskCompletionSource<bool> startupSignal);

        /// <summary>
        /// Stops packet capture.
        /// </summary>
        void StopCapture();

       
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

}