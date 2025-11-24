# GIẢI THÍCH CHI TIẾT MODULE NETWORKMONITOR

## Tổng quan

NetworkMonitor là một thư viện C# được xây dựng để **bắt và phân tích các gói tin mạng** (network packets) theo thời gian thực. Nó sử dụng thư viện SharpPcap để lắng nghe lưu lượng mạng và cung cấp thông tin chi tiết về các kết nối TCP/UDP, đặc biệt là các yêu cầu và phản hồi HTTP.

### Mục đích chính:
- **Giám sát lưu lượng mạng**: Theo dõi các gói tin đi vào/ra khỏi máy tính
- **Phân tích giao thức**: Nhận diện và phân tích TCP, UDP, HTTP
- **Ghi lại dữ liệu**: Lưu trữ thông tin chi tiết về các kết nối mạng
- **Hỗ trợ debug**: Giúp developer kiểm tra API calls, kiểm tra network traffic

---

## Kiến trúc hệ thống

NetworkMonitor được tổ chức thành các thành phần sau:

```
NetworkMonitor/
├── Abstractions/          # Interfaces và contracts
│   └── IPacketCaptureService.cs
├── Services/              # Logic xử lý chính
│   ├── PacketCaptureService.cs      (Core service - Bắt gói tin)
│   ├── NetworkFlowConverter.cs      (Chuyển đổi dữ liệu)
│   └── PacketFormatter.cs           (Định dạng output)
├── Models/                # Các đối tượng dữ liệu
│   ├── TcpNetworkFlow.cs
│   ├── HttpNetworkFlow.cs
│   └── PacketInfo.cs
├── Keywords/              # Hằng số và từ khóa
│   ├── Network_Keywords.cs
│   ├── Service_Keywords.cs
│   ├── Validation_Keywords.cs
│   └── Logging_Keywords.cs
└── Examples/              # Ví dụ sử dụng
    ├── ServiceExample.cs
    └── NetworkFlowExample.cs
```

---

## LUỒNG HOẠT ĐỘNG CHI TIẾT

### BƯỚC 1: KHỞI TẠO SERVICE

```csharp
var service = new PacketCaptureService();
```

**Điều gì xảy ra:**
- Tạo một instance của `PacketCaptureService`
- Khởi tạo các biến nội bộ:
  - `_capturedPackets`: Danh sách lưu trữ gói tin (tối đa 1000 gói)
  - `_packetsLock`: Object để đồng bộ hóa thread-safe
  - `_monitoredPorts`: Danh sách cổng cần giám sát
  - `_monitorAllPorts`: Flag để giám sát tất cả cổng

---

### BƯỚC 2: CẤU HÌNH

```csharp
// Bật logging tự động
service.LogCapturedPackets = true;

// Đăng ký sự kiện để nhận thông báo
service.LogMessage += (sender, args) => {
    Console.WriteLine(args.Message);
};

service.PacketCaptured += (sender, args) => {
    // Xử lý khi có gói tin mới
};
```

**Điều gì xảy ra:**
- **LogCapturedPackets = true**: Khi bật, mỗi gói tin bắt được sẽ tự động ghi log
- **LogMessage event**: Nhận các thông báo về trạng thái (bắt đầu, dừng, lỗi, gói tin mới)
- **PacketCaptured event**: Được kích hoạt mỗi khi bắt được gói tin mới

---

### BƯỚC 3: CHỌN NETWORK DEVICE

```csharp
var devices = CaptureDeviceList.Instance;
if (devices.Count > 0)
{
    var device = devices[0];  // Chọn adapter mạng đầu tiên
}
```

**Điều gì xảy ra:**
- Lấy danh sách tất cả network adapters trên máy tính
- Mỗi device đại diện cho một card mạng (WiFi, Ethernet, Loopback, v.v.)
- Bạn có thể chọn device bất kỳ để giám sát

**Ví dụ devices:**
- `Ethernet adapter`: Card mạng có dây
- `WiFi adapter`: Card mạng không dây  
- `Loopback adapter`: Giám sát traffic nội bộ (localhost)

---

### BƯỚC 4: CHỌN CHỂ ĐỘ GIÁM SÁT CỔNG

NetworkMonitor hỗ trợ 4 chế độ lọc cổng:

#### 4.1. Chế độ "all" - Giám sát tất cả cổng
```csharp
await service.StartCaptureAsync(device, "all", null, cts.Token);
```
- Bắt tất cả các gói tin TCP/UDP trên mọi cổng
- Dùng khi muốn giám sát toàn bộ network traffic

