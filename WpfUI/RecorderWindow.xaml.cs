using Common.Helper;
using Common.Interfaces.IOFile;
using Common.Interfaces.Logging;
using Common.Interfaces.Services;
using Common.Models.Entities;
using Common.Resources;
using FileManagement.FileHelper.FileHandler;
using NetworkMonitor.Models;
using NetworkMonitor.Services;
using SharpPcap;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WpfUI.Properties;
using File = System.IO.File;

namespace WpfUI
{
    public partial class RecorderWindow : Window, INotifyPropertyChanged
    {
        #region Fields Process

        private IProcessManager _processManager;
        private CancellationTokenSource? _clientCts;
        private CancellationTokenSource? _serverCts;

        private int _actualServerPort = 4000;
        private string _serverExecutableDir = "";

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
                    }
                    else
                    {
                        _logger.LogWarning($"Stage {value} not found in TestkitManagerService");
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
        private bool _isMonitorRunning = false;
        private string _testcasePath = string.Empty;
        private IOFileHandler _fileHandler;
        private readonly ITestkitManagerService _testkitManagerService;
        private readonly ISystemLogger _logger;
        private PacketCaptureService? _serviceMonitor;
        private ILiveDevice? _device;
        private CancellationToken _ctsMonitor;

        #region Constructor

        public RecorderWindow(string testcasePath, string testCaseName,
            string clientPath, string serverPath, bool isHttp,
            IProcessManager processManager, IOFileHandler fileHandler,
            ITestkitManagerService testkitManagerService,
            ISystemLogger logger)
        {
            InitializeComponent();

            _testcasePath = testcasePath;
            _testCaseName = testCaseName;
            _clientPath = clientPath;
            _serverPath = serverPath;
            _isHttp = isHttp;
            _fileHandler = fileHandler;
            _processManager = processManager;
            _testkitManagerService = testkitManagerService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            DataContext = this;
            SubscribeToDataSources();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.DataContext = null;
        }

        public async Task<bool> InitializeAsync()
        {
            _serverExecutableDir = Path.GetDirectoryName(_serverPath) ?? "";

            var protocol = Settings.Default.Protocol ?? "TCP";
            _serviceMonitor = new PacketCaptureService(_testkitManagerService, protocol);
            var devices = SharpPcap.CaptureDeviceList.Instance;

            _actualServerPort = AppSettingsManager.ReadPortFromExePath(_serverPath, _logger) ?? -1;
            // fix port 3000
            if (_actualServerPort == -1)
            {
                _actualServerPort = 3000;
            }

            if (devices.Count == 0)
            {
                return false;
            }

            _device = devices.FirstOrDefault(d =>
                    d.Description != null &&
                    d.Description.ToLower().Contains("loopback"));

            //Not Found Loopback, => (Fallback)
            if (_device == null)
            {
                _device = devices.FirstOrDefault();
                if (_device != null)
                {
                    _logger.LogWarning("⚠️ Không tìm thấy Loopback Adapter! Đang sử dụng card mạng vật lý: " + _device.Description);
                    _logger.LogWarning("Lưu ý: Bạn sẽ KHÔNG bắt được traffic localhost (127.0.0.1).");
                }
            }

            _ctsMonitor = new CancellationToken();
            await Task.Delay(500);
            return true;
        }

        #endregion

        #region Subscribe to Data Sources

        private void SubscribeToDataSources()
        {
            _testkitManagerService.OnStageCreated += OnStageCreated;
            _testkitManagerService.OnStageUpdated += OnStageUpdated;
            _testkitManagerService.OnStagesChanged += OnStagesChanged;
            _testkitManagerService.OnQueueCountChanged += OnQueueCountChangedHandler;
        }

        private void UnsubscribeFromDataSources()
        {
            if (_testkitManagerService != null)
            {
                _testkitManagerService.OnStageCreated -= OnStageCreated;
                _testkitManagerService.OnStageUpdated -= OnStageUpdated;
                _testkitManagerService.OnStagesChanged -= OnStagesChanged;
                _testkitManagerService.OnQueueCountChanged -= OnQueueCountChangedHandler;
            }
        }

        #endregion

        #region Event Handlers - ProcessManager

        private void OnStageCreated(int stageIndex)
        {
            Dispatcher.Invoke(() =>
            {
                if (!StageKeys.Contains(stageIndex))
                {
                    StageKeys.Add(stageIndex);
                }
                SelectedStageKey = stageIndex;
                OnPropertyChanged(nameof(TestStages));
            });
        }

        private void OnStageUpdated(int stageIndex)
        {
            Dispatcher.Invoke(() =>
            {
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

        #region Event Handlers - Network monitor

        private void OnQueueCountChangedHandler(int count)
        {
            Dispatcher.Invoke(() => PendingTransactionCount = count);
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
                _testkitManagerService.DeleteStage(SelectedStageKey);

                if (StageKeys.Contains(SelectedStageKey))
                {
                    StageKeys.Remove(SelectedStageKey);
                }

                if (StageKeys.Any())
                {
                    SelectedStageKey = StageKeys.Last();
                }
                else
                {
                    SelectedStageKey = 0;
                    SelectedStageData = new TestStage();
                }

                OnPropertyChanged(nameof(TestStages));
                OnPropertyChanged(nameof(SelectedStageData));
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
                    _logger.LogDebug($" User input cleared for Stage {SelectedStageKey}");
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
                    _logger.LogDebug($" Client output cleared for Stage {SelectedStageKey}");
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
                    if (currentStage.NetworkHttpFlows != null)
                    {
                        currentStage.NetworkHttpFlows.Clear();
                    }

                    if (currentStage.NetworkTcpFlows != null)
                    {
                        currentStage.NetworkTcpFlows.Clear();
                    }

                    OnPropertyChanged(nameof(SelectedStageData));
                    _logger.LogDebug($"Network data cleared for Stage {SelectedStageKey}");
                }
            }
        }

        #region Delete Single Row Methods

        private void BtnDeleteTcpRow_Click(object sender, RoutedEventArgs e)
        {
            if (dgTcp.SelectedItems.Count == 0) return;
            var collection = dgTcp.ItemsSource as System.Collections.IList;

            if (collection != null)
            {
                var itemsToDelete = new System.Collections.ArrayList(dgTcp.SelectedItems);
                foreach (var item in itemsToDelete)
                {
                    collection.Remove(item);
                }
            }
        }

        private void BtnDeleteHttpRow_Click(object sender, RoutedEventArgs e)
        {
            if (dgHttp.SelectedItems.Count == 0) return;
            var collection = dgHttp.ItemsSource as System.Collections.IList;
            if (collection != null)
            {
                var itemsToDelete = new System.Collections.ArrayList(dgHttp.SelectedItems);
                foreach (var item in itemsToDelete)
                {
                    collection.Remove(item);
                }
            }
        }

        #endregion

        #endregion

        #region UI Events

        private void CmbStages_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(SelectedStageData));
        }

