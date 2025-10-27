// ProcessManagement/Services/ProcessManager.cs
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Helper.Kernel32API;
using Common.Helper.Kernel32API.Implement;
using Common.Helper.Kernel32API.Interface;
using Common.Logging;

namespace ProcessManagement.Services
{
    /// <summary>
    /// Manages external process execution and captures console I/O
    /// Raises events for console output and user input (via Enter key detection)
    /// Author: dongnuc
    /// Date: 2025-10-26
    /// </summary>
    public class ProcessManager
    {
        #region Fields

        private readonly IProcessStarter _processStarter;
        private readonly IConsolePoller _consolePoller;
        private readonly IKeyListener _keyListener;
        private readonly IMutexManager _mutexManager;
        private readonly IConsoleManager _consoleManager;
        private readonly IProcessWaiter _processWaiter;

        // Track active input monitoring tasks
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _monitoringTasks = new();

        // Track active polling tasks
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _pollingTasks = new();

        // Buffer recent console lines for input capture
        private readonly ConcurrentDictionary<string, Queue<string>> _recentOutputBuffers = new();
        private const int BUFFER_SIZE = 5;

        #endregion

        #region Events

        /// <summary>
        /// Event raised when client console outputs text
        /// </summary>
        public event Action<string> OnClientOutput;

        /// <summary>
        /// Event raised when server console outputs text
        /// </summary>
        public event Action<string> OnServerOutput;

        /// <summary>
        /// Event raised when user inputs text (detected by Enter key press)
        /// Parameters: (input, dataType)
        /// </summary>
        public event Action<string, string> OnUserInput;

        #endregion

        #region Constructor

        public ProcessManager()
        {
            _processStarter = new ProcessStarter();
            _consolePoller = new ConsolePoller();
            _keyListener = new KeyListener();
            _mutexManager = new MutexManager();
            _consoleManager = new ConsoleManager();
            _processWaiter = new ProcessWaiter();

            LogManager.Instance.LogDebug("ProcessManager initialized");
        }

        #endregion

        #region Start Single Process with Continuous Polling

        /// <summary>
        /// Start single process with continuous console polling and Enter key monitoring
        /// Returns control immediately for manual lifecycle management
        /// </summary>
        /// <param name="exePath">Path to executable</param>
        /// <param name="name">Process name (e.g., "Client" or "Server")</param>
        /// <param name="isClient">True if client, false if server</param>
        /// <param name="showConsoleMessages">Show debug console messages</param>
        /// <returns>Tuple of (ChildProcess, Mutex, CancellationTokenSource)</returns>
        public (ChildProcess child, IntPtr mutex, CancellationTokenSource cts) StartSingleWithPolling(
            string exePath,
            string name,
            bool isClient = true,
            bool showConsoleMessages = false)
        {
            if (!File.Exists(exePath))
            {
                LogManager.Instance.LogError($"Executable not found: {exePath}");
                throw new FileNotFoundException($"Executable not found: {exePath}");
            }

            LogManager.Instance.LogInfomation($"🚀 Starting {name} process: {Path.GetFileName(exePath)}");

            // Start process
            var (child, mutex) = StartSingle(exePath, name, showConsoleMessages);

            // Initialize output buffer for this process
            _recentOutputBuffers[name] = new Queue<string>(BUFFER_SIZE);

            // Create cancellation token for this process
            var cts = new CancellationTokenSource();

            // ✅ Start continuous console polling
            StartContinuousPolling(child, mutex, name, isClient, cts.Token);

            // Start input monitoring for client process only
            if (isClient)
            {
                StartInputMonitoring(child, name);
            }

            LogManager.Instance.LogInfomation($"✅ {name} process started with PID: {child.processId}");

            return (child, mutex, cts);
        }

