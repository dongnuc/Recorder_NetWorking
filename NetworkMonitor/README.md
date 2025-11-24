# NetworkMonitor

A C# library for capturing and analyzing network packets using SharpPcap.

## Features

- Capture TCP/UDP network packets
- Event-based packet notifications (for UI applications)
- Service-oriented packet retrieval (for non-UI services)
- **Structured network flow objects (TCP and HTTP)**
- Multiple output formats: summary, detailed, JSON
- Configurable port filtering
- HTTP protocol detection with detailed parsing
- **Server/Client role detection based on monitored ports**
- **TCP flags logging and capture** (including SYN, ACK, FIN, RST, PSH, URG, ECE, CWR)
- **HTTP request/response parsing with all fields**
- Optional automatic packet logging via LogMessage event

## Structured Network Flow Objects

The library now provides structured objects for network flows with all relevant fields:

### TcpNetworkFlow
- **Info**: Timestamp and flow description
- **Source**: Source IP and port
- **Destination**: Destination IP and port
- **Flags**: TCP flags (e.g., "SYN, ACK")
- **State**: Connection state (e.g., "ESTABLISHED", "SYN_SENT")
- **Data**: TCP payload data
- **SourceRole**: "Server" or "Client" (based on monitored port)
- **DestinationRole**: "Server" or "Client" (based on monitored port)

### HttpNetworkFlow
- **Info**: Timestamp and flow description
- **Source**: Source IP and port
- **Destination**: Destination IP and port
- **Flags**: TCP flags
- **State**: Connection state
- **URI**: HTTP request URI (for requests)
- **Host**: HTTP Host header value
- **Method**: HTTP method (GET, POST, PUT, DELETE, etc.)
- **Status**: HTTP status code and message (for responses)
- **HttpVersion**: HTTP version (e.g., "HTTP/1.1")
- **HttpHeaders**: All HTTP headers
- **HttpBody**: HTTP body content
- **SourceRole**: "Server" or "Client"
- **DestinationRole**: "Server" or "Client"

## Usage

### Structured Network Flow Output

```csharp
using NetworkMonitor.Services;
using NetworkMonitor.Abstractions;
using SharpPcap;

var service = new PacketCaptureService();

// Enable automatic logging of structured network flows
service.LogCapturedPackets = true;

service.LogMessage += (sender, args) =>
{
    // Will log complete structured objects as JSON
    Console.WriteLine(args.Message);
};

var devices = CaptureDeviceList.Instance;
if (devices.Count > 0)
{
    var device = devices[0];
    var cts = new CancellationTokenSource();
    
    // Start capturing on port 5000 (monitored server port)
    await service.StartCaptureAsync(device, "5000", null, cts.Token);
    
    // Wait for some flows to be captured
    await Task.Delay(5000);
    
    // Get all captured flows as structured JSON objects
    var networkFlows = service.GetCapturedNetworkFlowsAsJson();
    foreach (var flow in networkFlows)
    {
        Console.WriteLine(flow);
    }
    
    // Or get flows as strongly-typed objects
    var flowObjects = service.GetCapturedNetworkFlows();
    foreach (var flow in flowObjects)
    {
        if (flow is HttpNetworkFlow httpFlow)
        {
            Console.WriteLine($"HTTP {httpFlow.Method} {httpFlow.URI}");
            Console.WriteLine($"Host: {httpFlow.Host}");
            Console.WriteLine($"Status: {httpFlow.Status}");
        }
        else if (flow is TcpNetworkFlow tcpFlow)
        {
            Console.WriteLine($"TCP {tcpFlow.Flags} - State: {tcpFlow.State}");
        }
    }
    
    service.StopCapture();
}
```

### Example Output

