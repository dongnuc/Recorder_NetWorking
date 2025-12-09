using NetworkMonitor.Abstractions;
using NetworkMonitor.Services;
using PacketDotNet;

namespace NetworkMonitor.Tests
{
    [TestFixture]
    public class ToHttpNetworkFlowTest
    {
        private TcpPacket CreateTcpPacket(ushort srcPort, ushort dstPort)
        {
            return new TcpPacket(srcPort, dstPort);
        }

        private PacketCapturedEventArgs CreateArgs(string payload, int srcPort = 1234, int dstPort = 80)
        {
            var tcp = CreateTcpPacket((ushort)srcPort, (ushort)dstPort);
            return new PacketCapturedEventArgs
            {
                SourceIp = "192.168.1.100",
                SourcePort = srcPort,
                DestinationIp = "1.2.3.4",
                DestinationPort = dstPort,
                TcpPacket = tcp,
                DecodedPayload = payload,
                Timestamp = new DateTime(2023, 10, 25, 12, 0, 0)
            };
        }

        [Test]
        public void TC1_ToHttpNetworkFlow_ArgsNull_ReturnsEmptyObject()
        {
            var result = NetworkFlowConverter.ToHttpNetworkFlow(null);
            Assert.IsNotNull(result);
            Assert.IsNull(result.Source);
        }

        /// <summary>
        /// TC2: Parse HTTP Request hợp lệ (GET)
        /// </summary>
        [Test]
        public void TC2_ToHttpNetworkFlow_ValidHttpRequest_ParsesMethodAndUri()
        {
            string payload = "GET /index.html HTTP/1.1\r\nHost: example.com\r\nUser-Agent: TestAgent\r\n\r\n";
            var args = CreateArgs(payload);

            var result = NetworkFlowConverter.ToHttpNetworkFlow(args);

            Assert.That(result.Info, Is.EqualTo("HTTP Request"));
            Assert.That(result.Method, Is.EqualTo("GET"));
            Assert.That(result.URI, Is.EqualTo("/index.html"));
            Assert.That(result.HttpVersion, Is.EqualTo("HTTP/1.1"));
            Assert.That(result.Host, Is.EqualTo("example.com"));
            Assert.That(result.HttpHeaders, Does.Contain("User-Agent: TestAgent"));
        }

        /// <summary>
        /// TC3: Parse HTTP Response hợp lệ (200 OK)
        /// </summary>
        [Test]
        public void TC3_ToHttpNetworkFlow_ValidHttpResponse_ParsesStatus()
        {
            string payload = "HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nContent-Length: 5\r\n\r\nHello";
            var args = CreateArgs(payload, 80, 1234);

            var result = NetworkFlowConverter.ToHttpNetworkFlow(args);

            Assert.That(result.Info, Is.EqualTo("HTTP Response"));
            Assert.That(result.Status, Is.EqualTo("200 OK"));
            Assert.That(result.HttpVersion, Is.EqualTo("HTTP/1.1"));
            Assert.That(result.HttpBody, Is.EqualTo("Hello"));
            Assert.That(result.HttpHeaders, Does.Contain("Content-Type: text/html"));
        }
        /// <summary>
        /// TC4: Payload rỗng (Không phải HTTP)
        /// </summary>
        [Test]
        public void TC4_ToHttpNetworkFlow_EmptyPayload_ReturnsBasicHttpInfo()
        {
            var args = CreateArgs("");

            var result = NetworkFlowConverter.ToHttpNetworkFlow(args);

            Assert.That(result.Info, Is.EqualTo("HTTP")); // Default info
            Assert.IsNull(result.Method);
            Assert.IsNull(result.Status);
        }

        /// <summary>
        /// TC5: Payload là dữ liệu rác (Không phải HTTP chuẩn)
        /// </summary>
        [Test]
        public void TC5_ToHttpNetworkFlow_GarbagePayload_ReturnsBasicHttpInfo()
        {
            var args = CreateArgs("This is just some random tcp data");

            var result = NetworkFlowConverter.ToHttpNetworkFlow(args);

            Assert.That(result.Info, Is.EqualTo("HTTP"));
            Assert.IsNull(result.Method); 
        }

        /// <summary>
        /// TC6: Kiểm tra Role (Server/Client) - Request từ Client
        /// </summary>
        [Test]
        public void TC6_ToHttpNetworkFlow_RequestWithMonitoredPort_IdentifiesRoles()
        {
            int monitoredPort = 8080;
            string payload = "POST /api/login HTTP/1.1\r\nHost: localhost:8080\r\n\r\n";
            var args = CreateArgs(payload, srcPort: 54321, dstPort: monitoredPort);

            var result = NetworkFlowConverter.ToHttpNetworkFlow(args, monitoredPort);

            Assert.That(result.SourceRole, Is.EqualTo("Client"));
            Assert.That(result.DestinationRole, Is.EqualTo("Server"));
            Assert.That(result.Method, Is.EqualTo("POST"));
        }

        /// <summary>
        /// TC7: Kiểm tra Role - Response từ Server
        /// </summary>
        [Test]
        public void TC7_ToHttpNetworkFlow_ResponseWithMonitoredPort_IdentifiesRoles()
        {
            int monitoredPort = 8080;
            // Response từ Server (8080) -> Client (random port)
            string payload = "HTTP/1.1 404 Not Found\r\n\r\n";
            var args = CreateArgs(payload, srcPort: monitoredPort, dstPort: 54321);

            var result = NetworkFlowConverter.ToHttpNetworkFlow(args, monitoredPort);

            Assert.That(result.SourceRole, Is.EqualTo("Server"));
            Assert.That(result.DestinationRole, Is.EqualTo("Client"));
            Assert.That(result.Status, Is.EqualTo("404 Not Found"));
        }

        /// <summary>
        /// TC8: HTTP Body có xuống dòng (Multiline Body)
        /// </summary>
        [Test]
        public void TC8_ToHttpNetworkFlow_MultilineBody_PreservesFormat()
        {
            string payload = "POST /submit HTTP/1.1\r\n\r\nLine 1\nLine 2";
            var args = CreateArgs(payload);

            var result = NetworkFlowConverter.ToHttpNetworkFlow(args);

            Assert.That(result.HttpBody, Is.EqualTo("Line 1\nLine 2"));
        }
    }
}