        #endregion

        #region Window Lifecycle

        protected override async void OnClosing(CancelEventArgs e)
        {
            if (_isClosing)
            {
                try
                {
                    StageKeys?.Clear();
                    SelectedStageData = new TestStage();
                    this.DataContext = null;
                    BindingOperations.ClearAllBindings(this);
                }
                catch { }
                base.OnClosing(e);
                return;
            }

            try
            {
                this.IsEnabled = false;
                _isClosing = true;
                await CleanupAsync();
                this.Close();
            }
            catch (Exception ex)
            {
                _logger.LogError($" Error closing recorder: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");

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
                _logger.LogInfomation("Stopping Client process...");

                if (_clientCts != null)
                {
                    _clientCts.Cancel();
                }

                await _processManager.CloseClientAsync();

                _clientCts?.Dispose();
                _clientCts = null;

                _isClientRunning = false;
                if (!_isClosing)
                {
                    UpdateProcessButtonStates();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error stopping client: {ex.Message}");
                _logger.LogError($"   Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private async Task CloseServerAsync()
        {
            try
            {
                _logger.LogInfomation("Stopping Server process...");

                if (_serverCts != null)
                {
                    _serverCts?.Cancel();
                }

                await _processManager.CloseServerAsync();

                _serverCts?.Dispose();
                _serverCts = null;

                _isServerRunning = false;
                if (!_isClosing)
                {
                    UpdateProcessButtonStates();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error stopping server: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void dgTcp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgTcp.SelectedItem is TcpNetworkFlow selectedItem)
            {
                txtTcpPayload.Text = selectedItem.Data;
            }
            else
            {
                txtTcpPayload.Text = "";
            }
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
                    .Select(stage => stage.Value.User!)
                    .Cast<object>()
                    .ToList();

                var allClients = testStages
                    .OrderBy(x => x.Key)
                    .Where(stage => stage.Value.Client != null && stage.Value.Client.Stage > 0)
                    .Select(stage => stage.Value.Client!)
                    .Cast<object>()
                    .ToList();

                var allServers = testStages
                    .OrderBy(x => x.Key)
                    .Where(stage => stage.Value.Server != null && stage.Value.Server.Stage > 0)
                    .Select(stage => stage.Value.Server!)
                    .Cast<object>()
                    .ToList();

                var allDatabases = testStages
                    .OrderBy(x => x.Key)
                    .Where(stage => stage.Value.Database != null && stage.Value.Database.Stage > 0)
                    .Select(stage => stage.Value.Database!)
                    .Cast<object>()
                    .ToList();

                var allNetworksTCP = testStages
                    .OrderBy(x => x.Key)
                    .Where(stage => stage.Value.NetworkTcpFlows != null && stage.Value.NetworkTcpFlows.Count > 0)
                    .SelectMany(stage => stage.Value.NetworkTcpFlows!)
                    .Cast<object>()
                    .ToList();

                var allNetworksHTTP = testStages
                    .OrderBy(x => x.Key)
                    .Where(stage => stage.Value.NetworkHttpFlows != null && stage.Value.NetworkHttpFlows.Count > 0)
                    .SelectMany(stage => stage.Value.NetworkHttpFlows!)
                    .Cast<object>()
                    .ToList();

                var sheetsList = new List<(string SheetName, ICollection<object> Data)>
                {
                    ("User", allUsers),
                    ("Client", allClients),
                    ("Server", allServers),
                    ("Database", allDatabases),
                    ("Network", allNetworksTCP.Count > 0 ? allNetworksTCP : allNetworksHTTP),
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

                _logger.LogInfomation($" Exported test data to: {detailsPath}");
                MessageBox.Show($"Data exported successfully to:\n{detailsPath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError($" Error exporting data: {ex.Message}");
                MessageBox.Show($"Lỗi khi xuất dữ liệu:\n\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateProcessButtonStates()
        {
            Dispatcher.Invoke(() =>
            {
                BtnStartClient.Visibility = _isClientRunning ? Visibility.Collapsed : Visibility.Visible;
                BtnStartClient.IsEnabled = true;
                BtnCloseClient.Visibility = _isClientRunning ? Visibility.Visible : Visibility.Collapsed;
                BtnCloseClient.IsEnabled = true;

                BtnStartServer.Visibility = _isServerRunning ? Visibility.Collapsed : Visibility.Visible;
                BtnStartServer.IsEnabled = true;
                BtnCloseServer.Visibility = _isServerRunning ? Visibility.Visible : Visibility.Collapsed;
                BtnCloseServer.IsEnabled = true;
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
                _testkitManagerService.CreateCloseClientStage();
                _logger.LogInfomation(" Client process stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($" Error stopping client: {ex.Message}");
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
                _testkitManagerService.CreateCloseServerStage();
                MessageBox.Show(
                    "Server process stopped successfully.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError($" Error stopping server: {ex.Message}");
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
                MessageBox.Show("Client is already running.", "Info", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(_clientPath))
            {
                _logger.LogError($"Client executable not found: {_clientPath}");
                MessageBox.Show($"Client executable not found: {_clientPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
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

                BtnStartClient.IsEnabled = false;
                _testkitManagerService.CreateInitialStage(ActionKeywords.START_CLIENT);

                if (!_isMonitorRunning && _serviceMonitor != null && _device != null)
                {
                    var startupSignal = new TaskCompletionSource<bool>();
                    Task monitorTask = _serviceMonitor.StartCaptureAsync(_device, _actualServerPort.ToString(), "", _ctsMonitor, startupSignal);
                    await startupSignal.Task;
                    _isMonitorRunning = true;
                }

                var clientResult = await _processManager.StartSingleWithPollingAsync(
                    _clientPath,
                    "Client",
                    isClient: true,
                    showConsoleMessages: false
                );

                _clientCts = clientResult.cts;
                _isClientRunning = true;

                UpdateProcessButtonStates();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting client: {ex.Message}");
                MessageBox.Show($"Error starting client: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                _logger.LogError($"Server executable not found: {_serverPath}");
                MessageBox.Show($"Server executable not found: {_serverPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
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

                _testkitManagerService.CreateInitialStage(ActionKeywords.START_SERVER);

                BtnStartServer.IsEnabled = false;
                var serverResult = await _processManager.StartSingleWithPollingAsync(
                    _serverPath,
                    "Server",
                    isClient: false,
                    showConsoleMessages: false
                );

                _serverCts = serverResult.cts;
                _isServerRunning = true;

                UpdateProcessButtonStates();
                if (!_isMonitorRunning && _serviceMonitor != null && _device != null)
                {
                    var startupSignal = new TaskCompletionSource<bool>();
                    Task monitorTask = _serviceMonitor.StartCaptureAsync(_device, _actualServerPort.ToString(), "", _ctsMonitor, startupSignal);
                    await startupSignal.Task;
                    _isMonitorRunning = true;
                }
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting server: {ex.Message}");
                MessageBox.Show($"Error starting server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                UnsubscribeFromDataSources();

                var tasks = new List<Task>();
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
                            _logger?.LogWarning($"Client stop failed: {ex.Message}");
                        }
                    });
                    tasks.Add(clientTask);
                }

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
                            _logger?.LogWarning($"Server stop failed: {ex.Message}");
                        }
                    });
                    tasks.Add(serverTask);
                }

                if (tasks.Any())
                {
                    await Task.WhenAll(tasks);
                }
                await Task.Delay(1000);

                try
                {
                    _processManager?.Dispose();
                    _serviceMonitor?.StopCapture();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"ProcessManager dispose failed: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($" Error during cleanup: {ex.Message}");
                _logger?.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        private void BtnFlushNetwork_Click(object sender, RoutedEventArgs e)
        {
            _testkitManagerService.FlushNetworkQueue();
        }
    }
}