using Common.Helper;
using Common.Helper.Kernel32API;
using Common.Interfaces.IOFile;
using Common.Logging;
using Common.Models.Entities;
using Common.Resources;
using FileManagement.FileHelper.FileHandler;
using Middleware.Services;
using ProcessManagement.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using File = System.IO.File;

namespace WpfUI
{

    public partial class RecorderWindow : Window, INotifyPropertyChanged
    {
        #region Fields Process

        private ProcessManager _processManager;
        private ChildProcess _clientChild;
        private IntPtr _clientMutex;
        private CancellationTokenSource _clientCts;

        private ChildProcess _serverChild;
        private IntPtr _serverMutex;
        private CancellationTokenSource _serverCts;

        private int _currentStageIndex = 0;
        private readonly string _testCaseName;
        private readonly string _clientPath;
        private readonly string _serverPath;
        private readonly bool _isHttp;
        private bool _isClosing = false;

        // store NetwoekTransaction when middleware raised event
        private readonly Queue<NetworkTransaction> _pendingTransactions = new();
        private readonly object _transactionLock = new object();

        #endregion

        #region Properties UI
        private TestStage _selectedStageData = new TestStage();
        public TestStage SelectedStageData
        {
            get => _selectedStageData;
            set
            {
                if (_selectedStageData != value)
                {
                    _selectedStageData = value;
                    OnPropertyChanged(nameof(SelectedStageData));
                }
            }
        }
        public Dictionary<int, TestStage> TestStages = new Dictionary<int, TestStage>();
        private ObservableCollection<int> _stageKeys = new ObservableCollection<int>();
        public ObservableCollection<int> StageKeys
        {
            get => _stageKeys;
            set
            {
                _stageKeys = value;
                OnPropertyChanged();
            }
        }

        private int _selectedStageKey;
        public int SelectedStageKey
        {
            get => _selectedStageKey;
            set
            {
                if (_selectedStageKey != value)
                {
                    _selectedStageKey = value;
                    OnPropertyChanged();

                    // Update SelectedStageData khi user chọn stage khác
                    if (TestStages.TryGetValue(value, out var stage))
                    {
                        SelectedStageData = stage;
                    }
                }
            }
        }
        private TestStage CurrentTestStage
        {
            get
            {
                if (TestStages.TryGetValue(_currentStageIndex, out var stage))
                {
                    return stage;
                }

                LogManager.Instance.LogWarning($"⚠️ Stage {_currentStageIndex} not found, creating new one");
                var newStage = new TestStage();
                TestStages[_currentStageIndex] = newStage;
                return newStage;
            }
        }

        #endregion

        private bool _isClientRunning = false;
        private bool _isServerRunning = false;
        private bool _isMiddlewareRunning = false;
        private int _proxyPort = -1;
        private int _serverPort = -1;
        private bool _isStart = false;
        private string _testcasePath = string.Empty;

        #region Constructor

        public RecorderWindow(string testcasePath, string testCaseName,
            string clientPath, string serverPath, bool isHttp)
        {
            LogManager.Instance.LogDebug($"RecorderWindow ctor: this={this.GetHashCode()} - creating TestStages (initial count: {TestStages.Count})");
            InitializeComponent();
            _processManager = new ProcessManager();
            TestStages = new Dictionary<int, TestStage>();
            _testCaseName = testCaseName;
            _clientPath = clientPath;
            _serverPath = serverPath;
            _isHttp = isHttp;
            _testcasePath = testcasePath;
            DataContext = this;
            SubscribeToDataSources();
            LogManager.Instance.LogInfomation($"📝 Recorder window opened: {testCaseName}");
        }