#### 4.2. Chế độ "common" - Giám sát cổng phổ biến
```csharp
await service.StartCaptureAsync(device, "common", null, cts.Token);
```
- Chỉ bắt traffic trên các cổng: **80, 443, 8000, 8080, 8888**
- Phù hợp cho giám sát HTTP/HTTPS traffic thông thường

#### 4.3. Chế độ "targeted" - Giám sát cổng đích
```csharp
await service.StartCaptureAsync(device, "targeted", null, cts.Token);
```
- Chỉ bắt traffic trên các cổng: **5000, 8080**
- Phù hợp cho ứng dụng .NET Core (thường dùng port 5000)

#### 4.4. Chế độ "custom" - Tùy chỉnh cổng
```csharp
await service.StartCaptureAsync(device, "custom", "3000,8080,9000", cts.Token);
```
- Tự định nghĩa danh sách cổng cần giám sát
- Cách thức: truyền chuỗi các số cổng cách nhau bởi dấu phẩy

---

### BƯỚC 5: BẮT ĐẦU CAPTURE (StartCaptureAsync)

```csharp
var cts = new CancellationTokenSource();
await service.StartCaptureAsync(device, "common", null, cts.Token);
```

**Điều gì xảy ra trong StartCaptureAsync:**

#### 5.1. Parse cấu hình cổng
```csharp
(_monitoredPorts, _monitorAllPorts) = ParsePorts(portsArg);
```
- Phân tích chuỗi cổng đầu vào
- Chuyển đổi thành danh sách số nguyên (int[])
- Xác định có giám sát tất cả cổng hay không

#### 5.2. Mở device ở chế độ Promiscuous
```csharp
device.Open(DeviceModes.Promiscuous, 1000);
```
- **Promiscuous Mode**: Cho phép bắt tất cả gói tin trên network, không chỉ gói tin gửi đến máy này
- Timeout: 1000ms (1 giây)

#### 5.3. Xây dựng BPF Filter
```csharp
string filter = BuildPacketFilter();
device.Filter = filter;
```

**BPF (Berkeley Packet Filter)** là cú pháp để lọc gói tin ở tầng kernel:

**Ví dụ filter được tạo ra:**
```
// Với "common" mode (ports: 80, 443, 8080):
tcp port 80 or udp port 80 or tcp port 443 or udp port 443 or tcp port 8080 or udp port 8080

// Với "all" mode:
tcp or udp
```

Filter này được áp dụng ngay tại driver level, rất hiệu quả!

#### 5.4. Đăng ký callback handler
```csharp
device.OnPacketArrival += Device_OnPacketArrival;
```
- Mỗi khi có gói tin mới, method `Device_OnPacketArrival` sẽ được gọi
- Đây là event handler chính xử lý gói tin

#### 5.5. Bắt đầu capture
```csharp
device.StartCapture();
_isCapturing = true;
```
- Bắt đầu lắng nghe network traffic
- Thread riêng sẽ chạy để bắt gói tin liên tục

#### 5.6. Loop chờ cancellation
```csharp
while (!cancellationToken.IsCancellationRequested)
{
    Thread.Sleep(100);
}
```
- Giữ cho capture tiếp tục chạy
- Chỉ dừng khi người dùng gọi `cts.Cancel()`

---

### BƯỚC 6: XỬ LÝ MỖI GÓI TIN (Device_OnPacketArrival)

Đây là trái tim của hệ thống! Mỗi khi bắt được gói tin, luồng này chạy:

#### 6.1. Parse Raw Packet
```csharp
var raw = e.GetPacket();
var packet = Packet.ParsePacket(raw.LinkLayerType, raw.Data);
```
- Lấy dữ liệu thô từ event
- Parse thành object Packet có cấu trúc

**Cấu trúc gói tin mạng (OSI Model):**
```
┌─────────────────────────┐
│   Ethernet Layer        │ <- Link layer
├─────────────────────────┤
│   IP Layer              │ <- Network layer (IPv4/IPv6)
├─────────────────────────┤
│   TCP/UDP Layer         │ <- Transport layer
├─────────────────────────┤
│   HTTP/Data Layer       │ <- Application layer
└─────────────────────────┘
```

