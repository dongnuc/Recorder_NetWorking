
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
using Common.Interfaces.IOFile; 
using System.IO;                
using WpfUI.ViewModels;      

namespace WpfUI
{
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

        private readonly IOFolderHandler _folderHandler;
        private readonly IOFileHandler _fileHandler;
        private readonly string _testCaseName;

        private readonly string _baseTestCasesPath = @"D:\MyTestCases";

        private string _currentTestCasePath; 

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

        private ObservableCollection<FileSystemItemViewModel> _rootItems = new ObservableCollection<FileSystemItemViewModel>();
        public ObservableCollection<FileSystemItemViewModel> RootItems
        {
            get => _rootItems;
            set
            {
                _rootItems = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Constructor

        public RecorderWindow(
            string testCaseName,
            ProcessManager processManager,
            IOFolderHandler folderHandler,   
            IOFileHandler fileHandler)    
        {
            InitializeComponent();

            _testCaseName = testCaseName;       
            _processManager = processManager;
            _folderHandler = folderHandler;   
            _fileHandler = fileHandler;      

            CurrentTestStage = new TestStage();

            DataContext = this;
            CreateInitialStage();

            SubscribeToDataSources();

            InitializeAndLoadTree(); 

            LogManager.Instance.LogInfomation($"📝 Recorder window opened: {testCaseName}");
        }

        #endregion

        #region Initial Stage Creation

        private void CreateInitialStage()
        {
            _currentStageIndex = 1;

            var initialInput = new InputClient
            {
                Stage = _currentStageIndex,
                Action = ActionKeywords.START,
                Input = string.Empty,
                DataType = "System"
            };

            CurrentTestStage.InputClients.Add(initialInput);

            StageKeys.Add(_currentStageIndex);
            SelectedStageKey = _currentStageIndex;

            LogManager.Instance.LogInfomation($"✅ Initial stage created: Stage {_currentStageIndex} - {ActionKeywords.START}");
        }

        #endregion

        #region Subscribe to Data Sources

        private void SubscribeToDataSources()
        {
            MiddlewareStart.Instance.OnTransactionCompleted += OnMiddlewareTransaction;

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

        private void OnUserInput(string input, string dataType)
        {
            Dispatcher.Invoke(() =>
            {
                _currentStageIndex++;

                var inputClient = new InputClient
                {
                    Stage = _currentStageIndex,
                    Action = ActionKeywords.INPUT,
                    Input = input,
                    DataType = dataType
                };

                CurrentTestStage.InputClients.Add(inputClient);

                if (!StageKeys.Contains(_currentStageIndex))
                {
                    StageKeys.Add(_currentStageIndex);
                    SelectedStageKey = _currentStageIndex;
                }

                LogManager.Instance.LogInfomation($"📥 Stage {_currentStageIndex} - Input: {input}");
                OnPropertyChanged(nameof(SelectedStageData));
            });
        }

        private void OnClientOutput(string output)
        {
            Dispatcher.Invoke(() =>
            {
                var outputClient = CurrentTestStage.OutputClients
                    .Where(o => o.Stage == _currentStageIndex)
                    .FirstOrDefault(o => string.IsNullOrEmpty(o.Method));

                if (outputClient == null)
                {
                    outputClient = new OutputClient
                    {
                        Stage = _currentStageIndex,
                        Output = output.Trim()
                    };
                    CurrentTestStage.OutputClients.Add(outputClient);
                }
                else
                {
                    outputClient.Output += Environment.NewLine + output.Trim();
                }

                LogManager.Instance.LogDebug($"📤 Client output added to Stage {_currentStageIndex}: {output}");
                OnPropertyChanged(nameof(SelectedStageData));
            });
        }
        private void OnServerOutput(string output)
        {
            Dispatcher.Invoke(() =>
            {
                var outputServer = CurrentTestStage.OutputServers
                    .Where(o => o.Stage == _currentStageIndex)
                    .FirstOrDefault(o => string.IsNullOrEmpty(o.Method));

                if (outputServer == null)
                {
                    outputServer = new OutputServer
                    {
                        Stage = _currentStageIndex,
                        Output = output.Trim()
                    };
                    CurrentTestStage.OutputServers.Add(outputServer);
                }
                else
                {
                    outputServer.Output += Environment.NewLine + output.Trim();
                }

                LogManager.Instance.LogDebug($"📤 Server output added to Stage {_currentStageIndex}: {output}");
                OnPropertyChanged(nameof(SelectedStageData));
            });
        }

        #endregion

        #region Event Handlers - Middleware

        private void OnMiddlewareTransaction(NetworkTransaction transaction)
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
                    Output = null
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
                    Output = null 
                };
                CurrentTestStage.OutputClients.Add(outputClient);

                OnPropertyChanged(nameof(SelectedStageData));

                LogManager.Instance.LogDebug($"🌐 Stage {_currentStageIndex} - Middleware transaction recorded: {transaction.Request.Method} - {transaction.Response.StatusCode}");
            });
        }

        #endregion

        #region Process Management

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

        private void dgOutputClients_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
        }

        #endregion


        #region File/Folder Tree Logic

        private void InitializeAndLoadTree()
        {
            try
            {
                _currentTestCasePath = _folderHandler.CreateDirectory(
                    _baseTestCasesPath,
                    _testCaseName
                );

                LoadFileTree();
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Failed to initialize test case directory: {ex.Message}");
                MessageBox.Show($"Failed to initialize test case directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadFileTree()
        {
            RootItems.Clear();

            try
            {
                var rootDirectoryInfo = new DirectoryInfo(_baseTestCasesPath);
                var rootNode = new FileSystemItemViewModel
                {
                    Name = rootDirectoryInfo.Name,
                    FullPath = rootDirectoryInfo.FullName,
                    IsFolder = true
                };

                LoadSubFoldersAndFiles(rootNode);

                RootItems.Add(rootNode);
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Error loading file tree root: {ex.Message}");
            }
        }

        private void LoadSubFoldersAndFiles(FileSystemItemViewModel parentNode)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(parentNode.FullPath))
                {
                    var dirInfo = new DirectoryInfo(dir);
                    var childFolder = new FileSystemItemViewModel
                    {
                        Name = dirInfo.Name,
                        FullPath = dirInfo.FullName,
                        IsFolder = true
                    };

                    LoadSubFoldersAndFiles(childFolder);
                    parentNode.Children.Add(childFolder);
                }

                foreach (var file in Directory.GetFiles(parentNode.FullPath))
                {
                    var fileInfo = new FileInfo(file);

                    if (fileInfo.Extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                        fileInfo.Extension.Equals(".xls", StringComparison.OrdinalIgnoreCase))
                    {
                        var childFile = new FileSystemItemViewModel
                        {
                            Name = fileInfo.Name,
                            FullPath = fileInfo.FullName,
                            IsFolder = false
                        };
                        parentNode.Children.Add(childFile);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Bỏ qua các thư mục không có quyền truy cập
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogWarning($"Could not access path: {parentNode.FullPath}. Error: {ex.Message}");
            }
        }

        private void BtnRefreshTree_Click(object sender, RoutedEventArgs e)
        {
            LogManager.Instance.LogDebug("Refreshing file tree...");
            LoadFileTree();
        }

        private void FileTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (FileTree.SelectedItem is FileSystemItemViewModel selectedItem && !selectedItem.IsFolder)
            {
                LogManager.Instance.LogInfomation($"File selected: {selectedItem.FullPath}");
                LoadDataFromFile(selectedItem.FullPath);
            }
        }

        
        private async void BtnSaveCurrent_Click(object sender, RoutedEventArgs e)
        {
            if (!(FileTree.SelectedItem is FileSystemItemViewModel selectedItem) || selectedItem.IsFolder)
            {
                MessageBox.Show("Please select a file from the tree to save to.", "No File Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string targetPath = selectedItem.FullPath;

            var confirm = MessageBox.Show($"This will overwrite the content of:\n{targetPath}\n\nAre you sure?", "Confirm Save", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm == MessageBoxResult.No)
            {
                return;
            }

            try
            {
                var excelConfig = new ConfigModel
                {
                    SaveLocation = targetPath,

                };

                _fileHandler.ConfigForWritingFile(excelConfig, "Excel");


                LogManager.Instance.LogDebug($"Saving {CurrentTestStage.InputClients.Count} input rows to {targetPath}");
                var inputBindingList = new BindingList<InputClient>(CurrentTestStage.InputClients.ToList());
                await _fileHandler.WriteFileDataAsync(inputBindingList, 2, 1); 
                LogManager.Instance.LogDebug($"Saving {CurrentTestStage.OutputClients.Count} output client rows to {targetPath}");
                var outputClientBindingList = new BindingList<OutputClient>(CurrentTestStage.OutputClients.ToList());
                await _fileHandler.WriteFileDataAsync(outputClientBindingList, 2, 1); 

                LogManager.Instance.LogDebug($"Saving {CurrentTestStage.OutputServers.Count} output server rows to {targetPath}");
                var outputServerBindingList = new BindingList<OutputServer>(CurrentTestStage.OutputServers.ToList());
                await _fileHandler.WriteFileDataAsync(outputServerBindingList, 2, 1);

                MessageBox.Show("Data saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                LogManager.Instance.LogInfomation($"Data saved to {targetPath}");
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Failed to save file: {ex.Message}");
                MessageBox.Show($"Failed to save file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadDataFromFile(string filePath)
        {
            try
            {
                CurrentTestStage.InputClients.Clear();
                CurrentTestStage.OutputClients.Clear();
                CurrentTestStage.OutputServers.Clear();
                StageKeys.Clear();

                var inputs = _fileHandler.ReadFileData<InputClient>(filePath, "InputClients", 2, 1);
                foreach (var item in inputs)
                {
                    CurrentTestStage.InputClients.Add(item); 
                }

                var outputsClient = _fileHandler.ReadFileData<OutputClient>(filePath, "OutputClients", 2, 1);
                foreach (var item in outputsClient)
                {
                    CurrentTestStage.OutputClients.Add(item);
                }

                var outputsServer = _fileHandler.ReadFileData<OutputServer>(filePath, "OutputServers", 2, 1);
                foreach (var item in outputsServer)
                {
                    CurrentTestStage.OutputServers.Add(item);
                }

                var allStages = inputs.Select(i => i.Stage)
                                      .Distinct()
                                      .OrderBy(s => s);
                foreach (var stage in allStages)
                {
                    StageKeys.Add(stage);
                }

                if (StageKeys.Any())
                {
                    SelectedStageKey = StageKeys.First();
                }

                LogManager.Instance.LogInfomation($"Successfully loaded {inputs.Count} inputs from {filePath}");
                OnPropertyChanged(nameof(SelectedStageData)); 
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Failed to load file: {ex.Message}");
                MessageBox.Show($"Failed to load file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                CreateInitialStage();
            }
        }

        #endregion

        #region Window Lifecycle

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
    }
}