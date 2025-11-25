using NetworkMonitor.Services;
using NetworkMonitor.Abstractions;
using PacketDotNet;

namespace NetworkMonitor.Examples
{
    /// <summary>
    /// Example usage of NetworkMonitor for services (non-UI contexts).
    /// </summary>
    public class ServiceExample
    {
        /// <summary>
        /// Demonstrates how to use PacketCaptureService in a service context
        /// to retrieve network data as formatted strings with TCP flags.
        /// </summary>
        //public static async Task RunExample()
        //{
        //    var service = new PacketCaptureService();
            
        //    // Enable automatic logging of captured packets (includes TCP flags)
        //    service.LogCapturedPackets = true;
            
        //    // Subscribe to log messages to see TCP flags in real-time
        //    service.LogMessage += (sender, args) =>
        //    {
        //        Console.WriteLine($"[{(args.IsError ? "ERROR" : "INFO")}] {args.Message}");
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
        //        // Start capturing packets in the background
        //        // Monitor only common ports: 80, 443, 8000, 8080, 8888
        //        var captureTask = service.StartCaptureAsync(device, "common", null, cts.Token);
                
        //        Console.WriteLine("Capturing packets for 10 seconds...");
        //        Console.WriteLine("TCP flags will be logged in real-time...\n");
        //        await Task.Delay(10000);

        //        // Retrieve all captured packets as summary strings
        //        Console.WriteLine("\n=== All Captured Packets (Summary) ===");
        //        var allPackets = service.GetCapturedPacketsAsStrings("summary");
        //        Console.WriteLine($"Total packets captured: {allPackets.Count}");
        //        foreach (var packet in allPackets.Take(5))
        //        {
        //            Console.WriteLine(packet);
        //        }
        //        if (allPackets.Count > 5)
        //        {
        //            Console.WriteLine($"... and {allPackets.Count - 5} more packets");
        //        }

        //        // Retrieve recent packets in detailed format
        //        Console.WriteLine("\n=== Recent Packets (Detailed) ===");
        //        var recentPackets = service.GetRecentPacketsAsStrings(3, "detailed");
        //        foreach (var packet in recentPackets)
        //        {
        //            Console.WriteLine(packet);
        //            Console.WriteLine("---");
        //        }

        //        // Retrieve packets in JSON format
        //        Console.WriteLine("\n=== Sample Packet (JSON) ===");
        //        var jsonPackets = service.GetRecentPacketsAsStrings(1, "json");
        //        if (jsonPackets.Count > 0)
        //        {
        //            Console.WriteLine(jsonPackets[0]);
        //        }

        //        // Get packet statistics
        //        Console.WriteLine($"\n=== Statistics ===");
        //        Console.WriteLine($"Total packets in buffer: {service.GetCapturedPacketCount()}");

        //        // Clear the buffer
        //        Console.WriteLine("\nClearing packet buffer...");
        //        service.ClearCapturedPackets();
        //        Console.WriteLine($"Packets after clear: {service.GetCapturedPacketCount()}");

        //        // Stop capturing
        //        Console.WriteLine("\nStopping capture...");
        //        cts.Cancel();
        //        await Task.Delay(1000); // Give time for cleanup
        //        service.StopCapture();
        //        Console.WriteLine("Capture stopped.");
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
        ///// Example of continuous monitoring with periodic retrieval.
        ///// </summary>
        //public static async Task RunContinuousMonitoringExample()
        //{
        //    var service = new PacketCaptureService();
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
        //        // Start capturing
        //        _ = service.StartCaptureAsync(device, "all", null, cts.Token);
                
        //        Console.WriteLine("Starting continuous monitoring (press any key to stop)...\n");

        //        // Monitor for 30 seconds or until key press
        //        var monitorTask = Task.Run(() =>
        //        {
        //            while (!cts.Token.IsCancellationRequested)
        //            {
        //                Thread.Sleep(5000); // Check every 5 seconds
                        
        //                var count = service.GetCapturedPacketCount();
        //                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Packets in buffer: {count}");
                        
        //                // Get and display recent packets
        //                var recent = service.GetRecentPacketsAsStrings(5, "summary");
        //                foreach (var packet in recent)
        //                {
        //                    Console.WriteLine($"  {packet}");
        //                }
                        
        //                // Clear old packets if buffer is getting full
        //                if (count > 800)
        //                {
        //                    Console.WriteLine("  Buffer near capacity, clearing old packets...");
        //                    service.ClearCapturedPackets();
        //                }
        //            }
        //        });

        //        // Wait for user to press a key or timeout after 30 seconds
        //        var waitTask = Task.Run(() => Console.ReadKey(true));
        //        await Task.WhenAny(monitorTask, waitTask, Task.Delay(30000));
                
        //        cts.Cancel();
        //        await Task.Delay(1000);
        //        service.StopCapture();
        //        Console.WriteLine("\nMonitoring stopped.");
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
