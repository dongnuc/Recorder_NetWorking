using NetworkMonitor.Abstractions;
using NetworkMonitor.Keywords;
using NetworkMonitor.Services;
using PacketDotNet;
using NUnit.Framework;
using System;

namespace NetworkMonitor.Tests
{
    [TestFixture]
    public class TcpNetworkFlowTest
    {
        private TcpPacket CreateTcpPacket(ushort srcPort, ushort dstPort)
        {
            // PacketDotNet.TcpPacket constructor: sourcePort, destinationPort
            return new TcpPacket(srcPort, dstPort);
        }

        private PacketCapturedEventArgs CreateArgs(TcpPacket tcpPacket, string payload = "")
        {
            return new PacketCapturedEventArgs
            {
                SourceIp = "192.168.1.1",
                SourcePort = tcpPacket.SourcePort,
                DestinationIp = "192.168.1.2",
                DestinationPort = tcpPacket.DestinationPort,
                TcpPacket = tcpPacket,
                DecodedPayload = payload,
                Timestamp = new DateTime(2023, 10, 25, 10, 0, 0)
            };
        }

        [Test]
        public void TC1_ToTcpNetworkFlow_ArgsNull_ReturnsEmptyObject()
        {
            var result = NetworkFlowConverter.ToTcpNetworkFlow(null);

            Assert.IsNotNull(result);
            Assert.IsNull(result.Source);
            Assert.IsNull(result.Flags);
        }

        [Test]
        public void TC2_ToTcpNetworkFlow_ValidArgs_MapsBasicFieldsCorrectly()
        {
            // Arrange
            var tcp = CreateTcpPacket(1234, 80);
            var args = CreateArgs(tcp, "Hello Server");

            // Act
            var result = NetworkFlowConverter.ToTcpNetworkFlow(args);

            // Assert
            Assert.That(result.Time, Is.EqualTo(args.Timestamp.ToString(Logging_Keywords.TimestampFormat)));
            Assert.That(result.Source, Is.EqualTo("192.168.1.1:1234"));
            Assert.That(result.Destination, Is.EqualTo("192.168.1.2:80"));
            Assert.That(result.Data, Is.EqualTo("Hello Server"));
            Assert.That(result.Info, Is.EqualTo("TCP"));
        }

        [Test]
        public void TC3_ToTcpNetworkFlow_MonitoredPortNull_ReturnsNullRoles()
        {
            var tcp = CreateTcpPacket(5000, 6000);
            var args = CreateArgs(tcp);

            // Act: Không truyền monitoredPort
            var result = NetworkFlowConverter.ToTcpNetworkFlow(args, monitoredPort: null);

            // Assert: Không xác định được vai trò
            Assert.IsNull(result.SourceRole);
            Assert.IsNull(result.DestinationRole);
        }

        /// <summary>
        /// Test Case 4: Traffic đi TỪ Port đang lắng nghe (Server) -> Port khác (Client).
        /// Source Port (8080) == Monitored Port (8080) => Source là Server.
        /// Destination Port (54321) != Monitored Port => Destination là Client.
        /// </summary>
        [Test]
        public void TC4_ToTcpNetworkFlow_SourceMatchesMonitoredPort_IdentifySourceAsServer()
        {
            // Arrange
            int monitorPort = 8080; // Đây là Port của Server
            int ephemeralPort = 54321; // Port ngẫu nhiên của Client

            // Packet từ 8080 gửi đến 54321
            var tcp = CreateTcpPacket((ushort)monitorPort, (ushort)ephemeralPort);
            var args = CreateArgs(tcp);

            // Act
            var result = NetworkFlowConverter.ToTcpNetworkFlow(args, monitorPort);

            // Assert
            Assert.That(result.SourceRole, Is.EqualTo("Server"), "Source trùng với Monitor Port nên phải là Server");
            Assert.That(result.DestinationRole, Is.EqualTo("Client"), "Destination khác Monitor Port nên phải là Client");
        }

