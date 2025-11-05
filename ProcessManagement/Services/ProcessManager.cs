using Common.Helper;
using Common.Helper.Kernel32API;
using Common.Helper.Kernel32API.Implement;
using Common.Helper.Kernel32API.Interface;
using Common.Interfaces.Services;
using Common.Logging;
using System.Collections.Concurrent;

namespace ProcessManagement.Services
{

    public class ProcessManager : IProcessManager
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

        // ✅ Store process handles for capture
        private readonly ConcurrentDictionary<string, (ChildProcess child, IntPtr mutex)> _processHandles = new();

        // ✅ Store previous console snapshots (for difference detection)
        private readonly ConcurrentDictionary<string, string> _previousSnapshots = new();

        #endregion

        #region Events
        public event Action<string> OnClientOutput;
        public event Action<string> OnServerOutput;
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

        #region Start Single Process (New Logic)
        /// <summary>
        /// ✅ V4.0: Start process và capture initial output
        /// </summary>
        /// <param name="exePath">Path to executable</param>
        /// <param name="name">Process name (e.g., "Client" or "Server")</param>
        /// <param name="isClient">True if client, false if server</param>
        /// <param name="showConsoleMessages">Show debug console messages</param>
        /// <returns>Tuple of (ChildProcess, Mutex, CancellationTokenSource)</returns>
        public async Task<(ChildProcess child, IntPtr mutex, CancellationTokenSource cts)> StartSingleWithPollingAsync(
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

            //  Start process
            var (child, mutex) = StartSingle(exePath, name, showConsoleMessages);

            // Store process handles
            _processHandles[name] = (child, mutex);

            await Task.Delay(2000);

            //  Capture INITIAL output (lần đầu tiên)
            await CaptureAndRaiseInitialOutputAsync(child, mutex, name, isClient);

            // Start Enter key monitoring (chỉ cho client)
            var cts = new CancellationTokenSource();
            if (isClient)
            {
                StartInputMonitoring(child, mutex, name, cts.Token);
            }

            LogManager.Instance.LogInfomation($"✅ {name} process started with PID: {child.processId}");

            return (child, mutex, cts);
        }

