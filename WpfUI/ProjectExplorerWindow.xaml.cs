using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WpfUI.Properties;
using System.IO;
using WpfUI.ViewModels;
using FileManagement.FolderHelper;
using FileManagement.FileHelper.FileHandler;
using Common.Interfaces.IOFile;
using Microsoft.Win32; 

namespace WpfUI
{
    public partial class ProjectExplorerWindow : Window
    {
        private readonly IOFolderHandler _folderHandler;
        private readonly IOFileHandler _fileHandler;

        public ProjectExplorerWindow()
        {
            InitializeComponent();
            _folderHandler = new FolderHandler();
            _fileHandler = new ExcelExecution();
            LoadRecentProject();
        }

        private void LoadRecentProject()
        {
            string recentPath = Settings.Default.ProjectPath;
            if (!string.IsNullOrEmpty(recentPath) && Directory.Exists(recentPath))
            {
                LstRecentProjects.Items.Clear();
                LstRecentProjects.Items.Add(new ListBoxItem
                {
                    Content = System.IO.Path.GetFileName(recentPath),
                    Tag = recentPath,
                    ToolTip = recentPath
                });
            }
        }

        private void BtnCreateNew_Click(object sender, RoutedEventArgs e)
        {
            var setupWindow = new ProjectSetupWindow();

            this.Hide();
            var result = setupWindow.ShowDialog();

            if (setupWindow.ProjectCreatedSuccessfully)
            {
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
                Title = "Chọn thư mục Project TestKit"
            };

            if (dialog.ShowDialog() == true)
            {
                string projectRoot = dialog.FolderName;

                if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                {
                    MessageBox.Show("Đường dẫn project không hợp lệ.", "Error");
                    return;
                }
                Settings.Default.ProjectPath = projectRoot;
                Settings.Default.Save();
                OpenMainMenu(projectRoot);
            }
        }

        private void LstRecentProjectsOpen(object sender, SelectionChangedEventArgs e)
        {
            if (LstRecentProjects.SelectedItem is ListBoxItem selectedItem)
            {
                string projectRoot = selectedItem.Tag?.ToString();

                if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                {
                    MessageBox.Show("Đường dẫn project này không còn tồn tại.", "Error");
                    LstRecentProjects.Items.Remove(selectedItem);
                    return;
                }

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
            this.Hide();

            var mainMenu = new MainMenu(viewModel);
            mainMenu.Closed += MainMenu_Closed;
            mainMenu.Show();
            LstRecentProjects.SelectedItem = null;
        }

        private void MainMenu_Closed(object sender, EventArgs e)
        {
            LoadRecentProject();
            this.Show();
        }
    }
}