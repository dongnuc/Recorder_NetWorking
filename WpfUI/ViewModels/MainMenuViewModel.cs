using Common.Interfaces.IOFile;
using Common.Interfaces.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System;
using WpfUI.Properties;
using System.Threading.Tasks;

namespace WpfUI.ViewModels
{
    public class MainMenuViewModel : INotifyPropertyChanged
    {
        #region Fields
        
        private readonly IOFolderHandler _folderHandler;
        private readonly IOFileHandler _fileHandler;
        private readonly ISystemLogger _logger;
        private readonly string _projectPath;
        private readonly string _clientExePath;
        private readonly string _serverExePath;

        private readonly string _testCaseTemplateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                                "Resources", "Templates", "DotnetNetworking", "TestCase");
        private readonly string _questionTemplateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                                "Resources", "Templates", "DotnetNetworking", "Question");
        #endregion

        #region Properties
        
        public ObservableCollection<FileSystemItemViewModel> RootItems { get; set; }

        private FileSystemItemViewModel _selectedItem;
        public FileSystemItemViewModel SelectedItem
        {
            get => _selectedItem;
            set { _selectedItem = value; OnPropertyChanged(); }
        }
        
        #endregion

        public MainMenuViewModel(
            IOFolderHandler folderHandler,
            IOFileHandler fileHandler,
            string projectPath,
            ISystemLogger logger)
        {
            _folderHandler = folderHandler ?? throw new ArgumentNullException(nameof(folderHandler));
            _fileHandler = fileHandler ?? throw new ArgumentNullException(nameof(fileHandler));
            _projectPath = projectPath ?? throw new ArgumentNullException(nameof(projectPath));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _clientExePath = Settings.Default.ClientExePath;
            _serverExePath = Settings.Default.ServerExePath;

            RootItems = new ObservableCollection<FileSystemItemViewModel>();
            LoadFileTree();
        }

        #region File/Folder Tree Logic

        public void LoadFileTree()
        {
            RootItems.Clear();
            try
            {
                if (!Directory.Exists(_projectPath)) 
                { 
                    _folderHandler.CreateDirectory(_projectPath, ""); 
                }

                var rootDirectoryInfo = new DirectoryInfo(_projectPath);
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
                _logger.LogError($"Error loading file tree root: {ex.Message}"); 
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
            catch (UnauthorizedAccessException) { }
            catch (Exception ex) 
            { 
                _logger.LogWarning($"Could not access path: {parentNode.FullPath}. Error: {ex.Message}"); 
            }
        }

        public void CreateNewQuestion(string questionName, bool useDatabase)
        {
            if (string.IsNullOrWhiteSpace(questionName))
            {
                MessageBox.Show("Question name cannot be empty.", "Warning");
                return;
            }
            
            if (SelectedItem == null || !SelectedItem.FullPath.Equals(_projectPath, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Please select the root project folder to create a new Question.", "Warning");
                return;
            }

            try
            {
                string questionPath = _folderHandler.CreateDirectory(SelectedItem.FullPath, questionName);

                _folderHandler.CopyTemplateFromResource(questionPath, _questionTemplateDir, false,
                    "Header.xlsx", "Environment.xlsx");

                string chosenEnvRunFile = "EnvRunDB.xlsx";
                string srcSheetPath = Path.Combine(_questionTemplateDir, chosenEnvRunFile);
                string destEnvPath = Path.Combine(questionPath, "Environment.xlsx");

                if (!File.Exists(srcSheetPath))
                {
                    throw new Exception($"Template file {chosenEnvRunFile} not found in '.../Templates/DotnetNetworking/Question'. \nPlease check 'Copy to Output Directory'.");
                }

                _folderHandler.ReplaceSheetExcel(srcSheetPath, destEnvPath, "Run");

                string metaPath = _folderHandler.CreateDirectory(questionPath, "Meta");
                string givenPath = _folderHandler.CreateDirectory(metaPath, "Given");
                _folderHandler.CreateDirectory(givenPath, "Client");
                _folderHandler.CreateDirectory(givenPath, "Server");
                
                string headerPath = Path.Combine(questionPath, "Header.xlsx");
                string protocol = Settings.Default.Protocol;
                
                try
                {
                    _fileHandler.ModifyExcelCellContent(headerPath, "Config", 3, 2, protocol);
                    _logger.LogInfomation($"Set Protocol={protocol} for {questionName}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Could not set Protocol in Header.xlsx: {ex.Message}");
                }
                
                LoadFileTree();
                _logger.LogInfomation($"Created new question: {questionName}. UseDatabase={useDatabase}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create question: {ex.Message}");
                MessageBox.Show($"Failed to create question: {ex.Message}", "Error");
            }
        }

        public async Task<string> CreateNewTestCase(string testCaseName)
        {
            if (string.IsNullOrWhiteSpace(testCaseName)) 
            { 
                MessageBox.Show("Test case name cannot be empty.", "Warning"); 
                return string.Empty; 
            }
            
            if (SelectedItem == null || !SelectedItem.IsFolder)
            {
                MessageBox.Show("Please select a parent 'Question' folder first.", "Warning");
                return string.Empty;
            }
            
            if (SelectedItem.FullPath.Equals(_projectPath, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Cannot create a TestCase directly in the root. Please select a 'Question' folder.", "Warning");
                return string.Empty;
            }

            try
            {
                string testCasePath = _folderHandler.CreateDirectory(SelectedItem.FullPath, testCaseName);

                _folderHandler.CopyTemplateFromResource(testCasePath, _testCaseTemplateDir, false,
                    "Header.xlsx", "Environment.xlsx", "Detail.xlsx");

                string protocol = Settings.Default.Protocol;
                string networkSourceFile = (protocol == "TCP") ? "NetworkTCP.xlsx" : "NetworkHTTP.xlsx";
                string networkSourcePath = Path.Combine(_testCaseTemplateDir, networkSourceFile);
                string detailDestPath = Path.Combine(testCasePath, "Detail.xlsx");
                
                if (File.Exists(networkSourcePath) && File.Exists(detailDestPath))
                {
                    try
                    {
                        _folderHandler.ReplaceSheetExcel(networkSourcePath, detailDestPath, "Network");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to apply Network template: {ex.Message}");
                    }
                }
                else
                {
                    _logger.LogWarning($"Network template {networkSourceFile} not found.");
                }

                bool useDatabase = false;
                try
                {
                    string parentEnvPath = Path.Combine(SelectedItem.FullPath, "Environment.xlsx");
                    string indicator = _fileHandler.GetCellValue(parentEnvPath, "Run", 1, 1);

                    if (indicator != null && indicator.Equals("DATABASE_MODE", StringComparison.OrdinalIgnoreCase))
                    {
                        useDatabase = true;
                        _logger.LogInfomation($"Inherited UseDatabase=true from parent '{SelectedItem.Name}'");
                    }
                    else
                    {
                        _logger.LogInfomation($"Inherited UseDatabase=false from parent '{SelectedItem.Name}'");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Could not determine DB setting from parent. Defaulting to false. Error: {ex.Message}");
                }

                string chosenEnvRunFile = useDatabase ? "EnvRunDB.xlsx" : "EnvRunNoDB.xlsx";
                string srcSheetPath = Path.Combine(_testCaseTemplateDir, chosenEnvRunFile);
                string destEnvPath = Path.Combine(testCasePath, "Environment.xlsx");

                if (!File.Exists(srcSheetPath))
                {
                    throw new Exception($"Template file {chosenEnvRunFile} not found in '.../Templates/DotnetNetworking/TestCase'. \nPlease check 'Copy to Output Directory'.");
                }

                _folderHandler.ReplaceSheetExcel(srcSheetPath, destEnvPath, "Run");

                string metaPath = _folderHandler.CreateDirectory(testCasePath, "Meta");
                string givenPath = _folderHandler.CreateDirectory(metaPath, "Given");
                _folderHandler.CreateDirectory(givenPath, "Client");
                _folderHandler.CreateDirectory(givenPath, "Server");
                
                try
                {
                    string parentHeaderPath = Path.Combine(SelectedItem.FullPath, "Header.xlsx");
                    await _fileHandler.AppendNewRow(parentHeaderPath, "TestSuite", testCaseName, string.Empty);
                    await _fileHandler.AppendNewRow(parentHeaderPath, "QuestionMark", testCaseName, string.Empty);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Could not append TestCase to Header.xlsx. Error: {ex.Message}");
                }
                
                LoadFileTree();
                return testCasePath;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create test case: {ex.Message}");
                MessageBox.Show($"Failed to create test case: {ex.Message}", "Error");
            }
            return string.Empty;
        }

        public void DeleteSelectedItem()
        {
            if (SelectedItem == null) return;

            if (SelectedItem.FullPath.Equals(_projectPath, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Cannot delete the root project folder.", "Warning");
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to permanently delete:\n{SelectedItem.Name}?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    string projectRootPath = RootItems.Count > 0 ? RootItems[0].FullPath : string.Empty;
                    string parentPath = Path.GetDirectoryName(SelectedItem.FullPath);
                    bool isQuestion = !string.IsNullOrEmpty(parentPath) && 
                                     parentPath.Equals(projectRootPath, StringComparison.OrdinalIgnoreCase);
                    bool isTestCase = !SelectedItem.FullPath.Equals(projectRootPath, StringComparison.OrdinalIgnoreCase) && 
                                     !isQuestion;

                    if (isTestCase && SelectedItem.IsFolder)
                    {
                        string parentHeaderPath = Path.Combine(parentPath, "Header.xlsx");
                        string testCaseName = SelectedItem.Name;

                        try
                        {
                            (int row, int col) = _fileHandler.FindStringInExcel(parentHeaderPath, "TestSuite", testCaseName);
                            if (row > 0)
                            {
                                _fileHandler.DeleteRow(parentHeaderPath, "TestSuite", row);
                                _logger.LogInfomation($"Removed from 'TestSuite' at row {row}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Could not remove row from 'TestSuite'. Error: {ex.Message}");
                        }

                        try
                        {
                            (int row, int col) = _fileHandler.FindStringInExcel(parentHeaderPath, "QuestionMark", testCaseName);
                            if (row > 0)
                            {
                                _fileHandler.DeleteRow(parentHeaderPath, "QuestionMark", row);
                                _logger.LogInfomation($"Removed from 'QuestionMark' at row {row}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Could not remove row from 'QuestionMark'. Error: {ex.Message}");
                        }
                    }

                    _folderHandler.DeleteFileOrFolder(SelectedItem.FullPath);
                    _logger.LogInfomation($"Deleted: {SelectedItem.Name}");

                    LoadFileTree();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to delete: {ex.Message}");
                    MessageBox.Show($"Failed to delete: {ex.Message}", "Error");
                }
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
    }
}