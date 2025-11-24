using Common.Helper;
using Common.Helper.Kernel32API;
using Common.Interfaces.IOFile;
using Common.Interfaces.Services;
using Common.Logging;
using Common.Models.Entities;
using Common.Resources;
using FileManagement.FileHelper.FileHandler;
using Middleware.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using static Common.Models.Entities.MiddlewareModel;
using File = System.IO.File;

namespace WpfUI
{
    public partial class RecorderWindow : Window, INotifyPropertyChanged
    {
        #region Fields Process

        private IProcessManager _processManager;
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
        public Dictionary<int, TestStage> TestStages => _testkitManagerService?.GetCurrentTestStages() ?? new Dictionary<int, TestStage>();

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

                    //  Lấy data từ TestkitManagerService
                    var testStages = _testkitManagerService?.GetCurrentTestStages();
                    if (testStages != null && testStages.TryGetValue(value, out var stage))
                    {
                        SelectedStageData = stage;
                        LogManager.Instance.LogDebug($"Stage {value} selected - Data loaded");
                    }
                    else
                    {
                        LogManager.Instance.LogWarning($"Stage {value} not found in TestkitManagerService");
                    }
                }
            }
        }

        private int _pendingTransactionCount = 0;
        public int PendingTransactionCount
        {
            get => _pendingTransactionCount;
            set
            {
                if (_pendingTransactionCount != value)
                {
                    _pendingTransactionCount = value;
                    OnPropertyChanged(nameof(PendingTransactionCount));
                }
            }
        }
        #endregion

        private bool _isClientRunning = false;
        private bool _isServerRunning = false;
        private bool _isMiddlewareRunning = false;
        private int _proxyPort = -1;
        private int _serverPort = -1;
        private string _testcasePath = string.Empty;
        private IOFileHandler _fileHandler;
        private readonly ITestkitManagerService _testkitManagerService;
        private readonly object _middlewareLock = new object();

        #region Constructor

        public RecorderWindow(string testcasePath, string testCaseName,
            string clientPath, string serverPath, bool isHttp,
            IProcessManager processManager, IOFileHandler fileHandler,
            ITestkitManagerService testkitManagerService)
        {
            LogManager.Instance.LogDebug($"RecorderWindow ctor: this={this.GetHashCode()} - creating TestStages (initial count: {TestStages.Count})");
            InitializeComponent();

            _testcasePath = testcasePath;
            _testCaseName = testCaseName;
            _clientPath = clientPath;
            _serverPath = serverPath;
            _isHttp = isHttp;
            _fileHandler = fileHandler;
            _processManager = processManager;
            _testkitManagerService = testkitManagerService;

            DataContext = this;
            SubscribeToDataSources();
            LogManager.Instance.LogInfomation($"📝 Recorder window opened: {testCaseName}");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.DataContext = null;
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
        }

        #endregion


        #region Subscribe to Data Sources

        private void SubscribeToDataSources()
        {
            _testkitManagerService.OnStageCreated += OnStageCreated;
            _testkitManagerService.OnStageUpdated += OnStageUpdated;
            _testkitManagerService.OnStagesChanged += OnStagesChanged;
            _testkitManagerService.OnTransactionReceived += OnTransactionReceived;
            LogManager.Instance.LogDebug(" Subscribed to data sources with sequential processing");
        }

        private void UnsubscribeFromDataSources()
        {
            if (_testkitManagerService != null)
            {
                _testkitManagerService.OnStageCreated -= OnStageCreated;
                _testkitManagerService.OnStageUpdated -= OnStageUpdated;
                _testkitManagerService.OnStagesChanged -= OnStagesChanged;
                _testkitManagerService.OnTransactionReceived -= OnTransactionReceived;

            }
            LogManager.Instance.LogDebug(" Unsubscribed from data sources");
        }

        #endregion

        #region Event Handlers - ProcessManager

        private void OnTransactionReceived(NetworkTransaction transaction)
        {
            Dispatcher.Invoke(() =>
            {
                PendingTransactionCount = _testkitManagerService.GetPendingTransactionCount();
            });
        }

        /// <summary>
        /// Handle user input and create NEW STAGE
        /// </summary>
        private void OnStageCreated(int stageIndex)
        {
            Dispatcher.Invoke(() =>
            {
                LogManager.Instance.LogDebug($" Stage {stageIndex} created");

                // Add to stage keys if not exists
                if (!StageKeys.Contains(stageIndex))
                {
                    StageKeys.Add(stageIndex);
                }

                // Select newly created stage
                SelectedStageKey = stageIndex;

                OnPropertyChanged(nameof(TestStages));

                PendingTransactionCount = _testkitManagerService.GetPendingTransactionCount();
            });
        }

        /// <summary>
        /// Update single object properties in current stage
        /// </summary>
        private void OnStageUpdated(int stageIndex)
        {
            Dispatcher.Invoke(() =>
            {
                LogManager.Instance.LogDebug($" Stage {stageIndex} updated");

                // Refresh UI if currently viewing this stage
                if (SelectedStageKey == stageIndex)
                {
                    var testStages = _testkitManagerService.GetCurrentTestStages();
                    if (testStages.TryGetValue(stageIndex, out var stage))
                    {
                        SelectedStageData = stage;
                    }
                }

                OnPropertyChanged(nameof(SelectedStageData));
                OnPropertyChanged(nameof(TestStages));
            });
        }

        private void OnStagesChanged(Dictionary<int, TestStage> testStages)
        {
            Dispatcher.Invoke(() =>
            {
                LogManager.Instance.LogDebug($" Test stages changed (total: {testStages.Count})");

                // Refresh current stage data
                int currentStageIndex = _testkitManagerService.GetCurrentStageIndex();
                if (testStages.TryGetValue(currentStageIndex, out var currentStage))
                {
                    SelectedStageData = currentStage;
                }

                OnPropertyChanged(nameof(SelectedStageData));
                OnPropertyChanged(nameof(TestStages));
            });
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
                // Get test stages from TestkitManagerService
                var testStages = _testkitManagerService.GetCurrentTestStages();
                if (testStages.ContainsKey(SelectedStageKey))
                {
                    // Remove stage from dictionary
                    testStages.Remove(SelectedStageKey);

                    // Remove from UI collection
                    StageKeys.Remove(SelectedStageKey);

                    // Select another stage if available
                    if (StageKeys.Any())
                    {
                        SelectedStageKey = StageKeys.First();
                    }
                    else
                    {
                        // No stages left, clear selected data
                        SelectedStageData = new TestStage();
                    }

                    LogManager.Instance.LogInfomation($" Stage {SelectedStageKey} deleted");

                    // Notify UI to refresh
                    OnPropertyChanged(nameof(TestStages));
                    OnPropertyChanged(nameof(SelectedStageData));
                }
                else
                {
                    LogManager.Instance.LogWarning($" Stage {SelectedStageKey} not found in TestkitManagerService");
                    MessageBox.Show(
                        $"Stage {SelectedStageKey} not found.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void BtnClearInputClient_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Clear all User input data for this stage?",
                "Confirm Clear",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var testStages = _testkitManagerService.GetCurrentTestStages();
                if (testStages.TryGetValue(SelectedStageKey, out var currentStage))
                {
                    currentStage.User = null;
                    OnPropertyChanged(nameof(SelectedStageData));
                    LogManager.Instance.LogDebug($" User input cleared for Stage {SelectedStageKey}");
                }
            }
        }

        private void BtnClearOutputClient_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Clear all Client output data for this stage?",
                "Confirm Clear",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var testStages = _testkitManagerService.GetCurrentTestStages();
                if (testStages.TryGetValue(SelectedStageKey, out var currentStage))
                {
                    currentStage.Client = null;
                    OnPropertyChanged(nameof(SelectedStageData));
                    LogManager.Instance.LogDebug($" Client output cleared for Stage {SelectedStageKey}");
                }
            }
        }

        private void BtnClearOutputServer_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Clear all Server output data for this stage?",
                "Confirm Clear",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var testStages = _testkitManagerService.GetCurrentTestStages();
                if (testStages.TryGetValue(SelectedStageKey, out var currentStage))
                {
                    currentStage.Server = null;
                    OnPropertyChanged(nameof(SelectedStageData));
                    LogManager.Instance.LogDebug($" Server output cleared for Stage {SelectedStageKey}");
                }
            }
        }

        private void BtnClearOutputDB_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Clear all Database data for this stage?",
                "Confirm Clear",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var testStages = _testkitManagerService.GetCurrentTestStages();
                if (testStages.TryGetValue(SelectedStageKey, out var currentStage))
                {
                    currentStage.Database = new Database();
                    OnPropertyChanged(nameof(SelectedStageData));
                    LogManager.Instance.LogDebug($" Database data cleared for Stage {SelectedStageKey}");
                }
            }
        }

        private void BtnClearNetwork_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Clear all Network data for this stage?",
                "Confirm Clear",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var testStages = _testkitManagerService.GetCurrentTestStages();
                if (testStages.TryGetValue(SelectedStageKey, out var currentStage))
                {
                    currentStage.Network = new Network();
                    OnPropertyChanged(nameof(SelectedStageData));
                    LogManager.Instance.LogDebug($" Network data cleared for Stage {SelectedStageKey}");
                }
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
            if (_isClosing)
            {
                try
                {
                    StageKeys?.Clear();
                    SelectedStageData = null;
                    this.DataContext = null;
                    BindingOperations.ClearAllBindings(this);
                }
                catch { }
                base.OnClosing(e);
                return;
            }
            //e.Cancel = true;
            //var result = MessageBox.Show(
            //    "Stop recording and close?\n\nAll processes will be terminated.",
            //    "Confirm Close",
            //    MessageBoxButton.YesNo,
            //    MessageBoxImage.Question);

            //if (result == MessageBoxResult.No)
            //{
            //    e.Cancel = true;
            //    return;
            //}

            try
            {
                this.IsEnabled = false;
                _isClosing = true;
                await CleanupAsync();
                this.Close();
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
                _isClosing = true;
                base.OnClosing(e);
            }

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
                if (!_isClosing)
                {
                    UpdateProcessButtonStates();
                }
                LogManager.Instance.LogInfomation("Client process stopped successfully");

                if (!_isClientRunning && _isMiddlewareRunning && !_isClosing)
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
            bool shouldStop = false;
            lock (_middlewareLock)
            {
                if (_isMiddlewareRunning)
                {
                    _isMiddlewareRunning = false;
                    shouldStop = true;
                }
            }
            if (!shouldStop)
            {
                LogManager.Instance.LogDebug("Middleware already stopped by another thread");
                return;
            }
            try
            {
                if (MiddlewareStart.Instance.IsRunning)
                {
                    LogManager.Instance.LogDebug("Stopping middleware...");
                    await Task.Delay(200);
                    await MiddlewareStart.Instance.StopAsync();
                }
            }
            catch (Exception ex)
            {
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
                if (!_isClosing)
                {
                    UpdateProcessButtonStates();
                }

                LogManager.Instance.LogInfomation("Server process stopped successfully");

                if (!_isServerRunning && _isMiddlewareRunning && !_isClosing)
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
                var testStages = _testkitManagerService.GetCurrentTestStages();

                if (testStages == null || !testStages.Any())
                {
                    MessageBox.Show(
                        "Không có dữ liệu để xuất.",
                        "Thông báo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var allUsers = testStages
                    .OrderBy(x => x.Key)
                    .Where(stage => stage.Value.User != null && stage.Value.User.Stage > 0)
                    .Select(stage => stage.Value.User)
                    .Cast<object>()
                    .ToList();

                var allClients = testStages
                    .OrderBy(x => x.Key)
                    .Where(stage => stage.Value.Client != null && stage.Value.Client.Stage > 0)
                    .Select(stage => stage.Value.Client)
                    .Cast<object>()
                    .ToList();

                var allServers = testStages
                    .OrderBy(x => x.Key)
                    .Where(stage => stage.Value.Server != null && stage.Value.Server.Stage > 0)
                    .Select(stage => stage.Value.Server)
                    .Cast<object>()
                    .ToList();

                var allDatabases = testStages
                    .OrderBy(x => x.Key)
                    .Where(stage => stage.Value.Database != null && stage.Value.Database.Stage > 0)
                    .Select(stage => stage.Value.Database)
                    .Cast<object>()
                    .ToList();

                var allNetworks = testStages
                    .OrderBy(x => x.Key)
                    .Where(stage => stage.Value.Network != null && stage.Value.Network.Stage > 0)
                    .Select(stage => stage.Value.Network)
                    .Cast<object>()
                    .ToList();

                var sheetsList = new List<(string SheetName, ICollection<object> Data)>
                {
                    ("User", allUsers),
                    ("Client", allClients),
                    ("Server", allServers),
                    ("Database", allDatabases),
                    ("Network", allNetworks)
                };
                if (!sheetsList.Any())
                {
                    MessageBox.Show(
                        "Không có dữ liệu để xuất.",
                        "Thông báo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var detailsPath = Path.Combine(_testcasePath, "Detail.xlsx");


                ExcelExecution exporter = new ExcelExecution();
                exporter.ExportToExcelParams(detailsPath, sheetsList.ToArray());

                LogManager.Instance.LogInfomation($" Exported test data to: {detailsPath}");
                MessageBox.Show($"Data exported successfully to:\n{detailsPath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($" Error exporting data: {ex.Message}");
                MessageBox.Show($"Lỗi khi xuất dữ liệu:\n\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateProcessButtonStates()
        {
            Dispatcher.Invoke(() =>
            {
                // Client buttons
                BtnStartClient.Visibility = _isClientRunning ? Visibility.Collapsed : Visibility.Visible;
                BtnStartClient.IsEnabled = true;
                BtnCloseClient.Visibility = _isClientRunning ? Visibility.Visible : Visibility.Collapsed;
                BtnCloseClient.IsEnabled = true;

                // Server buttons
                BtnStartServer.Visibility = _isServerRunning ? Visibility.Collapsed : Visibility.Visible;
                BtnStartServer.IsEnabled = true;
                BtnCloseServer.Visibility = _isServerRunning ? Visibility.Visible : Visibility.Collapsed;
                BtnCloseServer.IsEnabled = true;

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
            finally
            {
                BtnCloseClient.IsEnabled = true;
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
            finally
            {
                BtnCloseServer.IsEnabled = true;
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
                BtnStartClient.IsEnabled = false;
                _testkitManagerService.CreateInitialStage(ActionKeywords.START_CLIENT);

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
                _testkitManagerService.CreateInitialStage(ActionKeywords.START_SERVER);

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
            try
            {

                // Unsubscribe from events FIRST
                UnsubscribeFromDataSources();

                var tasks = new List<Task>();
                // Stop client
                if (_isClientRunning)
                {
                    var clientTask = Task.Run(async () =>
                    {
                        try
                        {
                            await CloseClientAsync();
                        }
                        catch (Exception ex)
                        {
                            LogManager.Instance?.LogWarning($"Client stop failed: {ex.Message}");
                        }
                    });
                    tasks.Add(clientTask);
                }

                // Stop server
                if (_isServerRunning)
                {
                    var serverTask = Task.Run(async () =>
                    {
                        try
                        {
                            await CloseServerAsync();
                        }
                        catch (Exception ex)
                        {
                            LogManager.Instance?.LogWarning($"Server stop failed: {ex.Message}");
                        }
                    });
                    tasks.Add(serverTask);
                }
                if (tasks.Any())
                {
                    LogManager.Instance?.LogInfomation($"Waiting for {tasks.Count} process(es) to close...");
                    await Task.WhenAll(tasks);
                }
                await Task.Delay(1000);
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

                // Dispose ProcessManager
                try
                {
                    _processManager?.Dispose();
                }
                catch (Exception ex)
                {
                    LogManager.Instance?.LogWarning($"ProcessManager dispose failed: {ex.Message}");
                }

            }
            catch (Exception ex)
            {
                LogManager.Instance?.LogError($" Error during cleanup: {ex.Message}");
                LogManager.Instance?.LogError($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}