#### 6.2. Trích xuất IP Layer
```csharp
var ipPacket = packet.Extract<IPPacket>();
string srcIp = ipPacket?.SourceAddress?.ToString() ?? "(unknown)";
string dstIp = ipPacket?.DestinationAddress?.ToString() ?? "(unknown)";
```
- Lấy địa chỉ IP nguồn và đích
- Hỗ trợ cả IPv4 và IPv6

#### 6.3. Trích xuất TCP/UDP Layer
```csharp
var tcp = packet.Extract<TcpPacket>();
var udp = packet.Extract<UdpPacket>();

if (tcp != null)
{
    srcPort = tcp.SourcePort;
    dstPort = tcp.DestinationPort;
    protocolLabel = "TCP";
    
    // Lấy payload (dữ liệu ứng dụng)
    if (tcp.PayloadData != null && tcp.PayloadData.Length > 0)
    {
        decodedPayload = Encoding.UTF8.GetString(tcp.PayloadData);
    }
}
```

**TCP Flags được trích xuất:**
- **SYN**: Bắt đầu kết nối
- **ACK**: Xác nhận nhận được dữ liệu
- **FIN**: Đóng kết nối
- **RST**: Reset kết nối (lỗi)
- **PSH**: Push dữ liệu ngay lập tức
- **URG**: Dữ liệu khẩn cấp
- **ECE**: ECN-Echo (congestion notification)
- **CWR**: Congestion Window Reduced

#### 6.4. Kiểm tra Port Filter
```csharp
if (!_monitorAllPorts && _monitoredPorts != null)
{
    bool match = (_monitoredPorts.Contains(srcPort) || 
                  _monitoredPorts.Contains(dstPort));
    if (!match) return; // Bỏ qua gói tin này
}
```
- Chỉ xử lý gói tin nếu source hoặc destination port nằm trong danh sách giám sát
- Bỏ qua các gói tin không liên quan

#### 6.5. Phát hiện HTTP Protocol
```csharp
var httpLabel = DetectHttpLabel(decodedPayload);
if (!string.IsNullOrEmpty(httpLabel))
    protocolLabel = httpLabel;
```

**Cách phát hiện HTTP:**

```csharp
private string? DetectHttpLabel(string? payload)
{
    // Kiểm tra có chứa "HTTP/"
    if (payload.Contains("HTTP/"))
        return "HTTP";
    
    // Kiểm tra HTTP methods: GET, POST, PUT, DELETE...
    if (payload.StartsWith("GET ") || 
        payload.StartsWith("POST ") ||
        payload.StartsWith("PUT ") ||
        payload.StartsWith("DELETE "))
        return "HTTP";
        
    return null;
}
```

#### 6.6. Tạo PacketCapturedEventArgs
```csharp
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
```
- Đóng gói tất cả thông tin gói tin vào một object
- Object này sẽ được lưu trữ và xử lý tiếp

#### 6.7. Lưu trữ Packet
```csharp
StorePacket(eventArgs);
```

```csharp
private void StorePacket(PacketCapturedEventArgs eventArgs)
{
    lock (_packetsLock)
    {
        _capturedPackets.Add(eventArgs);
        
        // Giới hạn 1000 gói tin để tránh memory leak
        if (_capturedPackets.Count > 1000)
        {
            _capturedPackets.RemoveAt(0); // Xóa gói tin cũ nhất
        }
    }
}
```
- Lưu vào buffer trong bộ nhớ
- Thread-safe với `lock`
- Tự động xóa gói tin cũ khi đầy

#### 6.8. Log nếu được bật
```csharp
if (_logCapturedPackets)
{
    var monitoredPort = GetMatchingMonitoredPort(srcPort, dstPort);
    object flow = ConvertPacketToNetworkFlow(eventArgs, monitoredPort);
    var flowJson = NetworkFlowConverter.ToJson(flow);
    RaiseLogMessage($"[Network Flow Captured]\n{flowJson}", false);
}
```
- Nếu `LogCapturedPackets = true`
- Chuyển đổi packet thành NetworkFlow object (TcpNetworkFlow hoặc HttpNetworkFlow)
- Serialize thành JSON
- Gửi qua LogMessage event

#### 6.9. Raise Event
```csharp
PacketCaptured?.Invoke(this, eventArgs);
```
- Thông báo cho tất cả subscribers
- Cho phép UI hoặc logic khác xử lý gói tin real-time

---

### BƯỚC 7: CHUYỂN ĐỔI DỮ LIỆU (NetworkFlowConverter)

Service này chuyển đổi raw packet data thành structured objects có ý nghĩa.

