# NetworkMonitor

A C# library for capturing and analyzing network packets using SharpPcap.

## Features

- Capture TCP/UDP network packets
- Event-based packet notifications (for UI applications)
- Service-oriented packet retrieval (for non-UI services)
- Multiple output formats: summary, detailed, JSON
- Configurable port filtering
- HTTP protocol detection

## Usage

### For UI Applications (Event-Based Approach)

```csharp
using NetworkMonitor.Services;
using NetworkMonitor.Abstractions;
using SharpPcap;

var service = new PacketCaptureService();

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
var devices = CaptureDeviceList.Instance;

if (devices.Count > 0)
{
    var device = devices[0];
    var cts = new CancellationTokenSource();
    
    // Start capturing in background
    _ = service.StartCaptureAsync(device, "common", null, cts.Token);
    
    // Wait for some packets to be captured
    await Task.Delay(5000);
    
    // Retrieve captured packets as strings
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