For HTTP flows, the JSON output includes all fields:
```json
{
  "Info": "[2025-11-24 12:03:41] POST /books HTTP/1.1 ::1:52387 -> ::1:5000",
  "Source": "::1:52387",
  "Destination": "::1:5000",
  "Flags": "PSH, ACK",
  "State": "ESTABLISHED",
  "URI": "/books",
  "Host": "localhost:5000",
  "Method": "POST",
  "Status": null,
  "HttpVersion": "HTTP/1.1",
  "HttpHeaders": "Host: localhost:5000\nContent-Type: application/json; charset=utf-8\nContent-Length: 51",
  "HttpBody": "{\"BookId\":0,\"Title\":\"Book1\",\"PublicationYear\":2025}",
  "SourceRole": "Client",
  "DestinationRole": "Server"
}
```

For TCP flows:
```json
{
  "Info": "[2025-11-24 12:03:41] TCP ::1:52387 -> ::1:5000",
  "Source": "::1:52387",
  "Destination": "::1:5000",
  "Flags": "SYN",
  "State": "SYN_SENT",
  "Data": null,
  "SourceRole": "Client",
  "DestinationRole": "Server"
}
```

## Usage

### For UI Applications (Event-Based Approach)

```csharp
using NetworkMonitor.Services;
using NetworkMonitor.Abstractions;
using SharpPcap;

var service = new PacketCaptureService();

// Enable automatic logging of captured packets with TCP flags
service.LogCapturedPackets = true;

// Subscribe to events
service.PacketCaptured += (sender, args) =>
{
    Console.WriteLine($"Captured: {args.ProtocolLabel} {args.SourceIp}:{args.SourcePort} -> {args.DestinationIp}:{args.DestinationPort}");
};

service.LogMessage += (sender, args) =>
{
    Console.WriteLine($"[{(args.IsError ? "ERROR" : "INFO")}] {args.Message}");
};

// Get available devices
var devices = CaptureDeviceList.Instance;
if (devices.Count > 0)
{
    var device = devices[0];
    var cts = new CancellationTokenSource();
    
    // Start capturing
    await service.StartCaptureAsync(device, "all", null, cts.Token);
}
```

### For Services (Query-Based Approach)

```csharp
using NetworkMonitor.Services;
using NetworkMonitor.Abstractions;
using SharpPcap;

var service = new PacketCaptureService();

// Enable automatic logging to see TCP flags in logs
service.LogCapturedPackets = true;

service.LogMessage += (sender, args) =>
{
    Console.WriteLine($"[{(args.IsError ? "ERROR" : "INFO")}] {args.Message}");
};

var devices = CaptureDeviceList.Instance;

if (devices.Count > 0)
{
    var device = devices[0];
    var cts = new CancellationTokenSource();
    
    // Start capturing in background
    _ = service.StartCaptureAsync(device, "common", null, cts.Token);
    
    // Wait for some packets to be captured
    await Task.Delay(5000);
    
    // Retrieve captured packets as strings (includes TCP flags)
    var packets = service.GetCapturedPacketsAsStrings("summary");
    foreach (var packet in packets)
    {
        Console.WriteLine(packet);
    }
    
    // Get recent packets in detailed format
    var recentPackets = service.GetRecentPacketsAsStrings(10, "detailed");
    foreach (var packet in recentPackets)
    {
        Console.WriteLine(packet);
        Console.WriteLine("---");
    }
    
    // Get packets in JSON format
    var jsonPackets = service.GetCapturedPacketsAsStrings("json");
    
    // Check packet count
    Console.WriteLine($"Total packets captured: {service.GetCapturedPacketCount()}");
    
    // Clear buffer
    service.ClearCapturedPackets();
    
    // Stop capturing
    service.StopCapture();
}
```

## TCP Flags Logging

TCP flags are automatically captured and included in all packet formats:

```csharp
var service = new PacketCaptureService();

// Enable logging to see TCP flags in real-time via LogMessage event
service.LogCapturedPackets = true;

service.LogMessage += (sender, args) =>
{
    // Will log packets like: "[Packet Captured] TCP [SYN, ACK]: 192.168.1.100:54321 -> 192.168.1.1:443"
    Console.WriteLine(args.Message);
};
```

