using Common.Helper;
using Common.Helper.Kernel32API;
using Common.Helper.Kernel32API.Implement;
using Common.Helper.Kernel32API.Interface;
using Common.Interfaces.Services;
using Common.Logging;
using Common.Resources;
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
        private readonly ITestkitManagerService _testkitManagerService;

        private readonly ConcurrentDictionary<string, CancellationTokenSource> _monitoringTasks = new();

        private readonly ConcurrentDictionary<string, (ChildProcess child, IntPtr mutex)> _processHandles = new();

        private readonly ConcurrentDictionary<string, string> _previousSnapshots = new();

        #endregion

        #region Constructor

        public ProcessManager(ITestkitManagerService testkitManagerService)
        {
            _processStarter = new ProcessStarter();
            _consolePoller = new ConsolePoller();
            _keyListener = new KeyListener();
            _mutexManager = new MutexManager();
            _consoleManager = new ConsoleManager();
            _processWaiter = new ProcessWaiter();
            _testkitManagerService = testkitManagerService;
        }

        #endregion

        #region Start Single Process 
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

            await Task.Delay(3000);

            //  Capture INITIAL output (lần đầu tiên) 
            //await CaptureAndRaiseInitialOutputAsync(child, mutex, name, isClient);

            // Start Enter key monitoring (chỉ cho client)
            var cts = new CancellationTokenSource();
            if (isClient)
            {
                StartInputMonitoring(child, mutex, name, cts.Token);
            }

            return (child, mutex, cts);
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
            LogManager.Instance.LogInfomation($" Stopping {name} process...");

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
        ///  V4.0: Monitor Enter key và trigger capture sequence
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
                bool lastEnterState = false;
                bool lastF10State = false;
                DateTime lastTriggerTime = DateTime.MinValue;
                const int DEBOUNCE_MS = 300;

                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (child.hProcess == IntPtr.Zero)
                        {
                            break;
                        }
                        //EnableProcessExitMonitoring(child, processName, mutex);
                        // Check Enter key state 0x0D => enter | 0x7B => F12
                        bool currentEnterState = _keyListener.IsKeyPressed(Constants.VK_F12);

                        // Check F10 key for snapshot only to update current snapshot
                        bool currentF10State = _keyListener.IsKeyPressed(Constants.VK_F10);


                        if (currentEnterState && !lastEnterState)
                        {
                            var timeSinceLastTrigger = DateTime.Now - lastTriggerTime;
                            if (timeSinceLastTrigger.TotalMilliseconds > DEBOUNCE_MS)
                            {
                                // Execute capture sequence
                                await ExecuteCaptureSequenceAsync(child, mutex, processName);

                                lastTriggerTime = DateTime.Now;
                            }
                        }

                        //if (currentF10State && !lastF10State)
                        //{
                        //    var timeSinceLastTrigger = DateTime.Now - lastTriggerTime;
                        //    if (timeSinceLastTrigger.TotalMilliseconds > DEBOUNCE_MS)
                        //    {
                        //        LogManager.Instance.LogInfomation($"📸 ===== F10 PRESSED in {processName} - Snapshot Only =====");
                        //        await CaptureSnapshotOnlyAsync(child, mutex, processName);
                        //        lastTriggerTime = DateTime.Now;
                        //    }
                        //}


                        lastF10State = currentF10State;
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

            }, cts.Token);
        }

        /// <summary>
        /// Execute capture sequence với adaptive polling và longer timeout
        /// </summary>
        private async Task ExecuteCaptureSequenceAsync(
            ChildProcess child,
            IntPtr mutex,
            string processName)
        {
            try
            {
                //Capture BEFORE
                string bufferBefore = "";
                if (_previousSnapshots.ContainsKey(processName))
                {

                    bufferBefore = _previousSnapshots[processName] ?? "";
                }


                // Capture AFTER 
                string bufferAfterInput = await _consolePoller.CaptureCurrentConsoleAsync(
                    child,
                    mutex,
                    expandBuffer: false
                );

                // Extract input value
                string extractedCapture = DataInspector.ExtractDifference(
                    bufferBefore,
                    bufferAfterInput
                );
                LogManager.Instance.LogInfomation($" Extracted input: [{extractedCapture}]");
                // có thể thay equals => contain đối với bài nhiều client
                bool isClient = processName.Equals("Client", StringComparison.OrdinalIgnoreCase);

                if (isClient)
                {
                    // first visit when start 
                    if (bufferBefore.Length <= 0 || bufferBefore == null)
                    {
                        _testkitManagerService.ReceiveClientOutput(bufferAfterInput);
                    }
                    else
                    {
                        var (input, outputClient) = DataInspector.SplitInputFromOutput(extractedCapture);
                        if(!string.IsNullOrEmpty(outputClient))
                        {
                            if (input != null && !string.IsNullOrWhiteSpace(input))
                            {
                                _testkitManagerService.ReceiveUserInput(input, ActionKeywords.INPUT);
                            }
                            else
                            {
                                _testkitManagerService.ReceiveUserInput("", "UserInput");
                                LogManager.Instance.LogWarning($"Input is null");
                            }
                            _testkitManagerService.ReceiveClientOutput(outputClient);
                        }
                    }
                }
                else
                {
                    LogManager.Instance.LogDebug($"Processing SERVER capture");
                    if (!string.IsNullOrWhiteSpace(extractedCapture))
                    {
                        var outputSplit = extractedCapture.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

                        // Chỉ skip dòng đầu nếu nó là chuỗi rỗng
                        var outputLines = (outputSplit.Length > 0 && outputSplit[0] == "")
                            ? outputSplit.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l))
                            : outputSplit.Where(l => !string.IsNullOrWhiteSpace(l));

                        string outputResult = string.Join(Environment.NewLine, outputLines);
                        _testkitManagerService.ReceiveServerOutput(outputResult);
                    }
                }

                //  Update previous snapshot IMMEDIATELY after extracting input
                _previousSnapshots[processName] = bufferAfterInput;

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
                    _consoleManager.Free();
                }
                throw new FileNotFoundException($"Executable not found: {exePath}");
            }

            IntPtr mutex = _mutexManager.Create($"Global\\ConsoleAttachMutex_{name}");
            ChildProcess child = _processStarter.Start(exePath, name);


            if (showConsoleMessages)
            {
                _consoleManager.Alloc();
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

            // Stop all monitoring tasks
            foreach (var kvp in _monitoringTasks.ToList())
            {
                StopInputMonitoring(kvp.Key);
            }

            // Clear dictionaries
            _processHandles.Clear();
            _previousSnapshots.Clear();

        }

        public async Task CloseClientAsync()
        {

            try
            {
                if (_processHandles.TryGetValue("Client", out var handles))
                {
                    var (child, mutex) = handles;

                    // Stop input monitoring
                    StopInputMonitoring("Client");

                    // Remove from dictionaries BEFORE closing
                    _processHandles.TryRemove("Client", out _);
                    _previousSnapshots.TryRemove("Client", out _);

                    // Close process
                    CloseSingle(child, mutex, false);

                    LogManager.Instance.LogInfomation(" Client process closed");
                }
                else
                {
                    LogManager.Instance.LogWarning(" Client process not found");
                }
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($" Error closing Client: {ex.Message}");
                throw;
            }

            await Task.CompletedTask;
        }

        public async Task CloseServerAsync()
        {

            try
            {
                if (_processHandles.TryGetValue("Server", out var handles))
                {
                    var (child, mutex) = handles;

                    // Server không có input monitoring, chỉ cần remove và close
                    _processHandles.TryRemove("Server", out _);
                    _previousSnapshots.TryRemove("Server", out _);

                    // Close process
                    CloseSingle(child, mutex, false);

                    LogManager.Instance.LogInfomation(" Server process closed");
                }
                else
                {
                    LogManager.Instance.LogWarning(" Server process not found");
                }
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($" Error closing Server: {ex.Message}");
                throw;
            }

            await Task.CompletedTask;
        }

        // not using
        public async Task CaptureSnapshotOnlyAsync(ChildProcess child, nint mutex, string processName)
        {
            try
            {

                //  Capture console hiện tại
                string currentSnapshot = await _consolePoller.CaptureCurrentConsoleAsync(
                    child,
                    mutex,
                    expandBuffer: false
                );

                if (string.IsNullOrWhiteSpace(currentSnapshot))
                {
                    LogManager.Instance.LogWarning($" No output captured from {processName}");
                    return;
                }


                //  Update previous snapshot 
                _previousSnapshots[processName] = currentSnapshot;
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Error capturing snapshot for {processName}: {ex.Message}");
                LogManager.Instance.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        #endregion
    }
}