#### 7.1. Chuyển đổi thành TcpNetworkFlow (cho TCP thường)

```csharp
public static TcpNetworkFlow ToTcpNetworkFlow(PacketCapturedEventArgs args, int? monitoredPort)
{
    var timestamp = args.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    var source = $"{args.SourceIp}:{args.SourcePort}";
    var destination = $"{args.DestinationIp}:{args.DestinationPort}";
    
    // Xác định vai trò Server/Client
    var (sourceRole, destinationRole) = DetermineRoles(
        args.SourcePort, 
        args.DestinationPort, 
        monitoredPort
    );
    
    // Lấy TCP flags và state
    var (flags, state) = GetTcpFlagsAndState(args.TcpPacket);
    
    return new TcpNetworkFlow
    {
        Time = timestamp,
        Info = "TCP",
        Source = source,
        Destination = destination,
        Flags = flags,              // "SYN, ACK"
        State = state,              // "Connection established"
        Data = args.DecodedPayload,
        SourceRole = sourceRole,    // "Client" or "Server"
        DestinationRole = destinationRole
    };
}
```

**Logic xác định Server/Client:**
```csharp
private static (string?, string?) DetermineRoles(int sourcePort, int destinationPort, int? monitoredPort)
{
    if (!monitoredPort.HasValue)
        return (null, null);
    
    // Port nào khớp với monitoredPort thì là Server
    string sourceRole = sourcePort == monitoredPort.Value ? "Server" : "Client";
    string destinationRole = destinationPort == monitoredPort.Value ? "Server" : "Client";
    
    return (sourceRole, destinationRole);
}
```

**Ví dụ:**
```
Giám sát port 5000 (server của bạn)
Gói tin: 192.168.1.100:54321 -> 192.168.1.50:5000

=> SourceRole = "Client" (port 54321 != 5000)
=> DestinationRole = "Server" (port 5000 == 5000)
```

**Xác định Connection State:**
```csharp
private static string? DetermineConnectionState(TcpPacket tcp)
{
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
```

#### 7.2. Chuyển đổi thành HttpNetworkFlow (cho HTTP)

Đối với gói tin HTTP, ngoài thông tin TCP, còn parse thêm HTTP data:

```csharp
public static HttpNetworkFlow ToHttpNetworkFlow(PacketCapturedEventArgs args, int? monitoredPort)
{
    // ... giống TcpNetworkFlow ...
    
    // Parse HTTP data
    var httpData = ParseHttpData(args.DecodedPayload);
    
    // Xác định Request hay Response
    string info;
    if (!string.IsNullOrEmpty(httpData.Method))
        info = "HTTP Request";
    else if (!string.IsNullOrEmpty(httpData.Status))
        info = "HTTP Response";
    else
        info = "HTTP";
    
    return new HttpNetworkFlow
    {
        Time = timestamp,
        Info = info,
        Source = source,
        Destination = destination,
        Flags = flags,
        State = state,
        URI = httpData.Uri,           // "/api/users"
        Host = httpData.Host,         // "localhost:5000"
        Method = httpData.Method,     // "GET", "POST", etc.
        Status = httpData.Status,     // "200 OK"
        HttpVersion = httpData.HttpVersion, // "HTTP/1.1"
        HttpHeaders = httpData.Headers,
        HttpBody = httpData.Body,
        SourceRole = sourceRole,
        DestinationRole = destinationRole
    };
}
```

**HTTP Request Example:**
```
GET /api/users HTTP/1.1
Host: localhost:5000
User-Agent: Mozilla/5.0
Content-Type: application/json

{"name": "John"}
```

**Parse thành:**
```json
{
  "Method": "GET",
  "URI": "/api/users",
  "HttpVersion": "HTTP/1.1",
  "Host": "localhost:5000",
  "HttpHeaders": "Host: localhost:5000; User-Agent: Mozilla/5.0; Content-Type: application/json",
  "HttpBody": "{\"name\": \"John\"}"
}
```

**HTTP Response Example:**
```
HTTP/1.1 200 OK
Content-Type: application/json
Content-Length: 45

{"id": 1, "name": "John", "email": "john@example.com"}
```

**Parse thành:**
```json
{
  "Status": "200 OK",
  "HttpVersion": "HTTP/1.1",
  "HttpHeaders": "Content-Type: application/json; Content-Length: 45",
  "HttpBody": "{\"id\": 1, \"name\": \"John\", \"email\": \"john@example.com\"}"
}
```

