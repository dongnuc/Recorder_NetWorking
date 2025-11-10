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
            TxtProjectLocation.Text = Settings.Default.ProjectPath;
            TxtClientPath.Text = Settings.Default.ClientExePath;
            TxtServerPath.Text = Settings.Default.ServerExePath;
            if (Settings.Default.UseDatabase) { RbDbYes.IsChecked = true; }
            else { RbDbNo.IsChecked = true; }
        }

        #region Browse Buttons
        private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { ValidateNames = false, CheckFileExists = false, CheckPathExists = true, FileName = "Select Folder", Title = "Select the base folder to create your project in" };
            if (dialog.ShowDialog() == true) { TxtProjectLocation.Text = Path.GetDirectoryName(dialog.FileName); }
        }
        private void BtnBrowseClient_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "Executable (*.exe)|*.exe" };
            if (dialog.ShowDialog() == true) TxtClientPath.Text = dialog.FileName;
        }
        private void BtnBrowseServer_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "Executable (*.exe)|*.exe" };
            if (dialog.ShowDialog() == true) TxtServerPath.Text = dialog.FileName;
        }
        #endregion

        private void BtnCreateProject_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtProjectName.Text) ||
                string.IsNullOrWhiteSpace(TxtProjectLocation.Text) ||
                string.IsNullOrWhiteSpace(TxtClientPath.Text) ||
                string.IsNullOrWhiteSpace(TxtServerPath.Text))
            {
                MessageBox.Show("All fields (Project, Location, Client, Server) are required.", "Error");
                return;
            }

            TxtStatus.Text = "⏳ Creating project structure...";
            BtnCreateProject.IsEnabled = false;
            try
            {
                string basePath = TxtProjectLocation.Text;
                string projectName = TxtProjectName.Text;

                string templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                        "Resources", "Templates", "DotnetNetworking", "Question");

                if (!Directory.Exists(templateDir) ||
                    !File.Exists(Path.Combine(templateDir, "Header.xlsx")) ||
                    !File.Exists(Path.Combine(templateDir, "Environment.xlsx")) || 
                    !File.Exists(Path.Combine(templateDir, "EnvRunDB.xlsx")) ||    
                    !File.Exists(Path.Combine(templateDir, "EnvRunNoDB.xlsx")))   
                {
                    throw new Exception("Template folder or files ('Header', 'Environment', 'EnvRunDB', 'EnvRunNoDB') not found in '.../Question'. \nPlease check 'Copy to Output Directory'.");
                }

                string projectRoot = _folderHandler.CreateDirectory(basePath, projectName);

                _folderHandler.CopyTemplateFromResource(projectRoot, templateDir, false, "Header.xlsx", "Environment.xlsx");

                bool useDatabase = RbDbYes.IsChecked == true;

                string chosenEnvRunFile = useDatabase ? "EnvRunDB.xlsx" : "EnvRunNoDB.xlsx";
                string srcSheetPath = Path.Combine(templateDir, chosenEnvRunFile);

                string destEnvPath = Path.Combine(projectRoot, "Environment.xlsx");

                _folderHandler.ReplaceSheetExcel(srcSheetPath, destEnvPath);

                LogManager.Instance.LogInfomation($"Project created at: {projectRoot}. UseDatabase={useDatabase}");

                Settings.Default.ProjectPath = basePath;
                Settings.Default.ClientExePath = TxtClientPath.Text;
                Settings.Default.ServerExePath = TxtServerPath.Text;
                Settings.Default.UseDatabase = useDatabase;
                Settings.Default.Save();

                var mainViewModel = new MainMenuViewModel(
                    _folderHandler,
                    _fileHandler,
                    projectRoot,
                    TxtClientPath.Text,
                    TxtServerPath.Text
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