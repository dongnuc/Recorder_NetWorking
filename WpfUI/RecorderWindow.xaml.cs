// UITestKit/RecorderWindow.xaml.cs
using Common.Helper.Kernel32API;
using Common.Logging;
using Common.Models.Entities;
using Common.Resources;
using Middleware.Services;
using ProcessManagement.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace WpfUI
{
    public partial class RecorderWindow : Window, INotifyPropertyChanged
    {
        #region Fields

        private ProcessManager _processManager;

        // ✅ Changed from Task<string> to CancellationTokenSource
        private ChildProcess _clientChild;
        private IntPtr _clientMutex;
        private CancellationTokenSource _clientCts;

        private ChildProcess _serverChild;
        private IntPtr _serverMutex;
        private CancellationTokenSource _serverCts;

        private int _currentStageIndex = 0;

        #endregion

        #region Properties

        public TestStage CurrentTestStage { get; private set; }

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
                _selectedStageKey = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedStageData));
            }
        }

        public TestStage SelectedStageData
        {
            get => CurrentTestStage;
        }

        #endregion

        #region Constructor

        public RecorderWindow(string testCaseName, ProcessManager processManager)
        {
            InitializeComponent();

            _processManager = processManager;

            // Create TestStage
            CurrentTestStage = new TestStage();
            

            DataContext = this;
            CreateInitialStage();

            // Subscribe to data sources
            SubscribeToDataSources();

            LogManager.Instance.LogInfomation($"📝 Recorder window opened: {testCaseName}");
        }

        #endregion

        #region Initial Stage Creation

        /// <summary>
        /// Create Stage 1 with "START" action on startup
        /// This captures initial connection data before user input
        /// </summary>
        private void CreateInitialStage()
        {
            _currentStageIndex = 1; // Start from stage 1

            var initialInput = new InputClient
            {
                Stage = _currentStageIndex,
                Action = ActionKeywords.START,
                Input = string.Empty,
                DataType = "System"
            };

            CurrentTestStage.InputClients.Add(initialInput);

            // Add to stage keys
            StageKeys.Add(_currentStageIndex);
            SelectedStageKey = _currentStageIndex;

            LogManager.Instance.LogInfomation($"✅ Initial stage created: Stage {_currentStageIndex} - {ActionKeywords.START}");
        }

        #endregion

        #region Subscribe to Data Sources

        private void SubscribeToDataSources()
        {
            // Subscribe to Middleware
            MiddlewareStart.Instance.OnTransactionCompleted += OnMiddlewareTransaction;

            // Subscribe to ProcessManager
            _processManager.OnUserInput += OnUserInput;
            _processManager.OnClientOutput += OnClientOutput;
            _processManager.OnServerOutput += OnServerOutput;

            LogManager.Instance.LogDebug("✅ Subscribed to data sources");
        }

        private void UnsubscribeFromDataSources()
        {
            MiddlewareStart.Instance.OnTransactionCompleted -= OnMiddlewareTransaction;

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
            Dispatcher.Invoke(() =>
            {
                // ✅ Increment stage ONLY when user inputs (Enter key pressed)
                _currentStageIndex++;

                var inputClient = new InputClient
                {
                    Stage = _currentStageIndex,
                    Action = ActionKeywords.INPUT,
                    Input = input,
                    DataType = dataType
                };

                CurrentTestStage.InputClients.Add(inputClient);

                // Add stage key
                if (!StageKeys.Contains(_currentStageIndex))
                {
                    StageKeys.Add(_currentStageIndex);
                    SelectedStageKey = _currentStageIndex;
                }

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
            Dispatcher.Invoke(() =>
            {
                // ✅ Find existing OutputClient for current stage (without Method - pure console output)
                var outputClient = CurrentTestStage.OutputClients
                    .Where(o => o.Stage == _currentStageIndex)
                    .FirstOrDefault(o => string.IsNullOrEmpty(o.Method));

                if (outputClient == null)
                {
                    // Create new record for this stage
                    outputClient = new OutputClient
                    {
                        Stage = _currentStageIndex,
                        Output = output.Trim()
                    };
                    CurrentTestStage.OutputClients.Add(outputClient);
                }
                else
                {
                    // Append to existing output
                    outputClient.Output += Environment.NewLine + output.Trim();
                }

                LogManager.Instance.LogDebug($"📤 Client output added to Stage {_currentStageIndex}: {output}");
                OnPropertyChanged(nameof(SelectedStageData));
            });
        }

        /// <summary>
        /// ✅ FIXED: Append to existing OutputServer or create new one for current stage
        /// All output in same stage will be combined
        /// </summary>
        private void OnServerOutput(string output)
        {
            Dispatcher.Invoke(() =>
            {
                // ✅ Find existing OutputServer for current stage (without Method - pure console output)
                var outputServer = CurrentTestStage.OutputServers
                    .Where(o => o.Stage == _currentStageIndex)
                    .FirstOrDefault(o => string.IsNullOrEmpty(o.Method));

                if (outputServer == null)
                {
                    // Create new record for this stage
                    outputServer = new OutputServer
                    {
                        Stage = _currentStageIndex,
                        Output = output.Trim()
                    };
                    CurrentTestStage.OutputServers.Add(outputServer);
                }
                else
                {
                    // Append to existing output
                    outputServer.Output += Environment.NewLine + output.Trim();
                }

                LogManager.Instance.LogDebug($"📤 Server output added to Stage {_currentStageIndex}: {output}");
                OnPropertyChanged(nameof(SelectedStageData));
            });
        }

        #endregion

        #region Event Handlers - Middleware

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

                // ✅ Stop client process (with CancellationTokenSource)
                if (_processManager != null && _clientChild.hProcess != IntPtr.Zero && _clientCts != null)
                {
                    LogManager.Instance.LogDebug("Stopping client process...");
                    await _processManager.StopSingleAsync(_clientChild, _clientMutex, _clientCts, "Client");
                }

                // ✅ Stop server process (with CancellationTokenSource)
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

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        private void dgOutputClients_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}