        /// <summary>
        /// Continuously poll console output until cancelled
        /// </summary>
        private void StartContinuousPolling(
            ChildProcess child,
            IntPtr mutex,
            string processName,
            bool isClient,
            CancellationToken cancellationToken)
        {
            Task.Run(async () =>
            {
                LogManager.Instance.LogDebug($"📡 Started continuous polling for {processName}");

                int emptyPollCount = 0;
                const int MAX_EMPTY_POLLS = 100; // Stop after 100 consecutive empty polls

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Check if process is still alive
                        if (child.hProcess == IntPtr.Zero)
                        {
                            LogManager.Instance.LogDebug($"Process {processName} terminated, stopping polling");
                            break;
                        }

                        // ✅ FIX: Use PollOnceAsync instead of PollAsync
                        string newOutput = await _consolePoller.PollOnceAsync(child, mutex);

                        if (!string.IsNullOrWhiteSpace(newOutput))
                        {
                            emptyPollCount = 0; // Reset counter
                            ProcessLogOutput(newOutput, isClient, processName);
                        }
                        else
                        {
                            emptyPollCount++;

                            // If too many empty polls, process might have terminated
                            if (emptyPollCount >= MAX_EMPTY_POLLS)
                            {
                                LogManager.Instance.LogDebug($"Process {processName} appears inactive after {MAX_EMPTY_POLLS} empty polls");
                                break;
                            }
                        }

                        // ✅ Poll every 200ms (adjust as needed)
                        await Task.Delay(200, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        LogManager.Instance.LogDebug($"Polling cancelled for {processName}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogManager.Instance.LogError($"Error polling {processName}: {ex.Message}");
                        await Task.Delay(1000, cancellationToken); // Wait before retry
                    }
                }

                LogManager.Instance.LogDebug($"📡 Stopped continuous polling for {processName}");
            }, cancellationToken);
        }

        /// <summary>
        /// Extract new output by comparing with previous output
        /// </summary>
        private string GetNewOutput(string previousOutput, string currentOutput)
        {
            if (string.IsNullOrWhiteSpace(previousOutput))
                return currentOutput;

            if (currentOutput.Length <= previousOutput.Length)
                return string.Empty;

            // Get the new part (everything after previous output)
            return currentOutput.Substring(previousOutput.Length);
        }

