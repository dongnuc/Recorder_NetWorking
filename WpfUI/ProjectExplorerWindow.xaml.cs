using System;
using System.Windows;
using System.Windows.Controls;
using WpfUI.Properties;
using System.IO;
using WpfUI.ViewModels;
using FileManagement.FolderHelper;
using FileManagement.FileHelper;
using FileManagement.FileHelper.FileHandler;
using Common.Interfaces.IOFile;
using Microsoft.Win32;
using Microsoft.Extensions.DependencyInjection;
using WpfUI.Services;
using Common.Interfaces.Services;
using System.Collections.Specialized;

namespace WpfUI
{
    public class RecentProjectItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Date { get; set; }
    }

    public partial class ProjectExplorerWindow : Window
    {
        private readonly IOFolderHandler _folderHandler;
        private readonly IOFileHandler _fileHandler;
        private readonly IOFileManagement _fileManager;
        private readonly IServiceProvider _serviceProvider;

        private bool _isNavigatingToMainMenu = false;

        public ProjectExplorerWindow(
            IServiceProvider serviceProvider,
            IOFolderHandler folderHandler,
            IOFileHandler fileHandler,
            IOFileManagement fileManager)
        {
            InitializeComponent();

            _serviceProvider = serviceProvider;
            _folderHandler = folderHandler;
            _fileHandler = fileHandler;
            _fileManager = fileManager;

            LoadRecentProject();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (!_isNavigatingToMainMenu)
            {
                Application.Current.Shutdown();
            }
        }

        private void LoadRecentProject()
        {
            LstRecentProjects.Items.Clear();

            if (Settings.Default.RecentProjects == null)
            {
                Settings.Default.RecentProjects = new StringCollection();
                Settings.Default.Save();
            }

            foreach (string path in Settings.Default.RecentProjects)
            {
                if (Directory.Exists(path))
                {
                    DateTime lastWrite = Directory.GetLastWriteTime(path);

                    LstRecentProjects.Items.Add(new RecentProjectItem
                    {
                        Name = System.IO.Path.GetFileName(path),
                        Path = path,
                        Date = $"Last modified: {lastWrite:dd/MM/yyyy HH:mm}"
                    });
                }
            }
        }

        private void AddToRecentProjects(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath)) return;

            if (Settings.Default.RecentProjects == null)
            {
                Settings.Default.RecentProjects = new StringCollection();
            }

            if (Settings.Default.RecentProjects.Contains(projectPath))
            {
                Settings.Default.RecentProjects.Remove(projectPath);
            }

            Settings.Default.RecentProjects.Insert(0, projectPath);

            while (Settings.Default.RecentProjects.Count > 10)
            {
                Settings.Default.RecentProjects.RemoveAt(10);
            }

            Settings.Default.ProjectPath = projectPath;
            Settings.Default.Save();
        }

        private void BtnCreateNew_Click(object sender, RoutedEventArgs e)
        {
            var setupWindow = new ProjectSetupWindow(
                _fileHandler,
                _serviceProvider,
                _fileManager);

            this.Hide();
            setupWindow.ShowDialog();

            if (setupWindow.ProjectCreatedSuccessfully)
            {
                string newPath = Settings.Default.ProjectPath;
                AddToRecentProjects(newPath);

                OpenMainMenu(setupWindow.ViewModel);
            }
            else
            {
                this.Show();
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Chọn thư mục Project TestKit (thư mục gốc)"
            };

            if (dialog.ShowDialog() == true)
            {
                string projectRoot = dialog.FolderName;

                if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                {
                    MessageBox.Show("Đường dẫn project không hợp lệ.", "Error");
                    return;
                }

                AddToRecentProjects(projectRoot);
                OpenMainMenu(projectRoot);
            }
        }

        private void LstRecentProjectsOpen(object sender, SelectionChangedEventArgs e)
        {
            if (LstRecentProjects.SelectedItem is RecentProjectItem selectedItem)
            {
                string projectRoot = selectedItem.Path;

                if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                {
                    MessageBox.Show("Đường dẫn project này không còn tồn tại.", "Error");
                    Settings.Default.RecentProjects.Remove(projectRoot);
                    Settings.Default.Save();

                    LoadRecentProject();
                    return;
                }

                AddToRecentProjects(projectRoot);
                OpenMainMenu(projectRoot);
            }
        }

        private void OpenMainMenu(string projectRootPath)
        {
            var mainViewModel = new MainMenuViewModel(
                _folderHandler,
                _fileHandler,
                projectRootPath
            );
            OpenMainMenu(mainViewModel);
        }

        private void OpenMainMenu(MainMenuViewModel viewModel)
        {
            _isNavigatingToMainMenu = true;

            this.Hide();

            var mainMenu = new MainMenu(
                viewModel,
                _fileHandler,
                _serviceProvider,
                _fileManager
            );

            mainMenu.Closed += MainMenu_Closed;
            mainMenu.Show();

            LstRecentProjects.SelectedItem = null;
        }

        private void MainMenu_Closed(object sender, EventArgs e)
        {
            _isNavigatingToMainMenu = false;
            LoadRecentProject();
            this.Show();
        }
    }
}