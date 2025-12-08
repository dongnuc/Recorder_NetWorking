﻿using Common.Interfaces.Services;
using Common.Logging;
using NetworkMonitor.Abstractions;
using NetworkMonitor.Keywords;
using NetworkMonitor.Models;
using PacketDotNet;
using SharpPcap;
using System.Text;

namespace NetworkMonitor.Services
{
    /// <summary>
    /// Service responsible for capturing network packets.
    /// </summary>
    public class PacketCaptureService : IPacketCaptureService
    {
        private ICaptureDevice? _device;
        private int _targetPort;
        private bool _isCapturing;
        private bool _logCapturedPackets = false; // Enable/disable packet logging
        private readonly ITestkitManagerService _testkitManager;
        private readonly string _protocol;

        public PacketCaptureService(ITestkitManagerService testkitManagerService,string protocol)
        {
            _testkitManager = testkitManagerService;
            _protocol = protocol;
        }
        /// <summary>
        /// Starts capturing packets on the specified device.
        /// </summary>
        /// <param name="device">The capture device to use.</param>
        /// <param name="portsMode">The ports mode (all, common, targeted, or custom).</param>
        /// <param name="customPorts">Custom port list if portsMode is custom.</param>
        /// <param name="cancellationToken">Cancellation token to stop capturing.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task StartCaptureAsync(ICaptureDevice device, string portsMode, string? customPorts,
            CancellationToken cancellationToken, TaskCompletionSource<bool> startupSignal)
        {
            return Task.Run(() => StartCapture(device, portsMode, customPorts, cancellationToken, startupSignal), cancellationToken);
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
                    _device.OnPacketArrival -= Device_OnPacketArrival;
                    _device.StopCapture();
                    _device.Close();
                    _isCapturing = false;
                }
                catch (Exception ex)
                {
                    LogManager.Instance.LogError(string.Format(Service_Keywords.StopCloseError, ex.GetType().Name, ex.Message));
                }
            }
        }

        /// <summary>
        /// Starts the packet capture process.
        /// </summary>
        private void StartCapture(ICaptureDevice device, string portsMode, string? customPorts,
            CancellationToken cancellationToken, TaskCompletionSource<bool> startupSignal)
        {
            _device = device;
            var isPort= int.TryParse(portsMode, out _targetPort);
            if (!isPort)
            {
                return;
            }
            // Parse ports configuration
            string portsArg = portsMode;
            if (portsMode == Service_Keywords.PortsModeCustom && !string.IsNullOrEmpty(customPorts))
            {
                portsArg = customPorts;
            }

            // Ensure we only attach the handler once
            if (_device != null)
            {
                _device.OnPacketArrival -= Device_OnPacketArrival;
                _device.OnPacketArrival += Device_OnPacketArrival;
            }

            try
            {
                device.Open(DeviceModes.Promiscuous, Service_Keywords.DefaultReadTimeout);
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError(string.Format(Service_Keywords.OpenDeviceError, ex.GetType().Name, ex.Message));
                return;
            }

            // Build BPF filter
            string filter = BuildPacketFilter();

            try
            {
                device.Filter = filter;
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError(string.Format(Service_Keywords.FilterError, ex.GetType().Name, ex.Message));
                try { device.Filter = ""; } catch { }
            }

            try
            {
                device.StartCapture();
                _isCapturing = true;
                LogManager.Instance.LogDebug(string.Format(Service_Keywords.SnifferStartedCapture, device.Description));
                startupSignal.TrySetResult(true);
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError(string.Format(Service_Keywords.StartCaptureError, ex.GetType().Name, ex.Message));
                startupSignal.TrySetException(ex);
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
            // Filter đơn giản: tcp port X or udp port X
            return $"{Network_Keywords.FilterTCP} port {_targetPort} or {Network_Keywords.FilterUDP} port {_targetPort}";
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
                if (srcPort != _targetPort && dstPort != _targetPort)
                {
                    return;
                }

                // Detect HTTP protocol
                var httpLabel = DetectHttpLabel(decodedPayload);
                if (!string.IsNullOrEmpty(httpLabel))
                    protocolLabel = httpLabel;

                if (srcPort == 0 && dstPort == 0 && string.IsNullOrEmpty(decodedPayload))
                    return;

                var eventArgs = new PacketCapturedEventArgs
                {
                    SourceIp = srcIp,
                    SourcePort = srcPort,
                    DestinationIp = dstIp,
                    DestinationPort = dstPort,
                    DecodedPayload = decodedPayload,
                    ProtocolLabel = protocolLabel,
                    Packet = packet,
                    TcpPacket = tcp,
                    Timestamp = raw.Timeval.Date
                };

                int monitoredPort = _targetPort;

                if (_protocol.Equals(Network_Keywords.ProtocolHTTP,StringComparison.OrdinalIgnoreCase))
                {
                    var flow = NetworkFlowConverter.ToHttpNetworkFlow(eventArgs, monitoredPort);
                    _testkitManager.IngestHttpTransaction(flow);
                }
                else
                {
                    var flow = NetworkFlowConverter.ToTcpNetworkFlow(eventArgs, monitoredPort);
                    _testkitManager.IngestTcpTransaction(flow);
                }


            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError(string.Format(Service_Keywords.HandlerError, DateTime.Now.ToString(Logging_Keywords.TimestampFormat), ex.GetType().Name, ex.Message));
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


    }


}