---

### BƯỚC 8: TRUY VẤN DỮ LIỆU ĐÃ BẮT

Sau khi bắt được gói tin, có nhiều cách để lấy dữ liệu:

#### 8.1. Lấy dưới dạng String (formatted)

```csharp
// Summary format (1 dòng)
var packets = service.GetCapturedPacketsAsStrings("summary");
// Output: "HTTP: 192.168.1.100:54321 -> 192.168.1.1:80 (has payload)"

// Detailed format (nhiều dòng)
var packets = service.GetCapturedPacketsAsStrings("detailed");
// Output:
// Protocol: HTTP
// Source: 192.168.1.100:54321
// Destination: 192.168.1.1:80
// TCP Flags: PSH, ACK
// Payload: GET /api/users HTTP/1.1...

// JSON format
var packets = service.GetCapturedPacketsAsStrings("json");
// Output: {"protocol": "HTTP", "source": "...", ...}
```

#### 8.2. Lấy dưới dạng NetworkFlow Objects

```csharp
// Lấy tất cả packets dưới dạng objects
var flows = service.GetCapturedNetworkFlows();

foreach (var flow in flows)
{
    if (flow is HttpNetworkFlow httpFlow)
    {
        Console.WriteLine($"HTTP {httpFlow.Method} {httpFlow.URI}");
        Console.WriteLine($"Status: {httpFlow.Status}");
        Console.WriteLine($"Host: {httpFlow.Host}");
    }
    else if (flow is TcpNetworkFlow tcpFlow)
    {
        Console.WriteLine($"TCP {tcpFlow.Flags}");
        Console.WriteLine($"State: {tcpFlow.State}");
    }
}
```

#### 8.3. Lấy dưới dạng JSON

```csharp
// Lấy tất cả packets dưới dạng JSON strings
var jsonFlows = service.GetCapturedNetworkFlowsAsJson();

foreach (var json in jsonFlows)
{
    Console.WriteLine(json);
    // Có thể parse lại thành object hoặc lưu vào file
}
```

#### 8.4. Lấy gói tin gần nhất

```csharp
// Lấy 10 gói tin gần nhất
var recentPackets = service.GetRecentPacketsAsStrings(10, "summary");

// Lấy 5 network flows gần nhất
var recentFlows = service.GetRecentNetworkFlows(5);

// Lấy 3 flows gần nhất dưới dạng JSON
var recentJsonFlows = service.GetRecentNetworkFlowsAsJson(3);
```

#### 8.5. Kiểm tra số lượng và xóa buffer

```csharp
// Kiểm tra có bao nhiêu gói tin đã bắt
int count = service.GetCapturedPacketCount();
Console.WriteLine($"Total: {count} packets");

// Xóa tất cả gói tin đã lưu
service.ClearCapturedPackets();
```

---

### BƯỚC 9: DỪNG CAPTURE

```csharp
service.StopCapture();
```

**Điều gì xảy ra:**
```csharp
public void StopCapture()
{
    if (_device != null && _isCapturing)
    {
        _device.StopCapture();  // Dừng bắt gói tin
        _device.Close();        // Đóng device
        _isCapturing = false;   // Cập nhật flag
        
        RaiseLogMessage("Sniffer stopped capture", false);
    }
}
```
- Dừng việc lắng nghe network traffic
- Đóng network device
- Dữ liệu đã bắt vẫn còn trong buffer (không bị xóa)

---

## LUỒNG DỮ LIỆU HOÀN CHỈNH

Hình ảnh minh họa luồng dữ liệu từ đầu đến cuối:

