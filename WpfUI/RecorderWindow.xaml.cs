using Common.Helper.Kernel32API;
using Common.Logging;
using Common.Models.Entities;
using Common.Resources;
using Middleware.Services;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using ProcessManagement.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using LicenseContext = OfficeOpenXml.LicenseContext;

namespace WpfUI
{
    #region
    public class ExcelExporter
    {
        public ExcelExporter()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }
        public void ExportToExcelParams(string filePath, params (string SheetName, ICollection<object> Data)[] sheetsData)
        {
            try
            {
                if (sheetsData == null || sheetsData.Length == 0)
                    throw new ArgumentException("Không có dữ liệu để xuất.");

                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                using var package = new ExcelPackage();

                foreach (var (sheetName, data) in sheetsData)
                {
                    if (data == null || !data.Any()) continue;

                    var firstItem = data.FirstOrDefault(d => d != null);
                    if (firstItem == null) continue;

                    var worksheet = package.Workbook.Worksheets.Add(sheetName);

                    var properties = firstItem.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

                    // ===== HEADER =====
                    for (int i = 0; i < properties.Length; i++)
                    {
                        worksheet.Cells[1, i + 1].Value = properties[i].Name;
                    }

                    using (var headerRange = worksheet.Cells[1, 1, 1, properties.Length])
                    {
                        headerRange.Style.Font.Bold = true;
                    }


                    // Thay thế LoadFromCollection bằng vòng lặp thủ công để đảm bảo hoạt động với ICollection<object>
                    if (data.Any())
                    {
                        int currentRow = 2;
                        foreach (var item in data)
                        {
                            if (item == null) continue;
                            for (int i = 0; i < properties.Length; i++)
                            {
                                var value = properties[i].GetValue(item);
                                worksheet.Cells[currentRow, i + 1].Value = value;
                            }
                            currentRow++;
                        }
                    }

                    // ===== TÙY CHỈNH CỘT (Giữ nguyên) =====
                    const double MAX_COLUMN_WIDTH = 60;
                    const double MIN_COLUMN_WIDTH = 10;

                    for (int i = 1; i <= properties.Length; i++)
                    {
                        var column = worksheet.Column(i);
                        var propertyName = properties[i - 1].Name;
                        column.Style.WrapText = true;
                        if ((propertyName.Equals("DataResponse", StringComparison.OrdinalIgnoreCase))
                            || (propertyName.Equals("Output", StringComparison.OrdinalIgnoreCase)))
                        {
                            column.Style.WrapText = true;
                            column.Width = MAX_COLUMN_WIDTH;
                        }
                        else if ((propertyName.Equals("DataTypeMiddleWare", StringComparison.OrdinalIgnoreCase)) ||
                            (propertyName.Equals("DataRequest", StringComparison.OrdinalIgnoreCase)))
                        {
                            column.Style.WrapText = true;
                            column.Width = MIN_COLUMN_WIDTH * 2;
                        }
                        else
                        {
                            column.AutoFit();
                        }

                        if (column.Width > MAX_COLUMN_WIDTH) column.Width = MAX_COLUMN_WIDTH;
                        if (column.Width < MIN_COLUMN_WIDTH) column.Width = MIN_COLUMN_WIDTH;
                    }

                    if (worksheet.Dimension != null)
                    {
                        worksheet.Cells[worksheet.Dimension.Address].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
                    }
                }

                package.SaveAs(new FileInfo(filePath));

                MessageBox.Show(
                    $"Xuất file Excel thành công!\nĐường dẫn: {filePath}",
                    "Thành công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                // ... (phần xử lý lỗi giữ nguyên)
                try
                {
                    string logPath = Path.Combine(Path.GetDirectoryName(filePath) ?? AppDomain.CurrentDomain.BaseDirectory, "ExportLog.txt");
                    File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Lỗi khi export Excel:\n{ex}\n\n");
                    MessageBox.Show($"Xuất Excel thất bại!\nChi tiết lỗi đã được ghi tại:\n{logPath}", "Lỗi Xuất File", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch
                {
                    MessageBox.Show($"Xuất Excel thất bại: {ex.Message}", "Lỗi Xuất File", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


    }
    #endregion


    public partial class RecorderWindow : Window, INotifyPropertyChanged
    {
        #region Fields

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

        // store NetwoekTransaction when middleware raised event
        private readonly Queue<NetworkTransaction> _pendingTransactions = new();
        private readonly object _transactionLock = new object();

        #endregion

        #region Properties
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

                    // ✅ Update SelectedStageData khi user chọn stage khác
                    if (TestStages.TryGetValue(value, out var stage))
                    {
                        SelectedStageData = stage;
                        LogManager.Instance.LogDebug($"📍 Switched to Stage {value}");
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

        #region Constructor

        public RecorderWindow(string testCaseName, string clientPath, string serverPath)
        {
            InitializeComponent();
            _processManager = new ProcessManager();
            _testCaseName = testCaseName;
            _clientPath = clientPath;
            _serverPath = serverPath;

            DataContext = this;
            TestStages = new Dictionary<int, TestStage>();
            // Create TestStage
            CreateInitialStage();
            // Subscribe to data sources
            SubscribeToDataSources();
            LogManager.Instance.LogInfomation($"📝 Recorder window opened: {testCaseName}");
            this.Loaded += RecorderWindow_Loaded;
        }

        private async void RecorderWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(500);
            try
            {
                await StartProcessesAsync();
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
            _currentStageIndex = 1;
            var testStage = new TestStage();

            _currentStageIndex = 1; // Start from stage 1

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

            LogManager.Instance.LogInfomation($"✅ Initial stage created: Stage {_currentStageIndex} - {ActionKeywords.START}");
        }

        #endregion

        #region Subscribe to Data Sources

        private void SubscribeToDataSources()
        {
            _processManager.OnUserInput += OnUserInput;
            MiddlewareStart.Instance.OnTransactionCompleted += OnTransactionCompletedHandler;
            _processManager.OnClientOutput += OnClientOutput;
            _processManager.OnServerOutput += OnServerOutput;
            LogManager.Instance.LogDebug("✅ Subscribed to data sources with sequential processing");
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
            LogManager.Instance.LogDebug("❌ Unsubscribed from data sources");
        }

        #endregion

        #region Event Handlers - ProcessManager

        /// <summary>
        /// ✅ FIXED: Handle user input and create NEW STAGE
        /// </summary>
        private void OnUserInput(string input, string dataType)
        {
            LogManager.Instance.LogDebug($"Data input{input}");
            Dispatcher.Invoke(() =>
            {
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
                FlushPendingTransactions();
                LogManager.Instance.LogInfomation($"📥 Stage {_currentStageIndex} - Input: {input}");
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
                try
                {
                    var currentStage = CurrentTestStage;
                    var outputClient = currentStage.OutputClients
                    .Where(o => o.Stage == _currentStageIndex)
                    .FirstOrDefault();

                    if (outputClient != null)
                    {
                        outputClient.Output = output;
                    }
                    else
                    {
                        outputClient = new OutputClient
                        {
                            Stage = _currentStageIndex,
                            Output = output
                        };
                        currentStage.OutputClients.Add(outputClient);
                    }
                    LogManager.Instance.LogDebug($"📤 Client output added to Stage {_currentStageIndex}: {output}");
                    OnPropertyChanged(nameof(SelectedStageData));
                }
                catch (Exception ex)
                {
                    LogManager.Instance.LogError($"❌ OnClientOutput error: {ex.Message}");
                }

            });
        }

        /// <summary>
        /// ✅ FIXED: Append to existing OutputServer or create new one for current stage
        /// All output in same stage will be combined
        /// </summary>
        private void OnServerOutput(string output)
        {
            LogManager.Instance.LogDebug($"Data output server {output}");

            Dispatcher.Invoke(() =>
            {
                try
                {
                    var currentStage = CurrentTestStage;
                    var outputServer = currentStage.OutputServers.Where(o => o.Stage == _currentStageIndex).FirstOrDefault();
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
                }
                catch (Exception ex)
                {
                }

                LogManager.Instance.LogDebug($"📤 Server output added to Stage {_currentStageIndex}: {output}");
                OnPropertyChanged(nameof(SelectedStageData));
            });
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
        /// ✅ FIXED v3.0: Create SEPARATE records for middleware data (không ghi đè console output)
        /// </summary>
        private void OnMiddlewareTransaction(NetworkTransaction transaction)
        {
            Dispatcher.Invoke(() =>
            {
                // ✅ V3.0: Tạo RECORD MỚI riêng cho middleware (KHÔNG tìm existing console output)
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

                // ✅ V3.0: Tạo RECORD MỚI riêng cho middleware (KHÔNG tìm existing console output)
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
                LogManager.Instance.LogDebug("🗑️ Input client row deleted");
            }
        }

        private void BtnDeleteOutputClient_Click(object sender, RoutedEventArgs e)
        {
            if (dgOutputClients.SelectedItem is OutputClient selectedItem)
            {
                CurrentTestStage.OutputClients.Remove(selectedItem);
                LogManager.Instance.LogDebug("🗑️ Output client row deleted");
            }
        }

        private void BtnDeleteOutputServer_Click(object sender, RoutedEventArgs e)
        {
            if (dgOutputServers.SelectedItem is OutputServer selectedItem)
            {
                CurrentTestStage.OutputServers.Remove(selectedItem);
                LogManager.Instance.LogDebug("🗑️ Output server row deleted");
            }
        }

        #endregion

        #region Process Management

        /// <summary>
        /// Set process info (called from MainWindow after starting processes)
        /// </summary>
        /// <param name="clientChild">Client process handle</param>
        /// <param name="clientMutex">Client mutex</param>
        /// <param name="clientCts">Client cancellation token source</param>
        /// <param name="serverChild">Server process handle</param>
        /// <param name="serverMutex">Server mutex</param>
        /// <param name="serverCts">Server cancellation token source</param>
        /// <param name="processManager">Process manager instance</param>
        public void SetProcessInfo(
            ChildProcess clientChild, IntPtr clientMutex, CancellationTokenSource clientCts,
            ChildProcess serverChild, IntPtr serverMutex, CancellationTokenSource serverCts,
            ProcessManager processManager)
        {
            _clientChild = clientChild;
            _clientMutex = clientMutex;
            _clientCts = clientCts;

            _serverChild = serverChild;
            _serverMutex = serverMutex;
            _serverCts = serverCts;

            _processManager = processManager;

            LogManager.Instance.LogDebug($"✅ Process info set - Client PID: {clientChild.processId}, Server PID: {serverChild.processId}");
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
        /// Stop all processes and cleanup resources
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
                LogManager.Instance.LogInfomation("🛑 Closing RecorderWindow - stopping all processes...");

                // Unsubscribe from events
                UnsubscribeFromDataSources();

                // Stop middleware
                if (MiddlewareStart.Instance.IsRunning)
                {
                    LogManager.Instance.LogDebug("Stopping middleware...");
                    await MiddlewareStart.Instance.StopAsync();
                }

                //  Stop client process (with CancellationTokenSource)
                if (_processManager != null && _clientChild.hProcess != IntPtr.Zero && _clientCts != null)
                {
                    LogManager.Instance.LogDebug("Stopping client process...");
                    await _processManager.StopSingleAsync(_clientChild, _clientMutex, _clientCts, "Client");
                }

                //  Stop server process (with CancellationTokenSource)
                if (_processManager != null && _serverChild.hProcess != IntPtr.Zero && _serverCts != null)
                {
                    LogManager.Instance.LogDebug("Stopping server process...");
                    await _processManager.StopSingleAsync(_serverChild, _serverMutex, _serverCts, "Server");
                }

                // Dispose ProcessManager
                _processManager?.Dispose();

                LogManager.Instance.LogInfomation("⏹️ Recording stopped - all processes terminated");
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"❌ Error closing recorder: {ex.Message}");
                LogManager.Instance.LogError($"Stack trace: {ex.StackTrace}");

                MessageBox.Show(
                    $"Error stopping processes:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            base.OnClosing(e);
        }

        private async Task CloseClientAsync()
        {
            if (_processManager != null && _clientChild.hProcess != IntPtr.Zero && _clientCts != null)
            {
                LogManager.Instance.LogDebug("Stopping client process...");
                await _processManager.StopSingleAsync(_clientChild, _clientMutex, _clientCts, "Client");
            }
        }
        private async Task StopMiddlewareAsync()
        {
            if (MiddlewareStart.Instance.IsRunning)
            {
                LogManager.Instance.LogDebug("Stopping middleware...");
                await MiddlewareStart.Instance.StopAsync();
            }
        }

        private async Task CloseServerAsync()
        {
            if (_processManager != null && _serverChild.hProcess != IntPtr.Zero && _serverCts != null)
            {
                LogManager.Instance.LogDebug("Stopping server process...");
                await _processManager.StopSingleAsync(_serverChild, _serverMutex, _serverCts, "Server");
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

                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    Title = "Save Excel File",
                    FileName = $"{_testCaseName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var exporter = new ExcelExporter();

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

                    // Gọi ExportToExcelParams với 4 sheets
                    exporter.ExportToExcelParams(
                        saveFileDialog.FileName,
                        sheetsList.ToArray()
                    );

                    LogManager.Instance.LogInfomation($"📊 Exported test data to: {saveFileDialog.FileName}");
                }
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
                LogManager.Instance.LogInfomation("🔴 Stopping Client process...");
                await CloseClientAsync();
                await StopMiddlewareAsync();

                LogManager.Instance.LogInfomation("✅ Client process stopped successfully");

                MessageBox.Show(
                    "Client process stopped successfully.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"❌ Error stopping client: {ex.Message}");
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
                await CloseServerAsync();
                await StopMiddlewareAsync();
                MessageBox.Show(
                    "Server process stopped successfully.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"❌ Error stopping server: {ex.Message}");
                MessageBox.Show(
                    $"Error stopping server process:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnDeleteOutputDB_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}