Supported TCP flags:
- **FIN** - Finish
- **SYN** - Synchronize
- **RST** - Reset
- **PSH** - Push
- **ACK** - Acknowledgment
- **URG** - Urgent
- **ECE** - ECN-Echo (bit 8, RFC 3168)
- **CWR** - Congestion Window Reduced (bit 9, RFC 3168)

## Output Formats

### Summary Format
Single-line summary of each packet:
```
HTTP: 192.168.1.100:54321 -> 192.168.1.1:80 (has payload)
TCP [SYN]: 192.168.1.100:54322 -> 192.168.1.1:443
```

### Detailed Format
Multi-line detailed information:
```
Protocol: HTTP
Source: 192.168.1.100:54321
Destination: 192.168.1.1:80
TCP Flags: PSH, ACK
Payload: GET /index.html HTTP/1.1...
```

### JSON Format
JSON-formatted packet data:
```json
{
  "protocol": "HTTP",
  "source": "192.168.1.100:54321",
  "destination": "192.168.1.1:80",
  "tcpFlags": "PSH, ACK",
  "payload": "GET /index.html HTTP/1.1..."
}
```

## Port Filtering Modes

- **all** - Monitor all TCP/UDP ports
- **common** - Monitor common ports (80, 443, 8000, 8080, 8888)
- **targeted** - Monitor targeted ports (5000, 8080)
- **custom** - Monitor custom ports (e.g., "8080,9000,3000")

## API Reference

### IPacketCaptureService Methods

#### StartCaptureAsync
```csharp
Task StartCaptureAsync(
    ICaptureDevice device, 
    string portsMode, 
    string? customPorts, 
    CancellationToken cancellationToken)
```
Starts capturing packets on the specified device.

#### StopCapture
```csharp
void StopCapture()
```
Stops packet capture.

#### GetCapturedPacketsAsStrings
```csharp
List<string> GetCapturedPacketsAsStrings(string format = "summary")
```
Gets all captured packets as formatted strings. Format options: "summary", "detailed", "json".

#### GetRecentPacketsAsStrings
```csharp
List<string> GetRecentPacketsAsStrings(int count, string format = "summary")
```
Gets the most recent N packets as formatted strings.

#### ClearCapturedPackets
```csharp
void ClearCapturedPackets()
```
Clears all stored captured packets.

#### GetCapturedPacketCount
```csharp
int GetCapturedPacketCount()
```
Gets the count of captured packets currently stored.

#### GetCapturedNetworkFlows (New)
```csharp
List<object> GetCapturedNetworkFlows()
```
Gets all captured packets as structured network flow objects (TcpNetworkFlow or HttpNetworkFlow).

#### GetRecentNetworkFlows (New)
```csharp
List<object> GetRecentNetworkFlows(int count)
```
Gets the most recent N packets as structured network flow objects.

#### GetCapturedNetworkFlowsAsJson (New)
```csharp
List<string> GetCapturedNetworkFlowsAsJson()
```
Gets all captured packets as JSON strings representing complete structured network flows.

#### GetRecentNetworkFlowsAsJson (New)
```csharp
List<string> GetRecentNetworkFlowsAsJson(int count)
```
Gets the most recent N packets as JSON strings representing structured network flows.

### Events

#### PacketCaptured
```csharp
event EventHandler<PacketCapturedEventArgs>? PacketCaptured
```
Raised when a packet is captured (event-based approach).

#### LogMessage
```csharp
event EventHandler<LogMessageEventArgs>? LogMessage
```
Raised when a log message needs to be written.

## Notes

- The service stores up to 1000 packets in memory to prevent memory issues
- When the buffer is full, the oldest packets are automatically removed
- Thread-safe for concurrent access
- Both event-based and query-based approaches can be used simultaneously