```
┌─────────────────────────────────────────────────────────────────┐
│                         USER CODE                               │
│  var service = new PacketCaptureService();                      │
│  service.StartCaptureAsync(device, "common", null, token);      │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│                    PACKET CAPTURE SERVICE                       │
│  • Mở device (Promiscuous mode)                                 │
│  • Áp dụng BPF filter (tcp port 80 or tcp port 443...)          │
│  • Đăng ký callback: Device_OnPacketArrival                     │
│  • Bắt đầu capture: device.StartCapture()                       │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│                    NETWORK INTERFACE (NIC)                      │
│  Lắng nghe tất cả network traffic đi qua card mạng              │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│                       BPF FILTER (Kernel)                       │
│  Lọc gói tin theo filter → Chỉ giữ lại TCP/UDP trên port       │
│  cần thiết                                                       │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│                 SHARPPCAP LIBRARY (C# Wrapper)                  │
│  Nhận raw packet data từ libpcap/WinPcap                        │
│  Trigger event: OnPacketArrival                                 │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│              Device_OnPacketArrival (Event Handler)             │
│  1. Parse packet: Extract IP, TCP/UDP layers                    │
│  2. Decode payload: UTF8 hoặc Hex                               │
│  3. Detect HTTP: Kiểm tra HTTP methods/headers                  │
│  4. Check port filter: Có thuộc monitored ports?                │
│  5. Create PacketCapturedEventArgs                              │
│  6. Store packet: Thêm vào _capturedPackets (max 1000)          │
│  7. Log nếu enabled: Convert to NetworkFlow → JSON              │
│  8. Raise events: PacketCaptured, LogMessage                    │
└────────────────────────┬────────────────────────────────────────┘
                         │
         ┌───────────────┴───────────────┐
         ▼                               ▼
┌──────────────────┐           ┌──────────────────────┐
│  MEMORY BUFFER   │           │  EVENT SUBSCRIBERS   │
│  _capturedPackets│           │  • UI Updates        │
│  (List<Packet>)  │           │  • Logging           │
│  Max 1000 items  │           │  • Real-time display │
└──────────────────┘           └──────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────────┐
│                   DATA RETRIEVAL (Query)                        │
│  • GetCapturedPacketsAsStrings("summary/detailed/json")         │
│  • GetCapturedNetworkFlows() → TcpNetworkFlow/HttpNetworkFlow  │
│  • GetCapturedNetworkFlowsAsJson() → JSON strings               │
│  • GetRecentPacketsAsStrings(count) → Recent N packets          │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│                  NETWORK FLOW CONVERTER                         │
│  • ToTcpNetworkFlow() → TcpNetworkFlow object                   │
│  • ToHttpNetworkFlow() → HttpNetworkFlow object                 │
│  • ParseHttpData() → Extract Method, URI, Headers, Body         │
│  • DetermineRoles() → Server/Client based on monitored port     │
│  • GetTcpFlagsAndState() → TCP flags & connection state         │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│                    FORMATTED OUTPUT                             │
│  JSON, Summary, Detailed, or Structured Objects                 │
│  → Sẵn sàng để hiển thị, lưu file, hoặc phân tích tiếp          │
└─────────────────────────────────────────────────────────────────┘
```

---

## CÁC TRƯỜNG HỢP SỬ DỤNG THỰC TẾ

### 1. Giám sát API Calls của ứng dụng .NET

```csharp
var service = new PacketCaptureService();
service.LogCapturedPackets = true;

service.LogMessage += (sender, args) => {
    // Log to file
    File.AppendAllText("api_calls.log", args.Message + "\n");
};

var device = CaptureDeviceList.Instance[0];
var cts = new CancellationTokenSource();

// Giám sát port 5000 (ASP.NET Core default)
await service.StartCaptureAsync(device, "targeted", null, cts.Token);

// Chạy trong 1 phút
await Task.Delay(60000);

// Lấy tất cả HTTP flows
var httpFlows = service.GetCapturedNetworkFlows()
    .OfType<HttpNetworkFlow>()
    .ToList();

// Phân tích
foreach (var flow in httpFlows)
{
    Console.WriteLine($"{flow.Method} {flow.URI} - Status: {flow.Status}");
}

service.StopCapture();
```

### 2. Debug HTTP Request/Response

```csharp
var service = new PacketCaptureService();

service.PacketCaptured += (sender, args) =>
{
    if (args.ProtocolLabel == "HTTP")
    {
        Console.WriteLine($"\n=== HTTP Packet Captured ===");
        Console.WriteLine($"From: {args.SourceIp}:{args.SourcePort}");
        Console.WriteLine($"To: {args.DestinationIp}:{args.DestinationPort}");
        Console.WriteLine($"Payload:\n{args.DecodedPayload}");
        Console.WriteLine("========================\n");
    }
};

// Giám sát port 8080
await service.StartCaptureAsync(device, "custom", "8080", cts.Token);
```

### 3. Giám sát Performance - Đếm số requests

