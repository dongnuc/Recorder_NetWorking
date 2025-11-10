using Common.Interfaces.IOFile;
using Common.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System;
using WpfUI.Properties; 

namespace WpfUI.ViewModels
{
    public class MainMenuViewModel : INotifyPropertyChanged
    {
        #region Fields
        private readonly IOFolderHandler _folderHandler;
        private readonly IOFileHandler _fileHandler;
        private readonly string _projectPath;
        private readonly string _clientExePath;
        private readonly string _serverExePath;

        private readonly string _templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                                "Resources", "Templates", "DotnetNetworking", "TestCase");
        #endregion

        #region Properties
        public ObservableCollection<FileSystemItemViewModel> RootItems { get; set; }
        public FileSystemItemViewModel SelectedItem { get; set; }
        #endregion

        public MainMenuViewModel(
            IOFolderHandler folderHandler,
            IOFileHandler fileHandler,
            string projectPath,
            string clientExePath,
            string serverExePath)
        {
            _folderHandler = folderHandler;
            _fileHandler = fileHandler;
            _projectPath = projectPath;
            _clientExePath = clientExePath;
            _serverExePath = serverExePath;
            RootItems = new ObservableCollection<FileSystemItemViewModel>();
            LoadFileTree();
        }

        #region File/Folder Tree Logic

        public void LoadFileTree()
        {
            RootItems.Clear();
            try
            {
                if (!Directory.Exists(_projectPath)) { _folderHandler.CreateDirectory(_projectPath, ""); }

                var rootDirectoryInfo = new DirectoryInfo(_projectPath);
                var rootNode = new FileSystemItemViewModel { Name = rootDirectoryInfo.Name, FullPath = rootDirectoryInfo.FullName, IsFolder = true };
                LoadSubFoldersAndFiles(rootNode);
                RootItems.Add(rootNode);
            }
            catch (Exception ex) { LogManager.Instance.LogError($"Error loading file tree root: {ex.Message}"); }
        }
        private void LoadSubFoldersAndFiles(FileSystemItemViewModel parentNode)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(parentNode.FullPath))
                {
                    var dirInfo = new DirectoryInfo(dir);
                    var childFolder = new FileSystemItemViewModel { Name = dirInfo.Name, FullPath = dirInfo.FullName, IsFolder = true };
                    LoadSubFoldersAndFiles(childFolder);
                    parentNode.Children.Add(childFolder);
                }
                foreach (var file in Directory.GetFiles(parentNode.FullPath))
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.Extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) || fileInfo.Extension.Equals(".xls", StringComparison.OrdinalIgnoreCase))
                    {
                        var childFile = new FileSystemItemViewModel { Name = fileInfo.Name, FullPath = fileInfo.FullName, IsFolder = false };
                        parentNode.Children.Add(childFile);
                    }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (Exception ex) { LogManager.Instance.LogWarning($"Could not access path: {parentNode.FullPath}. Error: {ex.Message}"); }
        }

        public void CreateNewTestCase(string testCaseName)
        {
            if (string.IsNullOrWhiteSpace(testCaseName)) { MessageBox.Show("Test case name cannot be empty.", "Warning"); return; }

            try
            {
                string testCasePath = _folderHandler.CreateDirectory(_projectPath, testCaseName);

                _folderHandler.CopyTemplateFromResource(testCasePath, _templateDir, false,
                    "Header.xlsx", "Environment.xlsx", "Detail.xlsx");

                bool useDatabase = Settings.Default.UseDatabase;

                string chosenEnvRunFile = useDatabase ? "EnvRunDB.xlsx" : "EnvRunNoDB.xlsx";
                string srcSheetPath = Path.Combine(_templateDir, chosenEnvRunFile);

                string destEnvPath = Path.Combine(testCasePath, "Environment.xlsx");

                if (!File.Exists(srcSheetPath))
                {
                    throw new Exception($"Template file {chosenEnvRunFile} not found in '.../Templates/DotnetNetworking/TestCase'. \nPlease check 'Copy to Output Directory'.");
                }

                _folderHandler.ReplaceSheetExcel(srcSheetPath, destEnvPath);

                LoadFileTree();
                LogManager.Instance.LogInfomation($"✅ Created new test case: {testCaseName}. UseDatabase={useDatabase}");
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Failed to create test case: {ex.Message}");
                MessageBox.Show($"Failed to create test case: {ex.Message}", "Error");
            }
        }
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }
        #endregion
    }
}