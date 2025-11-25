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
using System.Text.Json;
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

        // ✅ THÊM: Fields để lưu port thực tế
        private int _actualServerPort = 0;
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

        /// <summary>
        /// ✅ SỬA: InitializeAsync() - KHÔNG CẦN GỌI AppSettingsManager.UpdateAppSettings
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                // ✅ Lưu folder của server exe để sau này đọc port file
                _serverExecutableDir = Path.GetDirectoryName(_serverPath);
                LogManager.Instance?.LogInfomation($"📂 Server directory: {_serverExecutableDir}");

                // ✅ KHÔNG CẦN allocate port nữa, server sẽ tự dùng port từ appsettings
                // var (proxyPort, serverPort) = PortChecker.GetTwoAvailablePorts(8000, 9000);
                // if (!AppSettingsManager.UpdateAppSettings(_clientPath, proxyPort, _serverPath, serverPort))
                // {...}

                await Task.Delay(500);
                LogManager.Instance?.LogInfomation("✅ RecorderWindow initialized - Ready to start processes");
            }
            catch (Exception ex)
            {
                LogManager.Instance?.LogError($"❌ Error initializing RecorderWindow: {ex.Message}");
                throw;
            }
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

        #region Helper Methods - Port Reading

        // ✅ SỬA: Đọc port từ appsettings.json của server với retry logic
        private async Task<int> ReadServerActualPortAsync()
        {
            var appSettingsFile = Path.Combine(_serverExecutableDir, "appsettings.json");

            LogManager.Instance?.LogInfomation($"📂 Looking for appsettings.json: {appSettingsFile}");

            // Chờ file tồn tại - tối đa 50 lần x 100ms = 5 giây
            int fileRetries = 0;
            while (!File.Exists(appSettingsFile) && fileRetries < 50)
            {
                await Task.Delay(100);
                fileRetries++;
            }

            if (!File.Exists(appSettingsFile))
            {
                LogManager.Instance?.LogError($"❌ Could not find appsettings.json: {appSettingsFile}");
                throw new FileNotFoundException($"appsettings.json not found: {appSettingsFile}");
            }

            LogManager.Instance?.LogInfomation($"✅ Found appsettings.json, now reading port...");

            try
            {
                // Retry logic - đôi khi file vừa được ghi, chưa kịp flush
                int parseRetries = 0;
                int port = -1;

                while (parseRetries < 5)
                {
                    try
                    {
                        // Thêm delay nhỏ trước khi đọc để file flush dữ liệu
                        await Task.Delay(200);

                        // Đọc file JSON với FileShare.Read để cho phép file bị lock
                        string jsonContent;
                        using (var fileStream = new FileStream(appSettingsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var reader = new StreamReader(fileStream))
                        {
                            jsonContent = await reader.ReadToEndAsync();
                        }

                        LogManager.Instance?.LogDebug($"📄 appsettings.json content:\n{jsonContent}");

                        // Parse JSON để lấy Port
                        using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                        {
                            JsonElement root = doc.RootElement;

                            if (root.TryGetProperty("Port", out JsonElement portElement))
                            {
                                string portText = portElement.GetString();

                                if (int.TryParse(portText, out port))
                                {
                                    if (port > 0 && port < 65536)
                                    {
                                        LogManager.Instance?.LogInfomation($"✅ Server port read from appsettings.json: {port}");
                                        return port;
                                    }
                                    else
                                    {
                                        LogManager.Instance?.LogWarning($"⚠️ Invalid port range: {port}, retrying...");
                                        parseRetries++;
                                        continue;
                                    }
                                }
                                else
                                {
                                    LogManager.Instance?.LogWarning($"⚠️ Could not parse port: {portText}, retrying...");
                                    parseRetries++;
                                    continue;
                                }
                            }
                            else
                            {
                                LogManager.Instance?.LogWarning($"⚠️ 'Port' field not found, retrying...");
                                parseRetries++;
                                continue;
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        LogManager.Instance?.LogWarning($"⚠️ Error parsing JSON (attempt {parseRetries + 1}/5): {ex.Message}");
                        parseRetries++;
                        await Task.Delay(300);
                        continue;
                    }
                    catch (IOException ex)
                    {
                        LogManager.Instance?.LogWarning($"⚠️ File is locked or being written (attempt {parseRetries + 1}/5): {ex.Message}");
                        parseRetries++;
                        await Task.Delay(300);
                        continue;
                    }
                }

                // Nếu vẫn không đọc được sau 5 lần retry
                if (port <= 0 || port >= 65536)
                {
                    LogManager.Instance?.LogError($"❌ Failed to read valid port after {parseRetries} attempts");
                    throw new InvalidOperationException($"Could not read valid port from appsettings.json after {parseRetries} retry attempts");
                }

                return port;
            }
            catch (Exception ex)
            {
                LogManager.Instance?.LogError($"❌ Error reading port from appsettings.json: {ex.Message}");
                LogManager.Instance?.LogError($"Stack trace: {ex.StackTrace}");
                throw;
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
            e.Cancel = true;
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
                MessageBox.Show("Client is already running.", "Info", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(_clientPath))
            {
                throw new FileNotFoundException($"Client executable not found: {_clientPath}");
            }

            // ✅ Kiểm tra server port
            if (_actualServerPort == 0)
            {
                MessageBox.Show("❌ Server port not available.\n\nPlease start the Server first!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
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

                // ❌ XÓA: Không khởi động middleware
                // if (!_isMiddlewareRunning && _isServerRunning)
                // {
                //     LogManager.Instance.LogInfomation("Starting middleware...");
                //     await MiddlewareStart.Instance.StartAsync(_proxyPort, _serverPort, _isHttp);
                //     _isMiddlewareRunning = true;
                // }

                BtnStartClient.IsEnabled = false;
                _testkitManagerService.CreateInitialStage(ActionKeywords.START_CLIENT);

                // ✅ Client kết nối trực tiếp tới server (KHÔNG QUA MIDDLEWARE)
                LogManager.Instance?.LogInfomation($"🚀 Starting client - connecting directly to server on port {_actualServerPort}");

                var clientResult = await _processManager.StartSingleWithPollingAsync(
                    _clientPath,
                    "Client",
                    isClient: true,
                    showConsoleMessages: false
                );

                _clientChild = clientResult.child;
                _clientMutex = clientResult.mutex;
                _clientCts = clientResult.cts;

                _isClientRunning = true;
                UpdateProcessButtonStates();

                LogManager.Instance?.LogInfomation($"✅ Client started - connected directly to server on port {_actualServerPort}");
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"❌ Error starting client: {ex.Message}");
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

                // ❌ XÓA: Không khởi động middleware
                // if (!_isMiddlewareRunning && _isClientRunning)
                // {
                //     LogManager.Instance.LogInfomation("Starting middleware...");
                //     await MiddlewareStart.Instance.StartAsync(_proxyPort, _serverPort, _isHttp);
                //     _isMiddlewareRunning = true;
                // }

                _testkitManagerService.CreateInitialStage(ActionKeywords.START_SERVER);

                BtnStartServer.IsEnabled = false;

                // ✅ Khởi động server
                LogManager.Instance?.LogInfomation("🚀 Starting server process...");
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

                // ✅ Chờ server khởi động hoàn toàn
                LogManager.Instance?.LogInfomation("⏳ Waiting for server to fully initialize...");
                await Task.Delay(2000);

                // ✅ Đọc port thực tế từ appsettings.json
                try
                {
                    _actualServerPort = await ReadServerActualPortAsync();
                    LogManager.Instance?.LogInfomation($"🔌 Server running on actual port: {_actualServerPort}");
                }
                catch (Exception ex)
                {
                    LogManager.Instance?.LogError($"❌ Failed to read server port: {ex.Message}");
                    MessageBox.Show($"Warning: Could not read server port.\n\n{ex.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                UpdateProcessButtonStates();
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"❌ Error starting server: {ex.Message}");
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
                LogManager.Instance?.LogInfomation("🛑 Cleaning up RecorderWindow resources...");

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