using Common.Logging;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Common.Helper
{
    public static class PortChecker
    {
        public static (int port1, int port2) GetTwoAvailablePorts(int startPort = 8000, int endPort = 65535)
        {
            var availablePorts = GetAvailablePorts(startPort, endPort, 2);

            if (availablePorts.Count < 2)
                throw new InvalidOperationException($"Could not find 2 available ports in range {startPort}-{endPort}");

            return (availablePorts[0], availablePorts[1]);
        }

        public static List<int> GetAvailablePorts(int startPort = 8000, int endPort = 65535, int count = 2)
        {
            // Get all ports currently in use
            var usedPorts = GetUsedPorts();

            var availablePorts = new List<int>();

            // Search for available ports
            for (int port = startPort; port <= endPort && availablePorts.Count < count; port++)
            {
                if (!usedPorts.Contains(port) && IsPortAvailable(port))
                {
                    availablePorts.Add(port);
                }
            }

            return availablePorts;
        }

        public static HashSet<int> GetUsedPorts()
        {
            var usedPorts = new HashSet<int>();

            try
            {
                // Get TCP connections
                var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();

                var tcpConnections = ipGlobalProperties.GetActiveTcpConnections();
                foreach (var connection in tcpConnections)
                {
                    usedPorts.Add(connection.LocalEndPoint.Port);
                }

                // Get TCP listeners
                var tcpListeners = ipGlobalProperties.GetActiveTcpListeners();
                foreach (var listener in tcpListeners)
                {
                    usedPorts.Add(listener.Port);
                }

                // Get UDP listeners
                var udpListeners = ipGlobalProperties.GetActiveUdpListeners();
                foreach (var listener in udpListeners)
                {
                    usedPorts.Add(listener.Port);
                }
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"[PortChecker] Error getting used ports: {ex.Message}");
            }

            return usedPorts;
        }

        public static bool IsPortAvailable(int port)
        {
            if (port < 1 || port > 65535)
                return false;

            // Try TCP
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    socket.Bind(new IPEndPoint(IPAddress.Any, port));
                    return true;
                }
            }
            catch (SocketException)
            {
                return false;
            }
        }
    }
}