        /// <summary>
        /// Stop single process and cleanup monitoring
        /// </summary>
        /// <param name="child">Child process handle</param>
        /// <param name="mutex">Mutex handle</param>
        /// <param name="cts">Cancellation token source</param>
        /// <param name="name">Process name</param>
        /// <param name="showConsoleMessages">Show debug messages</param>
        public async Task StopSingleAsync(
            ChildProcess child,
            IntPtr mutex,
            CancellationTokenSource cts,
            string name,
            bool showConsoleMessages = false)
        {
            LogManager.Instance.LogInfomation($"⏹️ Stopping {name} process...");

            try
            {
                // ✅ Cancel continuous polling
                cts?.Cancel();

                // Stop input monitoring
                StopInputMonitoring(name);

                // Remove output buffer
                _recentOutputBuffers.TryRemove(name, out _);

                // Close process
                CloseSingle(child, mutex, showConsoleMessages);

                // Dispose cancellation token
                cts?.Dispose();

                LogManager.Instance.LogInfomation($"✅ {name} process stopped");
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Error stopping {name}: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        #endregion

        #region Input Monitoring (Enter Key Detection)

        /// <summary>
        /// Monitor Enter key press in console to detect user input
        /// Triggers OnUserInput event when Enter is pressed
        /// </summary>
        /// <param name="child">Child process to monitor</param>
        /// <param name="processName">Process name for logging</param>
        private void StartInputMonitoring(ChildProcess child, string processName)
        {
            // Cancel existing monitoring if any
            StopInputMonitoring(processName);

            var cts = new CancellationTokenSource();
            _monitoringTasks[processName] = cts;

            Task.Run(async () =>
            {
                LogManager.Instance.LogDebug($"🔍 Started Enter key monitoring for {processName}");

                bool lastEnterState = false;
                DateTime lastTriggerTime = DateTime.MinValue;
                const int DEBOUNCE_MS = 300; // Debounce time to avoid multiple triggers

                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        // Check if process is still alive
                        if (child.hProcess == IntPtr.Zero)
                        {
                            LogManager.Instance.LogDebug($"Process {processName} handle is null, stopping monitor");
                            break;
                        }

                        // Check Enter key state (VK_RETURN = 0x0D)
                        bool currentEnterState = _keyListener.IsKeyPressed(0x0D);

                        // Detect key press (transition from not pressed to pressed)
                        if (currentEnterState && !lastEnterState)
                        {
                            // Debounce: Prevent multiple triggers
                            var timeSinceLastTrigger = DateTime.Now - lastTriggerTime;
                            if (timeSinceLastTrigger.TotalMilliseconds > DEBOUNCE_MS)
                            {
                                // ✅ V3.0: CAPTURE INPUT BEFORE DELAY
                                // Lấy snapshot của buffer TRƯỚC KHI console cập nhật
                                string capturedInput = GetRecentInput(processName);
                                
                                // Wait for key to be released and console buffer to update
                                await Task.Delay(150, cts.Token);

                                LogManager.Instance.LogInfomation($"⌨️ Enter key detected in {processName} - Input: {capturedInput}");

                                // Raise input event
                                OnUserInput?.Invoke(capturedInput, "UserInput");

                                lastTriggerTime = DateTime.Now;
                            }
                        }

                        lastEnterState = currentEnterState;

                        // Check every 50ms
                        await Task.Delay(50, cts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogManager.Instance.LogError($"Error in input monitoring for {processName}: {ex.Message}");
                        await Task.Delay(1000, cts.Token); // Wait before retry
                    }
                }

                LogManager.Instance.LogDebug($"🔍 Stopped Enter key monitoring for {processName}");
            }, cts.Token);
        }

        /// <summary>
        /// Stop input monitoring for specific process
        /// </summary>
        /// <param name="processName">Process name</param>
        private void StopInputMonitoring(string processName)
        {
            if (_monitoringTasks.TryRemove(processName, out var cts))
            {
                try
                {
                    cts?.Cancel();
                    cts?.Dispose();
                    LogManager.Instance.LogDebug($"🛑 Input monitoring stopped for {processName}");
                }
                catch (Exception ex)
                {
                    LogManager.Instance.LogError($"Error stopping input monitoring: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// ✅ V3.0 IMPROVED: Get recent input from output buffer
        /// Lọc ra line cuối cùng KHÔNG phải system message
        /// </summary>
        /// <param name="processName">Process name</param>
        /// <returns>Recent input text or timestamp placeholder</returns>
        private string GetRecentInput(string processName)
        {
            if (_recentOutputBuffers.TryGetValue(processName, out var buffer))
            {
                // ✅ Duyệt buffer từ cuối lên để tìm line KHÔNG phải system message
                var lines = buffer.Reverse().ToList();
                
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var trimmedLine = line.Trim();
                        
                        // ✅ Filter out system messages
                        if (!IsSystemMessage(trimmedLine))
                        {
                            LogManager.Instance.LogDebug($"🔍 GetRecentInput found: {trimmedLine}");
                            return trimmedLine;
                        }
                        else
                        {
                            LogManager.Instance.LogDebug($"🔍 GetRecentInput skipped system message: {trimmedLine}");
                        }
                    }
                }
                
                LogManager.Instance.LogDebug($"🔍 GetRecentInput: No valid input found in buffer");
            }
            else
            {
                LogManager.Instance.LogDebug($"🔍 GetRecentInput: Buffer not found for {processName}");
            }

            // Fallback: Return timestamp
            return $"[Input at {DateTime.Now:HH:mm:ss}]";
        }

        /// <summary>
        /// ✅ V3.0 IMPROVED: Check if line is a system message (not user input)
        /// Thêm nhiều keywords để filter chính xác hơn
        /// </summary>
        private bool IsSystemMessage(string line)
        {
            var lowerLine = line.ToLower();

            string[] systemKeywords = {
                "server", "client", "listening", "connected", "starting",
                "loading", "waiting", "initializing", "ready", "status",
                "error", "warning", "info", "debug", "received", "sent",
                "processing", "response", "request", "sending", "receiving",
                "established", "closed", "opened", "failed", "success",
                "connecting", "disconnecting", "message from", "reply from"
            };

            // ✅ Check if line contains ANY system keyword
            bool isSystem = systemKeywords.Any(keyword => lowerLine.Contains(keyword));
            
            // ✅ Additional check: Lines starting with timestamps or log levels
            if (lowerLine.StartsWith("[") || lowerLine.StartsWith("->") || lowerLine.StartsWith("<-"))
            {
                isSystem = true;
            }

            return isSystem;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Start single process (internal use)
        /// </summary>
        public (ChildProcess child, IntPtr mutex) StartSingle(string exePath, string name, bool showConsoleMessages = false)
        {
            if (!File.Exists(exePath))
            {
                if (showConsoleMessages)
                {
                    _consoleManager.Alloc();
                    Console.WriteLine("Đường dẫn EXE không hợp lệ.");
                    _consoleManager.Free();
                }
                throw new FileNotFoundException($"Executable not found: {exePath}");
            }

            IntPtr mutex = _mutexManager.Create($"Global\\ConsoleAttachMutex_{name}");
            ChildProcess child = _processStarter.Start(exePath, name);

            Thread.Sleep(2000); // Wait for console initialization

            if (showConsoleMessages)
            {
                _consoleManager.Alloc();
                Console.WriteLine($"Đã khởi động ứng dụng console {name} với cửa sổ riêng.");
                Console.WriteLine("Tương tác trực tiếp với nó trong cửa sổ console tương ứng.");
                _consoleManager.Free();
            }

            return (child, mutex);
        }

        /// <summary>
        /// Close single process (internal use)
        /// </summary>
        public void CloseSingle(ChildProcess child, IntPtr mutex, bool showConsoleMessages = false)
        {
            try
            {
                _consoleManager.SendCtrlC(child.processId, mutex, _mutexManager);
                _processWaiter.WaitForProcess(child.hProcess);
                _consoleManager.CloseHandles(child);
                _mutexManager.Close(mutex);

                if (showConsoleMessages)
                {
                    _consoleManager.Alloc();
                    Console.WriteLine($"Tiến trình {child.name} đã thoát.");
                    _consoleManager.Free();
                }
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Error closing process {child.name}: {ex.Message}");
            }
        }

        #endregion

        #region Log Processing

        /// <summary>
        /// Process console log output and raise appropriate events
        /// Stores recent lines in buffer for input detection
        /// </summary>
        private void ProcessLogOutput(string log, bool isClient, string processName)
        {
            if (string.IsNullOrWhiteSpace(log))
                return;

            LogManager.Instance.LogDebug($"🔍 ProcessLogOutput called - Process: {processName}, IsClient: {isClient}, LogLength: {log.Length}");

            var lines = log.Split(new[] { Environment.NewLine, "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

            LogManager.Instance.LogDebug($"🔍 Split into {lines.Length} lines");

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Add to recent output buffer
                if (_recentOutputBuffers.TryGetValue(processName, out var buffer))
                {
                    buffer.Enqueue(line);

                    // Keep buffer size limited
                    while (buffer.Count > BUFFER_SIZE)
                    {
                        buffer.Dequeue();
                    }
                }

                // Raise output event
                if (isClient)
                {
                    LogManager.Instance.LogDebug($"📤 Raising OnClientOutput: {line}");
                    OnClientOutput?.Invoke(line);
                }
                else
                {
                    LogManager.Instance.LogDebug($"📤 Raising OnServerOutput: {line}");
                    OnServerOutput?.Invoke(line);
                }
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Cleanup all monitoring tasks
        /// </summary>
        public void Dispose()
        {
            LogManager.Instance.LogDebug("ProcessManager disposing...");

            // Stop all monitoring tasks
            foreach (var kvp in _monitoringTasks.ToList())
            {
                StopInputMonitoring(kvp.Key);
            }

            // Stop all polling tasks
            foreach (var kvp in _pollingTasks.ToList())
            {
                kvp.Value?.Cancel();
                kvp.Value?.Dispose();
            }

            // Clear buffers
            _recentOutputBuffers.Clear();
            _pollingTasks.Clear();

            LogManager.Instance.LogDebug("ProcessManager disposed");
        }

        #endregion
    }
}