        public async Task InitializeAsync()
        {
            var (proxyPort, serverPort) = PortChecker.GetTwoAvailablePorts(8000, 9000);

            if (!AppSettingsManager.UpdateAppSettings(_clientPath, proxyPort, _serverPath, serverPort))
            {
                MessageBox.Show("Failed to update appsettings.json. Check log for details.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _proxyPort = proxyPort; _serverPort = serverPort;

            await Task.Delay(500);
            try
            {
                //await MiddlewareStart.Instance.StartAsync(proxyPort, serverPort, _isHttp);
                //_isMiddlewareRunning = true;
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"❌ Failed to start processes: {ex.Message}");
                MessageBox.Show(
                    $"Failed to start processes!\n\n{ex.Message}",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                this.Close();
            }
        }

        /// <summary>
        /// ✅ Tự động start Server và Client
        /// </summary>
        private async Task StartProcessesAsync()
        {
            try
            {
                if (!File.Exists(_clientPath))
                {
                    throw new FileNotFoundException($"Client executable not found: {_clientPath}");
                }

                if (!File.Exists(_serverPath))
                {
                    throw new FileNotFoundException($"Server executable not found: {_serverPath}");
                }

                var serverResult = await _processManager.StartSingleWithPollingAsync(
                    _serverPath,
                    "Server",
                    isClient: false,
                    showConsoleMessages: false
                );

                _serverChild = serverResult.child;
                _serverMutex = serverResult.mutex;
                _serverCts = serverResult.cts;

                await Task.Delay(1500);

                //Client
                var clientResult = await _processManager.StartSingleWithPollingAsync(
                    _clientPath,
                    "Client",
                    isClient: true,
                    showConsoleMessages: false
                );

                //  ASSIGN to fields
                _clientChild = clientResult.child;
                _clientMutex = clientResult.mutex;
                _clientCts = clientResult.cts;

            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($" Error starting processes: {ex.Message}");
                LogManager.Instance.LogError($"   Stack: {ex.StackTrace}");
                throw;
            }
        }

        #endregion

        #region Initial Stage Creation

        /// <summary>
        /// Create Stage 1 with "START" action on startup
        /// This captures initial connection data before user input
        /// </summary>
        private void CreateInitialStage()
        {
            LogManager.Instance.LogDebug($"RecorderWindow ctor: this={this.GetHashCode()} - creating TestStages (initial count: {TestStages.Count})");
            _currentStageIndex = 1;
            var testStage = new TestStage();

            var initialInput = new InputClient
            {
                Stage = _currentStageIndex,
                Action = ActionKeywords.START,
                Input = string.Empty,
                DataType = "System"
            };

            testStage.InputClients.Add(initialInput);

            // Add to stage keys
            TestStages[_currentStageIndex] = testStage;
            StageKeys.Add(_currentStageIndex);
            SelectedStageKey = _currentStageIndex;

            SelectedStageData = testStage;

            LogManager.Instance.LogInfomation($"Initial stage created: Stage {_currentStageIndex} - {ActionKeywords.START}");
        }

        #endregion

        #region Subscribe to Data Sources

        private void SubscribeToDataSources()
        {
            _processManager.OnUserInput += OnUserInput;
            MiddlewareStart.Instance.OnTransactionCompleted += OnTransactionCompletedHandler;
            _processManager.OnClientOutput += OnClientOutput;
            _processManager.OnServerOutput += OnServerOutput;
            LogManager.Instance.LogDebug(" Subscribed to data sources with sequential processing");
        }

        private void UnsubscribeFromDataSources()
        {
            MiddlewareStart.Instance.OnTransactionCompleted -= OnTransactionCompletedHandler;

            if (_processManager != null)
            {
                _processManager.OnUserInput -= OnUserInput;
                _processManager.OnClientOutput -= OnClientOutput;
                _processManager.OnServerOutput -= OnServerOutput;
            }
            LogManager.Instance.LogDebug(" Unsubscribed from data sources");
        }

        #endregion

        #region Event Handlers - ProcessManager

        /// <summary>
        /// FIXED: Handle user input and create NEW STAGE
        /// </summary>
        private void OnUserInput(string input, string dataType)
        {
            LogManager.Instance.LogDebug($"Data input{input}");
            Dispatcher.Invoke(() =>
            {
                //  Increment stage ONLY when user inputs (Enter key pressed)
                _currentStageIndex++;
                var newTestStage = new TestStage();

                var inputClient = new InputClient
                {
                    Stage = _currentStageIndex,
                    Action = ActionKeywords.INPUT,
                    Input = input,
                    DataType = dataType
                };

                newTestStage.InputClients.Add(inputClient);
                TestStages[_currentStageIndex] = newTestStage;
                // Add stage key
                if (!StageKeys.Contains(_currentStageIndex))
                {
                    StageKeys.Add(_currentStageIndex);
                    SelectedStageKey = _currentStageIndex;
                }
                SelectedStageKey = _currentStageIndex;
                if (_isMiddlewareRunning)
                {
                    FlushPendingTransactions();
                }
                LogManager.Instance.LogInfomation($"Stage {_currentStageIndex} - Input: {input}");
                OnPropertyChanged(nameof(SelectedStageData));
            });
        }

        /// <summary>
        /// ✅ FIXED: Append to existing OutputClient or create new one for current stage
        /// All output in same stage will be combined
        /// </summary>
        private void OnClientOutput(string output)
        {
            LogManager.Instance.LogDebug($"Data output client {output}");

            Dispatcher.Invoke(() =>
            {
                UpdateClientOutput(output);
            });
        }

        private void UpdateClientOutput(string output)
        {
            try
            {
                LogManager.Instance.LogDebug($"RecorderWindow ctor: this={this.GetHashCode()} - creating TestStages (initial count: {TestStages.Count})");

                TestStages.TryGetValue(_currentStageIndex, out var currentStage);

                var outputClient = currentStage.OutputClients
                    .Where(o => o.Stage == _currentStageIndex)
                    .FirstOrDefault();

                if (outputClient != null)
                {
                    LogManager.Instance.LogDebug($" Updating existing OutputClient for Stage {_currentStageIndex}");
                    outputClient.Output = output;
                }
                else
                {
                    LogManager.Instance.LogDebug($" Creating new OutputClient for Stage {_currentStageIndex}");
                    outputClient = new OutputClient
                    {
                        Stage = _currentStageIndex,
                        Output = output
                    };
                    currentStage.OutputClients.Add(outputClient);
                }


                OnPropertyChanged(nameof(SelectedStageData));
                OnPropertyChanged(nameof(CurrentTestStage));
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($" UpdateClientOutput error: {ex.Message}");
                throw; // Re-throw để thấy error trong debugger
            }
        }


        /// <summary>
        /// FIXED: Append to existing OutputServer or create new one for current stage
        /// All output in same stage will be combined
        /// </summary>
        private void OnServerOutput(string output)
        {
            LogManager.Instance.LogDebug($"Data output server {output}");
            Dispatcher.Invoke(() => UpdateServerOutput(output));
        }

        private void UpdateServerOutput(string output)
        {
            try
            {
                LogManager.Instance.LogDebug($"📝 UpdateServerOutput - _currentStageIndex: {_currentStageIndex}");

                TestStages.TryGetValue(_currentStageIndex, out var currentStage);

                var outputServer = currentStage.OutputServers
                    .FirstOrDefault(o => o.Stage == _currentStageIndex);

                if (outputServer != null)
                {
                    outputServer.Output = output;
                }
                else
                {
                    outputServer = new OutputServer
                    {
                        Stage = _currentStageIndex,
                        Output = output
                    };
                    currentStage.OutputServers.Add(outputServer);
                }


                OnPropertyChanged(nameof(SelectedStageData));
                OnPropertyChanged(nameof(CurrentTestStage));
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"❌ UpdateServerOutput error: {ex.Message}");
                LogManager.Instance.LogError($"   Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        #endregion

        #region Event Handlers - Middleware

        private async void OnTransactionCompletedHandler(NetworkTransaction transaction)
        {
            LogManager.Instance.LogInfomation($"🌐 [MIDDLEWARE] Transaction received");
            LogManager.Instance.LogInfomation($"   Protocol: {transaction?.Protocol}");
            LogManager.Instance.LogInfomation($"   Request: {transaction?.Request?.Method} {transaction?.Request?.Url}");
            LogManager.Instance.LogInfomation($"   Response: {transaction?.Response?.StatusCode}");

            try
            {
                lock (_transactionLock)
                {
                    _pendingTransactions.Enqueue(transaction);
                    LogManager.Instance.LogDebug($"📦 Buffered transaction (Queue size: {_pendingTransactions.Count})");
                }

            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"❌ OnTransactionCompletedHandler error: {ex.Message}");
            }
        }


        private void FlushPendingTransactions()
        {
            lock (_transactionLock)
            {
                int count = _pendingTransactions.Count;

                if (count == 0)
                {
                    LogManager.Instance.LogDebug($"📦 No pending transactions to flush");
                    return;
                }

                LogManager.Instance.LogInfomation($"📦 Flushing {count} buffered transaction(s) to Stage {_currentStageIndex}");

                while (_pendingTransactions.Count > 0)
                {
                    var transaction = _pendingTransactions.Dequeue();

                    try
                    {
                        OnMiddlewareTransaction(transaction);
                    }
                    catch (Exception ex)
                    {
                        LogManager.Instance.LogError($"❌ Error processing buffered transaction: {ex.Message}");
                    }
                }

                LogManager.Instance.LogInfomation($"✅ Flushed {count} transaction(s) to Stage {_currentStageIndex}");
            }
        }


        /// <summary>
        /// FIXED v3.0: Create SEPARATE records for middleware data (không ghi đè console output)
        /// </summary>
        private void OnMiddlewareTransaction(NetworkTransaction transaction)
        {
            if (_isClientRunning && _isServerRunning)
            {
                Dispatcher.Invoke(() =>
                {
                    var outputServer = new OutputServer
                    {
                        Stage = _currentStageIndex,
                        Method = transaction.Request.Method,
                        DataRequest = transaction.Request.Body,
                        DataTypeMiddleware = transaction.Request.DataType,
                        ByteSize = transaction.Request.ByteSize.ToString(),
                        Output = null // Middleware record không có console output
                    };
                    CurrentTestStage.OutputServers.Add(outputServer);

                    var outputClient = new OutputClient
                    {
                        Stage = _currentStageIndex,
                        Method = transaction.Request.Method,
                        StatusCode = transaction.Response.StatusCode,
                        DataResponse = transaction.Response.Body,
                        DataTypeMiddleWare = transaction.Response.DataType,
                        ByteSize = transaction.Response.ByteSize.ToString(),
                        Output = null // Middleware record không có console output
                    };
                    CurrentTestStage.OutputClients.Add(outputClient);

                    OnPropertyChanged(nameof(SelectedStageData));

                    LogManager.Instance.LogDebug($"🌐 Stage {_currentStageIndex} - Middleware transaction recorded: {transaction.Request.Method} - {transaction.Response.StatusCode}");
                });
            }
        }

        #endregion

        #region Delete Methods

        private void BtnDeleteStage_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedStageKey == 0)
            {
                MessageBox.Show("No stage selected.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Prevent deleting stage 1 (initial connection stage)
            if (SelectedStageKey == 1)
            {
                var confirmResult = MessageBox.Show(
                    "Stage 1 is the initial connection stage.\n\nDeleting it will remove all connection data. Continue?",
                    "Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirmResult == MessageBoxResult.No)
                    return;
            }

            var result = MessageBox.Show(
                $"Delete stage {SelectedStageKey} and all its data?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Remove all data for this stage
                CurrentTestStage.InputClients.Where(i => i.Stage == SelectedStageKey).ToList()
                    .ForEach(i => CurrentTestStage.InputClients.Remove(i));

                CurrentTestStage.OutputClients.Where(o => o.Stage == SelectedStageKey).ToList()
                    .ForEach(o => CurrentTestStage.OutputClients.Remove(o));

                CurrentTestStage.OutputServers.Where(o => o.Stage == SelectedStageKey).ToList()
                    .ForEach(o => CurrentTestStage.OutputServers.Remove(o));

                StageKeys.Remove(SelectedStageKey);

                if (StageKeys.Any())
                {
                    SelectedStageKey = StageKeys.First();
                }
                LogManager.Instance.LogInfomation($"🗑️ Stage {SelectedStageKey} deleted");
            }
        }

        private void BtnDeleteInputClient_Click(object sender, RoutedEventArgs e)
        {
            if (dgInputClients.SelectedItem is InputClient selectedItem)
            {
                // Prevent deleting stage 1 initial input
                if (selectedItem.Stage == 1 && selectedItem.Action == ActionKeywords.START)
                {
                    var result = MessageBox.Show(
                        "This is the initial connection input. Delete anyway?",
                        "Warning",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                        return;
                }

                CurrentTestStage.InputClients.Remove(selectedItem);
                LogManager.Instance.LogDebug("Input client row deleted");
            }
        }

        private void BtnDeleteOutputClient_Click(object sender, RoutedEventArgs e)
        {
            if (dgOutputClients.SelectedItem is OutputClient selectedItem)
            {
                CurrentTestStage.OutputClients.Remove(selectedItem);
                LogManager.Instance.LogDebug("Output client row deleted");
            }
        }

        private void BtnDeleteOutputServer_Click(object sender, RoutedEventArgs e)
        {
            if (dgOutputServers.SelectedItem is OutputServer selectedItem)
            {
                CurrentTestStage.OutputServers.Remove(selectedItem);
                LogManager.Instance.LogDebug("Output server row deleted");
            }
        }

        #endregion

        #region UI Events

        private void CmbStages_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(SelectedStageData));
        }

        #endregion

        #region Window Lifecycle

        /// <summary>
        /// Handle window closing event
        /// Stop all processes and cleanup resources close tag
        /// </summary>
        protected override async void OnClosing(CancelEventArgs e)
        {
            var result = MessageBox.Show(
                "Stop recording and close?\n\nAll processes will be terminated.",
                "Confirm Close",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }

            try
            {
                LogManager.Instance.LogInfomation("Closing RecorderWindow - stopping all processes...");

                // Unsubscribe from events
                UnsubscribeFromDataSources();

                // Stop middleware
                if (MiddlewareStart.Instance.IsRunning)
                {
                    LogManager.Instance.LogDebug("Stopping middleware...");
                    await MiddlewareStart.Instance.StopAsync();
                }

                if (_isClientRunning)
                {
                    await CloseClientAsync();
                }

                if (_isServerRunning)
                {
                    //  Stop server process (with CancellationTokenSource)
                    await CloseServerAsync();
                }

                _processManager?.Dispose();
                LogManager.Instance.LogInfomation("Recording stopped - all processes terminated");
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($" Error closing recorder: {ex.Message}");
                LogManager.Instance.LogError($"Stack trace: {ex.StackTrace}");

                MessageBox.Show(
                    $"Error stopping processes:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            await CleanupAsync();

            base.OnClosing(e);
        }

        private async Task CloseClientAsync()
        {

            try
            {
                if (_clientChild.hProcess == IntPtr.Zero)
                {
                    LogManager.Instance.LogWarning("Client process already closed");
                    return;
                }

                LogManager.Instance.LogInfomation("Stopping Client process...");

                if (_clientCts != null)
                {
                    _clientCts.Cancel();
                }

                await _processManager.CloseClientAsync();

                _clientChild = default;
                _clientMutex = IntPtr.Zero;
                _clientCts?.Dispose();
                _clientCts = null;

                _isClientRunning = false;
                UpdateProcessButtonStates();

                LogManager.Instance.LogInfomation("Client process stopped successfully");

                if (!_isClientRunning && _isMiddlewareRunning)
                {
                    await StopMiddlewareAsync();
                }
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Error stopping client: {ex.Message}");
                LogManager.Instance.LogError($"   Stack trace: {ex.StackTrace}");
                throw;
            }

        }
        private async Task StopMiddlewareAsync()
        {
            if (MiddlewareStart.Instance.IsRunning)
            {
                LogManager.Instance.LogDebug("Stopping middleware...");
                await MiddlewareStart.Instance.StopAsync();
                _isMiddlewareRunning = false;
            }
        }

        private async Task CloseServerAsync()
        {


            try
            {
                if (_serverChild.hProcess == IntPtr.Zero)
                {
                    LogManager.Instance.LogWarning("⚠️ Server process already closed");
                    return;
                }
                LogManager.Instance.LogInfomation("Stopping Server process...");

                if (_serverCts != null)
                {
                    _serverCts?.Cancel();
                }

                await _processManager.CloseServerAsync();

                _serverChild = default;
                _serverMutex = IntPtr.Zero;
                _serverCts?.Dispose();
                _serverCts = null;

                _isServerRunning = false;
                UpdateProcessButtonStates();

                LogManager.Instance.LogInfomation("Server process stopped successfully");

                if (!_isServerRunning && _isMiddlewareRunning)
                {
                    await StopMiddlewareAsync();
                }
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Error stopping server: {ex.Message}");
                LogManager.Instance.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        private void BtnExportData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (TestStages == null || !TestStages.Any())
                {
                    MessageBox.Show(
                        "Không có dữ liệu để xuất.",
                        "Thông báo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Gộp tất cả InputClients từ tất cả các stage
                var allInputClients = TestStages
                    .OrderBy(x => x.Key)
                    .SelectMany(stage => stage.Value.InputClients ?? new ObservableCollection<InputClient>())
                    .Cast<object>()
                    .ToList();

                // Gộp tất cả OutputClients từ tất cả các stage
                var allOutputClients = TestStages
                    .OrderBy(x => x.Key)
                    .SelectMany(stage => stage.Value.OutputClients ?? new ObservableCollection<OutputClient>())
                    .Cast<object>()
                    .ToList();

                // Gộp tất cả OutputServers từ tất cả các stage
                var allOutputServers = TestStages
                    .OrderBy(x => x.Key)
                    .SelectMany(stage => stage.Value.OutputServers ?? new ObservableCollection<OutputServer>())
                    .Cast<object>()
                    .ToList();

                // Gộp tất cả OutputDBs từ tất cả các stage
                var allOutputDBs = TestStages
                    .OrderBy(x => x.Key)
                    .SelectMany(stage => stage.Value.OutputDBs ?? new ObservableCollection<OutputDB>())
                    .Cast<object>()
                    .ToList();

                // Tạo danh sách 4 sheets
                var sheetsList = new List<(string SheetName, ICollection<object> Data)>();

                if (allInputClients.Any())
                    sheetsList.Add(("InputClient", allInputClients));

                if (allOutputClients.Any())
                    sheetsList.Add(("OutputClient", allOutputClients));

                if (allOutputServers.Any())
                    sheetsList.Add(("OutputServer", allOutputServers));

                if (allOutputDBs.Any())
                    sheetsList.Add(("OutputDB", allOutputDBs));

                if (!sheetsList.Any())
                {
                    MessageBox.Show(
                        "Không có dữ liệu để xuất.",
                        "Thông báo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                var details = Path.Combine(_testcasePath, "detail.xlsx");

                // Gọi ExportToExcelParams với 4 sheets
                ExcelExecution exporter = new ExcelExecution();
                exporter.ExportToExcelParams(
                     details,
                    sheetsList.ToArray()
                );

                LogManager.Instance.LogInfomation($" Exported test data to: {details}");
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"❌ Error exporting data: {ex.Message}");
                MessageBox.Show(
                    $"Lỗi khi xuất dữ liệu:\n\n{ex.Message}",
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void BtnDeleteOutputDB_Click(object sender, RoutedEventArgs e)
        {

        }

        private void UpdateProcessButtonStates()
        {
            Dispatcher.Invoke(() =>
            {
                // Client buttons
                BtnStartClient.Visibility = _isClientRunning ? Visibility.Collapsed : Visibility.Visible;
                BtnCloseClient.Visibility = _isClientRunning ? Visibility.Visible : Visibility.Collapsed;

                // Server buttons
                BtnStartServer.Visibility = _isServerRunning ? Visibility.Collapsed : Visibility.Visible;
                BtnCloseServer.Visibility = _isServerRunning ? Visibility.Visible : Visibility.Collapsed;

                LogManager.Instance.LogDebug($" Buttons - Client running: {_isClientRunning}, Server running: {_isServerRunning}");
            });

        }

        private async void BtnCloseClient_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to stop the Client process?",
                "Confirm Close Client",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
                return;

            try
            {
                BtnCloseClient.IsEnabled = false;

                await CloseClientAsync();
                LogManager.Instance.LogInfomation(" Client process stopped successfully");

            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($" Error stopping client: {ex.Message}");
                MessageBox.Show(
                    $"Error stopping client process:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void BtnCloseServer_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
        "Are you sure you want to stop the Server process?",
        "Confirm Close Server",
        MessageBoxButton.YesNo,
        MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
                return;

            try
            {
                BtnCloseServer.IsEnabled = false;
                await CloseServerAsync();
                MessageBox.Show(
                    "Server process stopped successfully.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($" Error stopping server: {ex.Message}");
                MessageBox.Show(
                    $"Error stopping server process:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void BtnStartClient_Click(object sender, RoutedEventArgs e)
        {
            if (_isClientRunning)
            {
                MessageBox.Show("Server is already running.", "Info", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(_clientPath))
            {
                throw new FileNotFoundException($"Client executable not found: {_clientPath}");
            }

            if (_clientMutex != IntPtr.Zero && _clientCts != null)
            {
                try
                {
                    await CloseClientAsync();
                }
                catch (Exception ex)
                {
                    LogManager.Instance.LogError($"Error cleaning up old client: {ex.Message}");
                }
            }

            try
            {
                if (_currentStageIndex >= 1)
                {
                    _currentStageIndex++;
                }
                else
                {
                    _currentStageIndex = 1;
                }
                if (!_isMiddlewareRunning && _isServerRunning)
                {
                    LogManager.Instance.LogInfomation("Starting middleware...");
                    await MiddlewareStart.Instance.StartAsync(_proxyPort, _serverPort, _isHttp);
                    _isMiddlewareRunning = true;
                }
                //if (!_isStart)
                //{
                //    _isStart = true;
                //    CreateInitialStage();
                //}

                var testStage = new TestStage();

                var initialInput = new InputClient
                {
                    Stage = _currentStageIndex,
                    Action = ActionKeywords.START_CLIENT,
                    Input = string.Empty,
                    DataType = string.Empty
                };

                testStage.InputClients.Add(initialInput);

                // Add to stage keys
                TestStages[_currentStageIndex] = testStage;
                StageKeys.Add(_currentStageIndex);
                SelectedStageKey = _currentStageIndex;

                SelectedStageData = testStage;


                BtnStartClient.IsEnabled = false;
                var clientResult = await _processManager.StartSingleWithPollingAsync(
                    _clientPath,
                    "Client",
                    isClient: true,
                    showConsoleMessages: false
                );

                //  ASSIGN to fields
                _clientChild = clientResult.child;
                _clientMutex = clientResult.mutex;
                _clientCts = clientResult.cts;



                _isClientRunning = true;
                UpdateProcessButtonStates();
            }
            catch (Exception ex)
            {
            }
            finally
            {
                BtnStartClient.IsEnabled = true;
            }
        }

        private async void BtnStartServer_Click(object sender, RoutedEventArgs e)
        {
            if (_isServerRunning)
            {
                MessageBox.Show("Server is already running.", "Info", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(_serverPath))
            {
                throw new FileNotFoundException($"Server executable not found: {_serverPath}");
            }
            if (_serverMutex != IntPtr.Zero || _serverCts != null)
            {
                LogManager.Instance.LogWarning("Server process handle still exists, cleaning up...");

                try
                {
                    await CloseServerAsync();
                }
                catch (Exception ex)
                {
                    LogManager.Instance.LogError($"Error cleaning up old server: {ex.Message}");
                }
            }

            try
            {
                if (_currentStageIndex >= 1)
                {
                    _currentStageIndex++;
                }
                else
                {
                    _currentStageIndex = 1;
                }

                if (!_isMiddlewareRunning && _isClientRunning)
                {
                    LogManager.Instance.LogInfomation("Starting middleware...");
                    await MiddlewareStart.Instance.StartAsync(_proxyPort, _serverPort, _isHttp);
                    _isMiddlewareRunning = true;
                }
                //if (!_isStart)
                //{
                //    _isStart = true;
                //    CreateInitialStage();
                //}
                var testStage = new TestStage();

                var initialInput = new InputClient
                {
                    Stage = _currentStageIndex,
                    Action = ActionKeywords.START_SERVER,
                    Input = string.Empty,
                    DataType = string.Empty
                };

                testStage.InputClients.Add(initialInput);

                // Add to stage keys
                TestStages[_currentStageIndex] = testStage;
                StageKeys.Add(_currentStageIndex);
                SelectedStageKey = _currentStageIndex;

                SelectedStageData = testStage;



                BtnStartServer.IsEnabled = false;
                var serverResult = await _processManager.StartSingleWithPollingAsync(
                    _serverPath,
                    "Server",
                    isClient: false,
                    showConsoleMessages: false
                );

                _serverChild = serverResult.child;
                _serverMutex = serverResult.mutex;
                _serverCts = serverResult.cts;



                _isServerRunning = true;
                UpdateProcessButtonStates();
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Error starting server: {ex.Message}");
            }
            finally
            {
                BtnStartServer.IsEnabled = true;
            }
        }

        public async Task CleanupAsync()
        {
            if (_isClosing)
            {
                return; // Already cleaning up
            }

            _isClosing = true;

            try
            {
                LogManager.Instance?.LogInfomation("🛑 Cleaning up RecorderWindow resources...");

                // Unsubscribe from events FIRST
                UnsubscribeFromDataSources();

                // Stop middleware
                if (_isMiddlewareRunning)
                {
                    try
                    {
                        await StopMiddlewareAsync();
                    }
                    catch (Exception ex)
                    {
                        LogManager.Instance?.LogWarning($"Middleware stop failed: {ex.Message}");
                    }
                }

                // Stop client
                if (_isClientRunning)
                {
                    try
                    {
                        await CloseClientAsync();
                    }
                    catch (Exception ex)
                    {
                        LogManager.Instance?.LogWarning($"Client stop failed: {ex.Message}");
                    }
                }

                // Stop server
                if (_isServerRunning)
                {
                    try
                    {
                        await CloseServerAsync();
                    }
                    catch (Exception ex)
                    {
                        LogManager.Instance?.LogWarning($"Server stop failed: {ex.Message}");
                    }
                }

                // Dispose ProcessManager
                try
                {
                    _processManager?.Dispose();
                }
                catch (Exception ex)
                {
                    LogManager.Instance?.LogWarning($"ProcessManager dispose failed: {ex.Message}");
                }

                LogManager.Instance?.LogInfomation(" RecorderWindow cleanup completed");
            }
            catch (Exception ex)
            {
                LogManager.Instance?.LogError($" Error during cleanup: {ex.Message}");
                LogManager.Instance?.LogError($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}