```csharp
var service = new PacketCaptureService();
var requestCount = 0;
var responseCount = 0;

service.PacketCaptured += (sender, args) =>
{
    if (args.DecodedPayload?.StartsWith("GET") == true ||
        args.DecodedPayload?.StartsWith("POST") == true)
    {
        Interlocked.Increment(ref requestCount);
    }
    else if (args.DecodedPayload?.StartsWith("HTTP/") == true)
    {
        Interlocked.Increment(ref responseCount);
    }
};

await service.StartCaptureAsync(device, "common", null, cts.Token);

// Mỗi 5 giây in ra thống kê
while (true)
{
    await Task.Delay(5000);
    Console.WriteLine($"Requests: {requestCount}, Responses: {responseCount}");
}
```

### 4. Export to Excel/CSV

```csharp
var service = new PacketCaptureService();

// Capture trong 30 giây
await service.StartCaptureAsync(device, "all", null, cts.Token);
await Task.Delay(30000);
service.StopCapture();

// Lấy dữ liệu
var flows = service.GetCapturedNetworkFlows();

// Export to CSV
var csv = new StringBuilder();
csv.AppendLine("Time,Info,Source,Destination,Flags,State");

foreach (var flow in flows)
{
    if (flow is TcpNetworkFlow tcpFlow)
    {
        csv.AppendLine($"{tcpFlow.Time},{tcpFlow.Info},{tcpFlow.Source},{tcpFlow.Destination},{tcpFlow.Flags},{tcpFlow.State}");
    }
    else if (flow is HttpNetworkFlow httpFlow)
    {
        csv.AppendLine($"{httpFlow.Time},{httpFlow.Method} {httpFlow.URI},{httpFlow.Source},{httpFlow.Destination},{httpFlow.Status},");
    }
}

File.WriteAllText("network_traffic.csv", csv.ToString());
```

---

## CẤU TRÚC DỮ LIỆU OUTPUT

### TcpNetworkFlow (TCP Packet)

```json
{
  "Time": "2025-11-24 12:30:45",
  "Info": "TCP",
  "Source": "192.168.1.100:54321",
  "Destination": "192.168.1.50:5000",
  "Flags": "SYN",
  "State": "Client connecting to server (SYN)",
  "Data": null,
  "SourceRole": "Client",
  "DestinationRole": "Server"
}
```

**Giải thích:**
- **Time**: Thời điểm bắt gói tin
- **Info**: Loại packet (TCP)
- **Source/Destination**: IP:Port nguồn và đích
- **Flags**: Các TCP flags (SYN, ACK, FIN, RST, PSH, URG, ECE, CWR)
- **State**: Trạng thái kết nối dựa vào flags
- **Data**: Payload data (nếu có)
- **SourceRole/DestinationRole**: Server hay Client (dựa vào monitored port)

### HttpNetworkFlow (HTTP Packet)

#### HTTP Request Example:
```json
{
  "Time": "2025-11-24 12:30:46",
  "Info": "HTTP Request",
  "Source": "192.168.1.100:54322",
  "Destination": "192.168.1.50:5000",
  "Flags": "PSH, ACK",
  "State": "Data transfer in progress",
  "URI": "/api/users/123",
  "Host": "localhost:5000",
  "Method": "GET",
  "Status": null,
  "HttpVersion": "HTTP/1.1",
  "HttpHeaders": "Host: localhost:5000; User-Agent: Mozilla/5.0; Accept: application/json",
  "HttpBody": null,
  "SourceRole": "Client",
  "DestinationRole": "Server"
}
```

#### HTTP Response Example:
```json
{
  "Time": "2025-11-24 12:30:47",
  "Info": "HTTP Response",
  "Source": "192.168.1.50:5000",
  "Destination": "192.168.1.100:54322",
  "Flags": "PSH, ACK",
  "State": "Data transfer in progress",
  "URI": null,
  "Host": null,
  "Method": null,
  "Status": "200 OK",
  "HttpVersion": "HTTP/1.1",
  "HttpHeaders": "Content-Type: application/json; Content-Length: 87",
  "HttpBody": "{\"id\":123,\"name\":\"John Doe\",\"email\":\"john@example.com\"}",
  "SourceRole": "Server",
  "DestinationRole": "Client"
}
```

---

## TCP 3-WAY HANDSHAKE EXAMPLE

Khi bắt được một kết nối TCP hoàn chỉnh, bạn sẽ thấy:

### Packet 1: SYN (Client → Server)
```json
{
  "Info": "TCP",
  "Source": "192.168.1.100:54321",
  "Destination": "192.168.1.50:5000",
  "Flags": "SYN",
  "State": "Client connecting to server (SYN)",
  "SourceRole": "Client",
  "DestinationRole": "Server"
}
```

