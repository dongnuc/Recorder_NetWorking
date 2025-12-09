using Common.Helper;
using Common.Helper.Kernel32API;
using Common.Helper.Kernel32API.Implement;
using Common.Helper.Kernel32API.Interface;
using Common.Interfaces.Logging;
using Common.Interfaces.Services;
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
        private readonly ISystemLogger _logger;

        private readonly ConcurrentDictionary<string, CancellationTokenSource> _monitoringTasks = new();
        private readonly ConcurrentDictionary<string, (ChildProcess child, IntPtr mutex)> _processHandles = new();
        private readonly ConcurrentDictionary<string, string> _previousSnapshots = new();
        private readonly SemaphoreSlim _captureLock = new SemaphoreSlim(1, 1);

        private volatile bool _isCapturing = false;

        #endregion

        #region Constructor

        /// <summary>
        /// ✅ REFACTORED: Constructor with full Dependency Injection including ISystemLogger
        /// </summary>
        public ProcessManager(
            ITestkitManagerService testkitManagerService,
            IProcessStarter processStarter,
            IConsolePoller consolePoller,
            IKeyListener keyListener,
            IMutexManager mutexManager,
            IConsoleManager consoleManager,
            IProcessWaiter processWaiter,
            ISystemLogger logger)
        {
            _testkitManagerService = testkitManagerService ?? throw new ArgumentNullException(nameof(testkitManagerService));
            _processStarter = processStarter ?? throw new ArgumentNullException(nameof(processStarter));
            _consolePoller = consolePoller ?? throw new ArgumentNullException(nameof(consolePoller));
            _keyListener = keyListener ?? throw new ArgumentNullException(nameof(keyListener));
            _mutexManager = mutexManager ?? throw new ArgumentNullException(nameof(mutexManager));
            _consoleManager = consoleManager ?? throw new ArgumentNullException(nameof(consoleManager));
            _processWaiter = processWaiter ?? throw new ArgumentNullException(nameof(processWaiter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                _logger.LogError($"Executable not found: {exePath}");
                throw new FileNotFoundException($"Executable not found: {exePath}");
            }

            //  RESET SNAPSHOT KHI START LẠI
            _previousSnapshots.TryRemove(name, out _);

            //  Start process
            var (child, mutex) = StartSingle(exePath, name, showConsoleMessages);

            // Store process handles
            _processHandles[name] = (child, mutex);

            await Task.Delay(3000);

            var cts = new CancellationTokenSource();
            StartInputMonitoring(child, mutex, name, cts.Token);

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
            _logger.LogInfomation($" Stopping {name} process...");

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

                _logger.LogInfomation($" {name} process stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error stopping {name}: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        #endregion

        #region Input Monitoring (Enter Key Detection) - New Logic

        /// <summary>
        ///  Monitor F12 key cho CẢ CLIENT VÀ SERVER
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
                DateTime lastTriggerTime = DateTime.MinValue;
                const int DEBOUNCE_MS = 300;

                bool isClient = processName.Equals("Client", StringComparison.OrdinalIgnoreCase);

                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (child.hProcess == IntPtr.Zero)
                        {
                            break;
                        }

                        //  CẢ CLIENT VÀ SERVER ĐỀU CHECK F12
                        bool currentEnterState = _keyListener.IsKeyPressed(Constants.VK_F12);

                        if (currentEnterState && !lastEnterState)
                        {
                            var timeSinceLastTrigger = DateTime.Now - lastTriggerTime;
                            if (timeSinceLastTrigger.TotalMilliseconds > DEBOUNCE_MS)
                            {
                                //  TRÁNH DUPLICATE PROCESSING
                                if (!_isCapturing)
                                {
                                    _isCapturing = true;
                                    try
                                    {
                                        await HandleF12PressAsync(processName);
                                    }
                                    finally
                                    {
                                        _isCapturing = false;
                                    }
                                }

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
                        _logger.LogError($"Error in input monitoring for {processName}: {ex.Message}");
                        await Task.Delay(1000, cts.Token);
                    }
                }

            }, cts.Token);
        }

        /// <summary>
        /// ✅ XỬ LÝ F12 PRESS VỚI ƯU TIÊN CLIENT TRƯỚC
        /// </summary>
        private async Task HandleF12PressAsync(string triggerProcess)
        {
            await _captureLock.WaitAsync();
            try
            {
                bool hasClient = _processHandles.ContainsKey("Client");
                bool hasServer = _processHandles.ContainsKey("Server");

                //  CASE 1: CẢ CLIENT VÀ SERVER ĐỀU RUNNING → ƯU TIÊN CLIENT
                if (hasClient && hasServer)
                {
                    // 1. Chụp Client trước
                    var clientHandles = _processHandles["Client"];
                    await ExecuteCaptureSequenceAsync(clientHandles.child, clientHandles.mutex, "Client");

                    // Delay để đảm bảo stage được tạo
                    await Task.Delay(100);

                    // 2. Chụp Server sau
                    var serverHandles = _processHandles["Server"];
                    await ExecuteCaptureSequenceAsync(serverHandles.child, serverHandles.mutex, "Server");
                }
                //  CASE 2: CHỈ CLIENT RUNNING
                else if (hasClient)
                {
                    var clientHandles = _processHandles["Client"];
                    await ExecuteCaptureSequenceAsync(clientHandles.child, clientHandles.mutex, "Client");
                }
                //  CASE 3: CHỈ SERVER RUNNING
                else if (hasServer)
                {
                    var serverHandles = _processHandles["Server"];
                    await ExecuteCaptureSequenceAsync(serverHandles.child, serverHandles.mutex, "Server");
                }
            }
            finally
            {
                _captureLock.Release();
            }
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
                var dataInspector = new DataInspector(_logger);
                string extractedCapture = dataInspector.ExtractDifference(
                    bufferBefore,
                    bufferAfterInput
                );

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
                        if (!string.IsNullOrEmpty(outputClient))
                        {
                            if (input != null && !string.IsNullOrWhiteSpace(input))
                            {
                                _testkitManagerService.ReceiveUserInput(input, ActionKeywords.INPUT);
                            }
                            else
                            {
                                _testkitManagerService.ReceiveUserInput("", "UserInput");
                                _logger.LogWarning($" Input is null");
                            }
                            _testkitManagerService.ReceiveClientOutput(outputClient);
                        }
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(extractedCapture))
                    {
                        var outputSplit = extractedCapture.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

                        var outputLines = (outputSplit.Length > 0 && outputSplit[0] == "")
                           ? outputSplit.Skip(1).Where(l => l != null)  
                           : outputSplit.Where(l => l != null);

                        string outputResult = string.Join(Environment.NewLine, outputLines);
                        _testkitManagerService.ReceiveServerOutput(outputResult);
                    }
                }

                _previousSnapshots[processName] = bufferAfterInput;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error in capture sequence for {processName}: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
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
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error stopping input monitoring: {ex.Message}");
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
                _logger.LogError($"Error closing process {child.name}: {ex.Message}");
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

            // ✅ DISPOSE SEMAPHORE
            _captureLock?.Dispose();
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

                    // ✅ RESET SNAPSHOT KHI CLOSE
                    _previousSnapshots.TryRemove("Client", out _);

                    // Close process
                    CloseSingle(child, mutex, false);

                }
                else
                {
                    _logger.LogWarning("⚠️ Client process not found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error closing Client: {ex.Message}");
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

                    // Stop input monitoring
                    StopInputMonitoring("Server");

                    // Remove from dictionaries BEFORE closing
                    _processHandles.TryRemove("Server", out _);

                    // ✅ RESET SNAPSHOT KHI CLOSE
                    _previousSnapshots.TryRemove("Server", out _);

                    // Close process
                    CloseSingle(child, mutex, false);

                }
                else
                {
                    _logger.LogWarning("⚠️ Server process not found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error closing Server: {ex.Message}");
                throw;
            }

            await Task.CompletedTask;
        }

        #endregion
    }
}