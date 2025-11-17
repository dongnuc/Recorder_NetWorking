using Common.Interfaces.IOFile;
using Common.Interfaces.Services;
using Common.Logging;
using FileManagement.FileHelper.FileHandler;
using FileManagement.FolderHelper;
using Microsoft.Win32;
using OfficeOpenXml;
using System;
using System.IO;
using System.Windows;
using WpfUI.ViewModels;
using WpfUI.Properties;

namespace WpfUI
{
    public partial class ProjectSetupWindow : Window
    {
        private readonly IOFolderHandler _folderHandler;
        private readonly IOFileHandler _fileHandler;

        public bool ProjectCreatedSuccessfully { get; private set; } = false;
        public MainMenuViewModel ViewModel { get; private set; }

        public ProjectSetupWindow()
        {
            InitializeComponent();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            _folderHandler = new FolderHandler();
            _fileHandler = new ExcelExecution();
            LogManager.Instance.LogInfomation("ProjectSetupWindow opened");

            LoadSettings();
        }

        private void LoadSettings()
        {
            if (!string.IsNullOrEmpty(Settings.Default.ProjectPath))
            {
                string parentDir = Path.GetDirectoryName(Settings.Default.ProjectPath);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    TxtTestKitLocation.Text = parentDir;
                }
                else
                {
                    TxtTestKitLocation.Text = Settings.Default.ProjectPath;
                }
            }
        }

        #region Browse Buttons
        private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { ValidateNames = false, CheckFileExists = false, CheckPathExists = true, FileName = "Select Folder", Title = "Select the base folder to create your project in" };
            if (dialog.ShowDialog() == true) { TxtTestKitLocation.Text = Path.GetDirectoryName(dialog.FileName); }
        }
        #endregion

        private void BtnCreateProject_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtTestKitName.Text) ||
                string.IsNullOrWhiteSpace(TxtTestKitLocation.Text))
            {
                MessageBox.Show("Project Name and Location are required.", "Error");
                return;
            }

            TxtStatus.Text = "Checking project...";
            BtnCreateTestKit.IsEnabled = false;

            try
            {
                string basePath = TxtTestKitLocation.Text;
                string projectName = TxtTestKitName.Text;
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
                        BtnCreateTestKit.IsEnabled = true;
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
                BtnCreateTestKit.IsEnabled = true;
            }
        }
    }
}