### Packet 2: SYN-ACK (Server → Client)
```json
{
  "Info": "TCP",
  "Source": "192.168.1.50:5000",
  "Destination": "192.168.1.100:54321",
  "Flags": "SYN, ACK",
  "State": "Server responding (SYN-ACK)",
  "SourceRole": "Server",
  "DestinationRole": "Client"
}
```

### Packet 3: ACK (Client → Server)
```json
{
  "Info": "TCP",
  "Source": "192.168.1.100:54321",
  "Destination": "192.168.1.50:5000",
  "Flags": "ACK",
  "State": "Connection established",
  "SourceRole": "Client",
  "DestinationRole": "Server"
}
```

### Packet 4: PSH, ACK (HTTP Request)
```json
{
  "Info": "HTTP Request",
  "Source": "192.168.1.100:54321",
  "Destination": "192.168.1.50:5000",
  "Flags": "PSH, ACK",
  "State": "Data transfer in progress",
  "Method": "GET",
  "URI": "/api/users",
  "SourceRole": "Client",
  "DestinationRole": "Server"
}
```

### Packet 5: PSH, ACK (HTTP Response)
```json
{
  "Info": "HTTP Response",
  "Source": "192.168.1.50:5000",
  "Destination": "192.168.1.100:54321",
  "Flags": "PSH, ACK",
  "State": "Data transfer in progress",
  "Status": "200 OK",
  "HttpBody": "[...]",
  "SourceRole": "Server",
  "DestinationRole": "Client"
}
```

### Packet 6: FIN-ACK (Closing connection)
```json
{
  "Info": "TCP",
  "Source": "192.168.1.100:54321",
  "Destination": "192.168.1.50:5000",
  "Flags": "FIN, ACK",
  "State": "Closing connection (FIN-ACK)",
  "SourceRole": "Client",
  "DestinationRole": "Server"
}
```

---

## CÁC ĐIỂM QUAN TRỌNG CẦN NHỚ

### 1. Thread Safety
- Tất cả operations trên `_capturedPackets` đều được bảo vệ bởi `lock`
- An toàn khi nhiều threads truy cập đồng thời
- Event handlers có thể chạy trên thread khác nhau

### 2. Memory Management
- Buffer giới hạn 1000 packets
- Tự động xóa packets cũ khi đầy
- Có thể clear manually: `service.ClearCapturedPackets()`

### 3. Performance
- BPF filter áp dụng ở kernel level → Rất nhanh
- Chỉ parse các packets cần thiết
- Compiled Regex cho HTTP detection

### 4. Security & Permissions
- Cần quyền Administrator/Root để capture packets
- Promiscuous mode có thể bị disable bởi network admin
- Cẩn thận với dữ liệu nhạy cảm (passwords, tokens)

### 5. Platform Support
- Windows: Cần WinPcap hoặc Npcap
- Linux: Cần libpcap
- macOS: Cần libpcap

---

## KẾT LUẬN

NetworkMonitor là một công cụ mạnh mẽ để:
- ✅ Bắt và phân tích network traffic real-time
- ✅ Debug API calls và HTTP communications
- ✅ Giám sát performance và network behavior
- ✅ Export dữ liệu để phân tích offline

**Workflow tổng quát:**
1. Khởi tạo service
2. Chọn network device
3. Cấu hình port filtering
4. Bắt đầu capture
5. Xử lý packets real-time (event-based) hoặc query sau (service-based)
6. Convert sang structured formats (TcpNetworkFlow, HttpNetworkFlow)
7. Export hoặc phân tích dữ liệu
8. Dừng capture

**Use cases chính:**
- Development: Debug API calls, test HTTP endpoints
- Testing: Verify network behavior, check request/response
- Monitoring: Track network performance, detect anomalies
- Security: Analyze traffic patterns, detect suspicious activity

---

## TÀI LIỆU THAM KHẢO

- **SharpPcap Documentation**: https://github.com/dotpcap/sharppcap
- **PacketDotNet Documentation**: https://github.com/dotpcap/packetnet
- **BPF Filter Syntax**: https://biot.com/capstats/bpf.html
- **TCP Protocol**: RFC 793
- **HTTP Protocol**: RFC 2616

---

*Tài liệu này giải thích đầy đủ về NetworkMonitor module. Nếu có câu hỏi thêm, vui lòng tham khảo source code hoặc examples trong thư mục `NetworkMonitor/Examples/`.*
