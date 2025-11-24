using NetworkMonitor;
using PacketDotNet;
using SharpPcap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetworkMonitor.Abstractions;
using NetworkMonitor.Keywords;

namespace NetworkMonitor.Services
{
    /// <summary>
    /// Service responsible for capturing network packets.
    /// </summary>
    public class PacketCaptureService : IPacketCaptureService
    {
        private ICaptureDevice? _device;
        private List<int>? _monitoredPorts;
        private bool _monitorAllPorts;
        private bool _isCapturing;
        private readonly List<PacketCapturedEventArgs> _capturedPackets = new();
        private readonly object _packetsLock = new();
        private const int MaxStoredPackets = 1000; // Limit to prevent memory issues
        private bool _logCapturedPackets = false; // Enable/disable packet logging

        /// <summary>
        /// Event raised when a packet is captured.
        /// </summary>
        public event EventHandler<PacketCapturedEventArgs>? PacketCaptured;

        /// <summary>
        /// Event raised when a log message needs to be written.
        /// </summary>
        public event EventHandler<LogMessageEventArgs>? LogMessage;

        /// <summary>
        /// Gets or sets whether to log captured packets via LogMessage event.
        /// When enabled, each captured packet will be logged with its details including TCP flags.
        /// </summary>
        public bool LogCapturedPackets
        {
            get => _logCapturedPackets;
            set => _logCapturedPackets = value;
        }

