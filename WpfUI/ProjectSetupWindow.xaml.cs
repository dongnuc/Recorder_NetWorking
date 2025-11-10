using Common.Interfaces.IOFile;
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

        public ProjectSetupWindow()
        {
            InitializeComponent();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            _folderHandler = new FolderHandler();
            _fileHandler = new ExcelExecution();
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
                bool useHttp = RbHttp.IsChecked == true;
                string templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Templates");

                if (!Directory.Exists(templateDir) ||
                    !File.Exists(Path.Combine(templateDir, "header.xlsx")) ||
                    !File.Exists(Path.Combine(templateDir, "environment.xlsx")))
                {
                    throw new Exception("Template folder or files ('header.xlsx', 'environment.xlsx') not found. \nPlease check 'Resources/Templates' and set 'Copy to Output Directory'.");
                }

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
                var mainMenu = new MainMenu(mainViewModel);
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