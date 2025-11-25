using NetworkMonitor.Services;
using NetworkMonitor.Abstractions;
using PacketDotNet;
using Common.Interfaces.Services;

namespace NetworkMonitor.Examples
{
    /// <summary>
    /// Example usage of NetworkMonitor with structured network flow output.
    /// </summary>
    public class NetworkFlowExample
    {
        /// <summary>
        /// Demonstrates how to use PacketCaptureService to capture and output
        /// structured network flow objects (TCP and HTTP).
        /// </summary>
        //public static async Task RunExample()
        //{
        //    var service = new PacketCaptureService();
            
        //    // Enable automatic logging of captured packets as structured objects
        //    service.LogCapturedPackets = true;
            
        //    // Subscribe to log messages to see structured network flows in real-time
        //    service.LogMessage += (sender, args) =>
        //    {
        //        Console.WriteLine($"[{(args.IsError ? "ERROR" : "INFO")}] {args.Message}");
        //        Console.WriteLine(); // Add spacing between logs
        //    };

        //    // Get available network devices
        //    var devices = SharpPcap.CaptureDeviceList.Instance;
            
        //    if (devices.Count == 0)
        //    {
        //        Console.WriteLine("No network devices found.");
        //        return;
        //    }

        //    Console.WriteLine($"Found {devices.Count} network device(s).");
        //    var device = devices[0];
        //    Console.WriteLine($"Using device: {device.Description}");

        //    // Create cancellation token for stopping capture
        //    var cts = new CancellationTokenSource();

        //    try
        //    {
        //        // Start capturing packets on port 5000 (assuming it's the monitored server port)
        //        // This will help determine server vs client roles
        //        var captureTask = service.StartCaptureAsync(device, "5000", null, cts.Token);
                
        //        Console.WriteLine("Capturing network flows for 10 seconds...");
        //        Console.WriteLine("Structured network flow objects will be logged in real-time...\n");
        //        await Task.Delay(10000);

        //        // Stop capturing
        //        cts.Cancel();
        //        await Task.Delay(1000);
        //        service.StopCapture();

        //        Console.WriteLine("\n=== Captured Network Flows (Structured Objects) ===");
        //        Console.WriteLine($"Total flows captured: {service.GetCapturedPacketCount()}\n");

        //        // Get all captured network flows as JSON strings
        //        var networkFlows = service.GetCapturedNetworkFlowsAsJson();
                
        //        // Display first 3 flows
        //        int displayCount = Math.Min(3, networkFlows.Count);
        //        for (int i = 0; i < displayCount; i++)
        //        {
        //            Console.WriteLine($"Flow {i + 1}:");
        //            Console.WriteLine(networkFlows[i]);
        //            Console.WriteLine();
        //        }

        //        if (networkFlows.Count > displayCount)
        //        {
        //            Console.WriteLine($"... and {networkFlows.Count - displayCount} more flows");
        //        }

        //        // Get recent flows as objects
        //        Console.WriteLine("\n=== Recent Network Flow Objects ===");
        //        var recentFlows = service.GetRecentNetworkFlows(2);
        //        foreach (var flow in recentFlows)
        //        {
        //            Console.WriteLine($"Flow type: {flow.GetType().Name}");
        //            Console.WriteLine(NetworkFlowConverter.ToJson(flow));
        //            Console.WriteLine();
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error: {ex.Message}");
        //    }
        //    finally
        //    {
        //        cts.Dispose();
        //    }
        //}

        ///// <summary>
        ///// Example demonstrating HTTP-specific network flow capture.
        ///// </summary>
        //public static async Task RunHttpFlowExample()
        //{
        //    var service = new PacketCaptureService();
            
        //    // Enable logging to see HTTP flows with all fields
        //    service.LogCapturedPackets = true;
            
        //    service.LogMessage += (sender, args) =>
        //    {
        //        if (!args.IsError)
        //        {
        //            Console.WriteLine(args.Message);
        //            Console.WriteLine();
        //        }
        //    };

        //    var devices = SharpPcap.CaptureDeviceList.Instance;
            
        //    if (devices.Count == 0)
        //    {
        //        Console.WriteLine("No network devices found.");
        //        return;
        //    }

        //    var device = devices[0];
        //    var cts = new CancellationTokenSource();

        //    try
        //    {
        //        Console.WriteLine("Starting HTTP flow capture on port 5000...");
        //        Console.WriteLine("Make HTTP requests to see structured HTTP flow objects.\n");
                
        //        // Monitor port 5000 (typical for .NET applications)
        //        _ = service.StartCaptureAsync(device, "5000", null, cts.Token);
                
        //        Console.WriteLine("Monitoring for 30 seconds. Press any key to stop...\n");
                
        //        var waitTask = Task.Run(() => Console.ReadKey(true));
        //        await Task.WhenAny(waitTask, Task.Delay(30000));
                
        //        cts.Cancel();
        //        await Task.Delay(1000);
        //        service.StopCapture();

        //        Console.WriteLine("\n=== Captured HTTP Flows ===");
        //        var httpFlows = service.GetCapturedNetworkFlowsAsJson();
        //        Console.WriteLine($"Total flows captured: {httpFlows.Count}\n");

        //        // Display HTTP flows
        //        foreach (var flow in httpFlows.Take(5))
        //        {
        //            Console.WriteLine(flow);
        //            Console.WriteLine();
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error: {ex.Message}");
        //    }
        //    finally
        //    {
        //        cts.Dispose();
        //    }
        //}
    }
}
