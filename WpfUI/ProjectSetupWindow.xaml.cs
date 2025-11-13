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
        private readonly IServiceProvider _serviceProvider;
        private readonly IOFileManagement _fileManagement;
        public ProjectSetupWindow(IOFileHandler fileHandler,
        IServiceProvider serviceProvider,
        IOFileManagement fileManagement)
        {
            InitializeComponent();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            _folderHandler = new FolderHandler();
            _fileHandler = new ExcelExecution();
            _serviceProvider = serviceProvider;
            _fileManagement = fileManagement;
            LogManager.Instance.LogInfomation("🚀 ProjectSetupWindow opened");

            LoadSettings();
        }

        private void LoadSettings()
        {
            if (!string.IsNullOrEmpty(Settings.Default.ProjectPath))
            {
                TxtProjectLocation.Text = Path.GetDirectoryName(Settings.Default.ProjectPath);
            }
        }

        #region Browse Buttons
        private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { ValidateNames = false, CheckFileExists = false, CheckPathExists = true, FileName = "Select Folder", Title = "Select the base folder to create your project in" };
            if (dialog.ShowDialog() == true) { TxtProjectLocation.Text = Path.GetDirectoryName(dialog.FileName); }
        }


        #endregion

        private void BtnCreateProject_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtProjectName.Text) ||
                string.IsNullOrWhiteSpace(TxtProjectLocation.Text))
            {
                MessageBox.Show("Project Name and Location are required.", "Error");
                return;
            }

            TxtStatus.Text = "Creating project structure...";
            BtnCreateProject.IsEnabled = false;
            try
            {
                string basePath = TxtProjectLocation.Text;
                string projectName = TxtProjectName.Text;

                string projectRoot = _folderHandler.CreateDirectory(basePath, projectName);
                LogManager.Instance.LogInfomation($"Project root created at: {projectRoot}");

                Settings.Default.ProjectPath = projectRoot;


                Settings.Default.Save();
                LogManager.Instance.LogInfomation($"Project created. Settings saved (ProjectPath).");

                var mainViewModel = new MainMenuViewModel(
                    _folderHandler,
                    _fileHandler,
                    projectRoot
                );
                var mainMenu = new MainMenu(mainViewModel,_fileHandler,_serviceProvider,_fileManagement);
                mainMenu.Show();

                this.Close();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Failed to create project.";
                LogManager.Instance.LogError($"Failed to create project: {ex.Message}");
                MessageBox.Show($"Failed to create project: {ex.Message}", "Error");
                BtnCreateProject.IsEnabled = true;
            }
        }
    }
}