        /// <summary>
        /// ✅ Capture initial output khi start process (lần đầu tiên)
        /// </summary>
        private async Task CaptureAndRaiseInitialOutputAsync(
            ChildProcess child,
            IntPtr mutex,
            string processName,
            bool isClient)
        {
            try
            {
                LogManager.Instance.LogInfomation($"📸 Capturing initial output for {processName}");

                string initialOutput = await _consolePoller.CaptureCurrentConsoleAsync(
                    child,
                    mutex,
                    expandBuffer: true  // Expand buffer lần đầu
                );

                if (!string.IsNullOrWhiteSpace(initialOutput))
                {
                    _previousSnapshots[processName] = initialOutput;

                    LogManager.Instance.LogInfomation($"✅ Initial output captured ({initialOutput.Length} chars)");
                    LogManager.Instance.LogDebug($"📝 Initial content:\n{initialOutput}");

                    if (isClient)
                    {
                        OnClientOutput?.Invoke(initialOutput);
                    }
                    else
                    {
                        OnServerOutput?.Invoke(initialOutput);
                    }
                }
                else
                {
                    LogManager.Instance.LogWarning($"⚠️ No initial output for {processName}");
                    _previousSnapshots[processName] = string.Empty;
                }
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"❌ Error capturing initial output for {processName}: {ex.Message}");
                _previousSnapshots[processName] = string.Empty;
            }
        }

        /// <summary>
        /// Stop single process and cleanup monitoring
        /// </summary>
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
                // Cancel monitoring
                cts?.Cancel();
                StopInputMonitoring(name);

                // Cleanup
                _processHandles.TryRemove(name, out _);
                _previousSnapshots.TryRemove(name, out _);

                // Close process
                CloseSingle(child, mutex, showConsoleMessages);

                // Dispose
                cts?.Dispose();

                LogManager.Instance.LogInfomation($" {name} process stopped");
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Error stopping {name}: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        #endregion

        #region Input Monitoring (Enter Key Detection) - New Logic

        /// <summary>
        /// ✅ V4.0: Monitor Enter key và trigger capture sequence
        /// </summary>
        private void StartInputMonitoring(
            ChildProcess child,
            IntPtr mutex,
            string processName,
            CancellationToken cancellationToken)
        {
            // Cancel existing monitoring if any
            StopInputMonitoring(processName);

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _monitoringTasks[processName] = cts;

            Task.Run(async () =>
            {
                LogManager.Instance.LogDebug($"🔍 Started Enter key monitoring for {processName}");

                bool lastEnterState = false;
                DateTime lastTriggerTime = DateTime.MinValue;
                const int DEBOUNCE_MS = 300;

                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (child.hProcess == IntPtr.Zero)
                        {
                            LogManager.Instance.LogDebug($"Process {processName} handle is null, stopping monitor");
                            break;
                        }

                        // Check Enter key state 0x0D => enter | 0x7B => F12
                        bool currentEnterState = _keyListener.IsKeyPressed(Constants.VK_F12);

                        if (currentEnterState && !lastEnterState)
                        {
                            var timeSinceLastTrigger = DateTime.Now - lastTriggerTime;
                            if (timeSinceLastTrigger.TotalMilliseconds > DEBOUNCE_MS)
                            {
                                LogManager.Instance.LogInfomation($" ===== ENTER KEY PRESSED in {processName} =====");

                                // Execute capture sequence
                                await ExecuteCaptureSequenceAsync(child, mutex, processName);

                                lastTriggerTime = DateTime.Now;
                            }
                        }

                        lastEnterState = currentEnterState;
                        await Task.Delay(10, cts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogManager.Instance.LogError($"Error in input monitoring for {processName}: {ex.Message}");
                        await Task.Delay(1000, cts.Token);
                    }
                }

                LogManager.Instance.LogDebug($"🔍 Stopped Enter key monitoring for {processName}");
            }, cts.Token);
        }

        /// <summary>
        /// ✅ FIXED: Execute capture sequence với intelligent polling
        /// </summary>
        /// ✅ V4.1 FIXED: Execute capture sequence với adaptive polling và longer timeout
        /// </summary>
        private async Task ExecuteCaptureSequenceAsync(
            ChildProcess child,
            IntPtr mutex,
            string processName)
        {
            try
            {
                //Capture BEFORE
                LogManager.Instance.LogDebug($" 1: Capturing BEFORE snapshot");
                string bufferBefore = _previousSnapshots[processName];


                // Capture AFTER 
                LogManager.Instance.LogDebug($" 3: Capturing AFTER snapshot (with input)");
                string bufferAfterInput = await _consolePoller.CaptureCurrentConsoleAsync(
                    child,
                    mutex,
                    expandBuffer: false
                );

                // Extract input value
                LogManager.Instance.LogDebug($"4: Extracting input value");
                string extractedCapture = DataInspector.ExtractDifference(
                    bufferBefore,
                    bufferAfterInput
                );
                LogManager.Instance.LogInfomation($" Extracted input: [{extractedCapture}]");
                // có thể thay equals => contain đối với bài nhiều client
                bool isClient = processName.Equals("Client", StringComparison.OrdinalIgnoreCase);

                if (isClient)
                {
                    var (input, outputClient) = DataInspector.SplitInputFromOutput(extractedCapture);
                    if (input != null && !string.IsNullOrWhiteSpace(input))
                    {
                        OnUserInput?.Invoke(input, "UserInput");
                    }
                    else
                    {
                        OnUserInput?.Invoke($"[Input at {DateTime.Now:HH:mm:ss}]", "UserInput");
                        LogManager.Instance.LogWarning($" Could not extract input");
                    }
                    LogManager.Instance.LogDebug($" STEP 5: Raising OnUserInput event");
                    OnClientOutput?.Invoke(outputClient);
                }
                else
                {
                    LogManager.Instance.LogDebug($"Processing SERVER capture");
                    if (!string.IsNullOrWhiteSpace(extractedCapture))
                    {
                        var outputSplit = extractedCapture.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
                        var outputLines = outputSplit.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l));
                        string outputResult = "";
                        outputResult = string.Join(Environment.NewLine, outputLines);
                        OnServerOutput?.Invoke(outputResult);
                    }
                }

                //  Update previous snapshot IMMEDIATELY after extracting input
                _previousSnapshots[processName] = bufferAfterInput;
                LogManager.Instance.LogDebug($" Updated previous snapshot for {processName} (length: {bufferAfterInput.Length})");

                if (isClient)
                {
                    if (_processHandles.TryGetValue("Server", out var serverHandles))
                    {
                        var (serverChild, serverMutex) = serverHandles;
                        try
                        {
                            await ExecuteCaptureSequenceAsync(serverChild, serverMutex, "Server");
                        }
                        catch (Exception ex)
                        {
                            LogManager.Instance.LogError($" Failed to auto-capture Server: {ex.Message}");
                        }
                    }
                }
                else
                {
                    LogManager.Instance.LogWarning($" Server process not found for auto-capture");
                }
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($" Error in capture sequence: {ex.Message}");
                LogManager.Instance.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Stop input monitoring for specific process
        /// </summary>
        private void StopInputMonitoring(string processName)
        {
            if (_monitoringTasks.TryRemove(processName, out var cts))
            {
                try
                {
                    cts?.Cancel();
                    cts?.Dispose();
                    LogManager.Instance.LogDebug($" Input monitoring stopped for {processName}");
                }
                catch (Exception ex)
                {
                    LogManager.Instance.LogError($"Error stopping input monitoring: {ex.Message}");
                }
            }
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

            // Clear dictionaries
            _processHandles.Clear();
            _previousSnapshots.Clear();

            LogManager.Instance.LogDebug("ProcessManager disposed");
        }

        public void CloseClient()
        {
            throw new NotImplementedException();
        }

        public void CloseServer()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}


