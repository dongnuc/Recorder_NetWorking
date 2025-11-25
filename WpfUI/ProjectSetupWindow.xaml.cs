using Common.Interfaces.IOFile;
using Common.Logging;
using FileManagement.FolderHelper;
using Microsoft.Win32;
using OfficeOpenXml;
using System.IO;
using System.Windows;
using WpfUI.Properties;
using WpfUI.ViewModels;

namespace WpfUI
{
    public partial class ProjectSetupWindow : Window
    {
        private readonly IOFolderHandler _folderHandler;
        private readonly IOFileHandler _fileHandler;
        private readonly IServiceProvider _serviceProvider; 
        private readonly IOFileManagement _fileManagement;

        public bool ProjectCreatedSuccessfully { get; private set; } = false;
        public MainMenuViewModel ViewModel { get; private set; }

        public ProjectSetupWindow(
            IOFileHandler fileHandler,
            IServiceProvider serviceProvider,
            IOFileManagement fileManagement)
        {
            InitializeComponent();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            _folderHandler = new FolderHandler();
            _fileHandler = fileHandler;
            _serviceProvider = serviceProvider;
            _fileManagement = fileManagement;

            LogManager.Instance.LogInfomation("🚀 ProjectSetupWindow opened");
            LoadSettings();
        }

        private void LoadSettings()
        {
            if (!string.IsNullOrEmpty(Settings.Default.ProjectPath))
            {
                string parentDir = Path.GetDirectoryName(Settings.Default.ProjectPath);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    TxtProjectLocation.Text = parentDir;
                }
                else
                {
                    TxtProjectLocation.Text = Settings.Default.ProjectPath;
                }
            }
        }

        private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { ValidateNames = false, CheckFileExists = false, CheckPathExists = true, FileName = "Select Folder", Title = "Select the base folder to create your project in" };
            if (dialog.ShowDialog() == true) { TxtProjectLocation.Text = Path.GetDirectoryName(dialog.FileName); }
        }
        private void BtnCreateProject_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtProjectName.Text) ||
                string.IsNullOrWhiteSpace(TxtProjectLocation.Text))
            {
                MessageBox.Show("Project Name and Location are required.", "Error");
                return;
            }

            TxtStatus.Text = "Checking project...";
            BtnCreateProject.IsEnabled = false;

            try
            {
                string basePath = TxtProjectLocation.Text;
                string projectName = TxtProjectName.Text;
                string projectRoot = Path.Combine(basePath, projectName);

                if (Directory.Exists(projectRoot))
                {
                    var result = MessageBox.Show(
                        "Testkit này đã tồn tại.\nBạn có muốn edit (chỉnh sửa) testkit này không?",
                        "Cảnh báo: Project đã tồn tại",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                    {
                        TxtStatus.Text = "Đã hủy. Vui lòng chọn tên hoặc vị trí khác.";
                        BtnCreateProject.IsEnabled = true;
                        return;
                    }
                    LogManager.Instance.LogInfomation($"Opening existing project at: {projectRoot}");
                }
                else
                {
                    _folderHandler.CreateDirectory(basePath, projectName);
                    LogManager.Instance.LogInfomation($"Project root created at: {projectRoot}");
                }

                Settings.Default.ProjectPath = projectRoot;
                Settings.Default.Save();
                LogManager.Instance.LogInfomation($"Settings saved (ProjectPath).");

                this.ViewModel = new MainMenuViewModel(
                    _folderHandler,
                    _fileHandler,
                    projectRoot
                );

                this.ProjectCreatedSuccessfully = true;

                this.Close();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Failed to create/open project.";
                LogManager.Instance.LogError($"Failed to create/open project: {ex.Message}");
                MessageBox.Show($"Failed to create/open project: {ex.Message}", "Error");
                BtnCreateProject.IsEnabled = true;
            }
        }
    }
}