        /// <summary>
        /// Starts capturing packets on the specified device.
        /// </summary>
        /// <param name="device">The capture device to use.</param>
        /// <param name="portsMode">The ports mode (all, common, targeted, or custom).</param>
        /// <param name="customPorts">Custom port list if portsMode is custom.</param>
        /// <param name="cancellationToken">Cancellation token to stop capturing.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task StartCaptureAsync(ICaptureDevice device, string portsMode, string? customPorts, CancellationToken cancellationToken)
        {
            return Task.Run(() => StartCapture(device, portsMode, customPorts, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Stops packet capture.
        /// </summary>
        public void StopCapture()
        {
            if (_device != null && _isCapturing)
            {
                try
                {
                    _device.StopCapture();
                    _device.Close();
                    _isCapturing = false;
                    RaiseLogMessage(Service_Keywords.SnifferStoppedCapture, false);
                }
                catch (Exception ex)
                {
                    RaiseLogMessage(string.Format(Service_Keywords.StopCloseError, ex.GetType().Name, ex.Message), true);
                }
            }
        }

        /// <summary>
        /// Starts the packet capture process.
        /// </summary>
        private void StartCapture(ICaptureDevice device, string portsMode, string? customPorts, CancellationToken cancellationToken)
        {
            _device = device;

            // Parse ports configuration
            string portsArg = portsMode;
            if (portsMode == Service_Keywords.PortsModeCustom && !string.IsNullOrEmpty(customPorts))
            {
                portsArg = customPorts;
            }

            try
            {
                (_monitoredPorts, _monitorAllPorts) = ParsePorts(portsArg);
            }
            catch (Exception ex)
            {
                RaiseLogMessage($"Invalid ports: {ex.Message}", true);
                return;
            }

            // Ensure we only attach the handler once
            device.OnPacketArrival -= Device_OnPacketArrival;
            device.OnPacketArrival += Device_OnPacketArrival;

            try
            {
                device.Open(DeviceModes.Promiscuous, Service_Keywords.DefaultReadTimeout);
            }
            catch (Exception ex)
            {
                RaiseLogMessage(string.Format(Service_Keywords.OpenDeviceError, ex.GetType().Name, ex.Message), true);
                return;
            }

            // Build BPF filter
            string filter = BuildPacketFilter();

            try
            {
                device.Filter = filter;
                RaiseLogMessage(string.Format(Service_Keywords.SnifferAppliedFilter, filter), false);
            }
            catch (Exception ex)
            {
                RaiseLogMessage(string.Format(Service_Keywords.FilterError, ex.GetType().Name, ex.Message), true);
                try { device.Filter = ""; } catch { }
            }

            try
            {
                device.StartCapture();
                _isCapturing = true;
                RaiseLogMessage(string.Format(Service_Keywords.SnifferStartedCapture, device.Description), false);
            }
            catch (Exception ex)
            {
                RaiseLogMessage(string.Format(Service_Keywords.StartCaptureError, ex.GetType().Name, ex.Message), true);
                return;
            }

            // Loop until cancellation requested
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(Service_Keywords.DefaultSleepInterval);
                }
            }
            finally
            {
                StopCapture();
            }
        }

        /// <summary>
        /// Builds the BPF filter based on monitored ports configuration.
        /// </summary>
        private string BuildPacketFilter()
        {
            string filter = Network_Keywords.FilterTcpOrUdp;

            if (!_monitorAllPorts && _monitoredPorts != null && _monitoredPorts.Count > 0)
            {
                var tokens = new List<string>();
                foreach (var p in _monitoredPorts)
                {
                    // Only add valid port numbers
                    if (p > Network_Keywords.MinPort - 1 && p <= Network_Keywords.MaxPort)
                    {
                        tokens.Add($"{Network_Keywords.FilterTCP} port {p}");
                        tokens.Add($"{Network_Keywords.FilterUDP} port {p}");
                    }
                }
                if (tokens.Count > 0)
                    filter = string.Join(Network_Keywords.FilterOrSeparator, tokens);
            }

            return filter;
        }

        /// <summary>
        /// Parses the ports argument to determine which ports to monitor.
        /// </summary>
        private (List<int>?, bool) ParsePorts(string portsArg)
        {
            if (portsArg.Equals(Service_Keywords.PortsModeAll, StringComparison.OrdinalIgnoreCase))
                return (null, true);

            if (portsArg.Equals(Service_Keywords.PortsModeCommon, StringComparison.OrdinalIgnoreCase))
                return (Network_Keywords.CommonPorts.ToList(), false);

            if (portsArg.Equals(Service_Keywords.PortsModeTargeted, StringComparison.OrdinalIgnoreCase))
                return (Network_Keywords.TargetedPorts.ToList(), false);

            try
            {
                var ports = new List<int>();
                foreach (var portStr in portsArg.Split(','))
                {
                    if (int.TryParse(portStr.Trim(), out int port))
                    {
                        ports.Add(port);
                    }
                    else
                    {
                        throw new ArgumentException($"'{portStr.Trim()}' is not a valid port number.");
                    }
                }
                return (ports, false);
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new ArgumentException(Validation_Keywords.InvalidPortsFormat);
            }
        }

        /// <summary>
        /// Handles packet arrival events.
        /// </summary>
        private void Device_OnPacketArrival(object sender, PacketCapture e)
        {
            try
            {
                var raw = e.GetPacket();
                var packet = Packet.ParsePacket(raw.LinkLayerType, raw.Data);

                // Parse IP layer
                var ipPacket = packet.Extract<IPPacket>();
                string srcIp = ipPacket?.SourceAddress?.ToString() ?? Network_Keywords.UnknownIpAddress;
                string dstIp = ipPacket?.DestinationAddress?.ToString() ?? Network_Keywords.UnknownIpAddress;

                // Parse TCP/UDP
                var tcp = packet.Extract<TcpPacket>();
                var udp = packet.Extract<UdpPacket>();

                int srcPort = 0, dstPort = 0;
                string protocolLabel = Network_Keywords.ProtocolUnknown;
                string? decodedPayload = null;

                if (tcp != null)
                {
                    srcPort = tcp.SourcePort;
                    dstPort = tcp.DestinationPort;
                    protocolLabel = Network_Keywords.ProtocolTCP;
                    if (tcp.PayloadData != null && tcp.PayloadData.Length > 0)
                    {
                        try { decodedPayload = Encoding.UTF8.GetString(tcp.PayloadData); }
                        catch { decodedPayload = BitConverter.ToString(tcp.PayloadData); }
                    }
                }
                else if (udp != null)
                {
                    srcPort = udp.SourcePort;
                    dstPort = udp.DestinationPort;
                    protocolLabel = Network_Keywords.ProtocolUDP;
                    if (udp.PayloadData != null && udp.PayloadData.Length > 0)
                    {
                        try { decodedPayload = Encoding.UTF8.GetString(udp.PayloadData); }
                        catch { decodedPayload = BitConverter.ToString(udp.PayloadData); }
                    }
                }
                else
                {
                    // Not TCP/UDP — ignore
                    return;
                }

                // Respect monitored ports if configured
                if (!_monitorAllPorts && _monitoredPorts != null && _monitoredPorts.Count > 0)
                {
                    bool match = (_monitoredPorts.Contains(srcPort) || _monitoredPorts.Contains(dstPort));
                    if (!match) return;
                }

                // Detect HTTP protocol
                var httpLabel = DetectHttpLabel(decodedPayload);
                if (!string.IsNullOrEmpty(httpLabel))
                    protocolLabel = httpLabel;

                // Skip packets with no meaningful data
                if (srcPort == 0 && dstPort == 0 && string.IsNullOrEmpty(decodedPayload))
                    return;

                // Create packet captured event args
                var eventArgs = new PacketCapturedEventArgs
                {
                    SourceIp = srcIp,
                    SourcePort = srcPort,
                    DestinationIp = dstIp,
                    DestinationPort = dstPort,
                    DecodedPayload = decodedPayload,
                    ProtocolLabel = protocolLabel,
                    Packet = packet,
                    TcpPacket = tcp
                };

                // Store captured packet for service retrieval
                StorePacket(eventArgs);

                // Log captured packet if logging is enabled
                if (_logCapturedPackets)
                {
                    var packetSummary = PacketFormatter.FormatPacketSummary(eventArgs);
                    RaiseLogMessage($"[Packet Captured] {packetSummary}", false);
                }

                // Raise packet captured event
                PacketCaptured?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                RaiseLogMessage(string.Format(Service_Keywords.HandlerError, DateTime.Now.ToString(Logging_Keywords.TimestampFormat), ex.GetType().Name, ex.Message), true);
            }
        }

        /// <summary>
        /// Detects if the payload contains HTTP data and returns an appropriate label.
        /// </summary>
        private string? DetectHttpLabel(string? payload)
        {
            if (string.IsNullOrEmpty(payload))
                return null;

            // Early check for HTTP protocol prefix (most efficient)
            int httpIndex = payload.IndexOf(Network_Keywords.HttpProtocolPrefix, StringComparison.Ordinal);
            if (httpIndex >= 0)
            {
                // Check if it's at the start (response) or after a method (request)
                if (httpIndex == 0 ||
                    (httpIndex > 0 && char.IsWhiteSpace(payload[httpIndex - 1])))
                {
                    return Network_Keywords.ProtocolHTTP;
                }
            }

            // Check for HTTP methods at the start of payload (space-separated)
            if (payload.Length >= 3)
            {
                int spaceIndex = payload.IndexOf(' ');
                if (spaceIndex > 0 && spaceIndex < 10)
                {
                    string methodCandidate = payload.Substring(0, spaceIndex);
                    if (IsHttpMethod(methodCandidate))
                    {
                        return Network_Keywords.ProtocolHTTP;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a string is a known HTTP method.
        /// </summary>
        private bool IsHttpMethod(string method)
        {
            return method.Equals(Network_Keywords.HttpMethodGET, StringComparison.OrdinalIgnoreCase) ||
                   method.Equals(Network_Keywords.HttpMethodPOST, StringComparison.OrdinalIgnoreCase) ||
                   method.Equals(Network_Keywords.HttpMethodPUT, StringComparison.OrdinalIgnoreCase) ||
                   method.Equals(Network_Keywords.HttpMethodDELETE, StringComparison.OrdinalIgnoreCase) ||
                   method.Equals(Network_Keywords.HttpMethodHEAD, StringComparison.OrdinalIgnoreCase) ||
                   method.Equals(Network_Keywords.HttpMethodOPTIONS, StringComparison.OrdinalIgnoreCase) ||
                   method.Equals(Network_Keywords.HttpMethodPATCH, StringComparison.OrdinalIgnoreCase) ||
                   method.Equals(Network_Keywords.HttpMethodCONNECT, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Raises a log message event.
        /// </summary>
        private void RaiseLogMessage(string message, bool isError)
        {
            LogMessage?.Invoke(this, new LogMessageEventArgs
            {
                Message = message,
                IsError = isError
            });
        }

        /// <summary>
        /// Stores a captured packet in the internal buffer.
        /// </summary>
        private void StorePacket(PacketCapturedEventArgs eventArgs)
        {
            lock (_packetsLock)
            {
                _capturedPackets.Add(eventArgs);

                // Limit stored packets to prevent memory issues
                if (_capturedPackets.Count > MaxStoredPackets)
                {
                    _capturedPackets.RemoveAt(0); // Remove oldest packet
                }
            }
        }

        /// <summary>
        /// Gets all captured packets as formatted strings.
        /// </summary>
        /// <param name="format">The format to use: "summary", "detailed", or "json". Default is "summary".</param>
        /// <returns>List of formatted packet strings.</returns>
        public List<string> GetCapturedPacketsAsStrings(string format = "summary")
        {
            lock (_packetsLock)
            {
                return FormatPackets(_capturedPackets, format);
            }
        }

        /// <summary>
        /// Gets the most recent captured packets as formatted strings.
        /// </summary>
        /// <param name="count">Number of recent packets to retrieve.</param>
        /// <param name="format">The format to use: "summary", "detailed", or "json". Default is "summary".</param>
        /// <returns>List of formatted packet strings.</returns>
        public List<string> GetRecentPacketsAsStrings(int count, string format = "summary")
        {
            lock (_packetsLock)
            {
                var recentPackets = _capturedPackets
                    .Skip(Math.Max(0, _capturedPackets.Count - count))
                    .ToList();
                return FormatPackets(recentPackets, format);
            }
        }

        /// <summary>
        /// Clears all stored captured packets.
        /// </summary>
        public void ClearCapturedPackets()
        {
            lock (_packetsLock)
            {
                _capturedPackets.Clear();
            }
        }

        /// <summary>
        /// Gets the count of captured packets currently stored.
        /// </summary>
        public int GetCapturedPacketCount()
        {
            lock (_packetsLock)
            {
                return _capturedPackets.Count;
            }
        }

        /// <summary>
        /// Formats a list of packets based on the specified format.
        /// </summary>
        private List<string> FormatPackets(List<PacketCapturedEventArgs> packets, string format)
        {
            var result = new List<string>();

            foreach (var packet in packets)
            {
                string formattedPacket = format.ToLowerInvariant() switch
                {
                    "detailed" => PacketFormatter.FormatPacket(packet),
                    "json" => PacketFormatter.FormatPacketAsJson(packet),
                    _ => PacketFormatter.FormatPacketSummary(packet)
                };

                result.Add(formattedPacket);
            }

            return result;
        }
    }
}