        /// <summary>
        /// Test Case 5: Traffic đi TỪ Port khác (Client) -> Port đang lắng nghe (Server).
        /// Source Port (54321) != Monitored Port => Source là Client.
        /// </summary>
        [Test]
        public void TC5_ToTcpNetworkFlow_DestMatchesMonitoredPort_IdentifyDestAsServer()
        {
            // Arrange
            int monitorPort = 8080; // Đây là Port của Server
            int ephemeralPort = 54321; // Port ngẫu nhiên của Client

            var tcp = CreateTcpPacket((ushort)ephemeralPort, (ushort)monitorPort);
            var args = CreateArgs(tcp);

            // Act
            var result = NetworkFlowConverter.ToTcpNetworkFlow(args, monitorPort);

            // Assert
            Assert.That(result.SourceRole, Is.EqualTo("Client"), "Source khác Monitor Port nên phải là Client");
            Assert.That(result.DestinationRole, Is.EqualTo("Server"), "Destination trùng với Monitor Port nên phải là Server");
        }

      

        [Test]
        public void TC6_ToTcpNetworkFlow_FlagSYN_ReturnsCorrectState()
        {
            var tcp = CreateTcpPacket(1000, 2000);
            tcp.Synchronize = true;
            tcp.Acknowledgment = false;
            var args = CreateArgs(tcp);

            var result = NetworkFlowConverter.ToTcpNetworkFlow(args);

            Assert.That(result.Flags, Does.Contain("SYN"));
            Assert.That(result.Flags, Does.Not.Contain("ACK"));
            Assert.That(result.State, Does.Contain("Client connecting"));
        }

        [Test]
        public void TC7_ToTcpNetworkFlow_FlagSynAck_ReturnsServerRespondingState()
        {
            var tcp = CreateTcpPacket(80, 1000);
            tcp.Synchronize = true;
            tcp.Acknowledgment = true;
            var args = CreateArgs(tcp);

            var result = NetworkFlowConverter.ToTcpNetworkFlow(args);

            Assert.That(result.Flags, Is.EqualTo("SYN, ACK"));
            Assert.That(result.State, Does.Contain("Server responding"));
        }

        [Test]
        public void TC8_ToTcpNetworkFlow_FlagFinAck_ReturnsClosingState()
        {
            var tcp = CreateTcpPacket(1000, 80);
            tcp.Finished = true;
            tcp.Acknowledgment = true;
            var args = CreateArgs(tcp);

            var result = NetworkFlowConverter.ToTcpNetworkFlow(args);

            Assert.That(result.Flags, Is.EqualTo("FIN, ACK"));
            Assert.That(result.State, Does.Contain("Closing connection"));
        }

        [Test]
        public void TC9_ToTcpNetworkFlow_PushAck_ReturnsDataTransferState()
        {
            var tcp = CreateTcpPacket(1000, 80);
            tcp.Push = true;
            tcp.Acknowledgment = true;
            var args = CreateArgs(tcp, "Some Data");

            var result = NetworkFlowConverter.ToTcpNetworkFlow(args);

            Assert.That(result.Flags, Is.EqualTo("PSH, ACK"));
            Assert.That(result.State, Is.EqualTo("Data transfer in progress"));
        }

        /// <summary>
        /// Test Case:Cả Source và Dest đều KHÔNG trùng Monitored Port.
        /// Logic: Cả 2 đều coi là Client
        /// </summary>
        [Test]
        public void TC10_Extra_ToTcpNetworkFlow_NeitherMatchesMonitoredPort_BothAreClients()
        {
            // Arrange
            int monitorPort = 8080;
            // Packet đi lạc từ 1111 đến 2222 (không liên quan port 8080)
            var tcp = CreateTcpPacket(1111, 2222);
            var args = CreateArgs(tcp);

            // Act
            var result = NetworkFlowConverter.ToTcpNetworkFlow(args, monitorPort);

            // Assert
            Assert.That(result.SourceRole, Is.EqualTo("Client"));
            Assert.That(result.DestinationRole, Is.EqualTo("Client"));
        }
    }
}