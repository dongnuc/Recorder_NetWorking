using Common.Interfaces.IOFile;
// (Xóa các Using không cần thiết: IServiceProvider, IProcessManager, v.v...)
using Common.Logging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfUI.Dialogs;
using WpfUI.Properties;
using WpfUI.ViewModels;
using System;
using System.Linq;
using Microsoft.Win32;
using FileManagement.FileHelper;
using FileManagement.FileHelper.FileHandler;

namespace WpfUI
{
    public partial class MainMenu : Window
    {
        private MainMenuViewModel _viewModel;
        private readonly IOFileManagement _fileManager;
        private IOFileHandler _fileHandler;
        
        public MainMenu(MainMenuViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            this.DataContext = _viewModel;

            _fileHandler = new ExcelExecution();
            _fileManager = new FileManage(_fileHandler);
        }

        private void FileTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (FileTree.SelectedItem is FileSystemItemViewModel selectedItem)
            {
                if (_viewModel != null) _viewModel.SelectedItem = selectedItem;

                if (!selectedItem.IsFolder)
                {
                    try
                    {
                        _fileManager.OpenExcelFile(selectedItem.FullPath);
                    }
                    catch (System.ComponentModel.Win32Exception ex)
                    {
                        MessageBox.Show($"Không thể mở file.\n\nLỗi: {ex.Message}",
                            "Lỗi Mở File", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Đã xảy ra lỗi khi mở file:\n{ex.Message}",
                            "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnConfig_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ConfigWindow();
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                _viewModel.UpdateAllQuestionConfigs();
            }
        }

        private void TreeViewItem_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var treeViewItem = sender as TreeViewItem;
            if (treeViewItem == null) return;


            var selectedItem = _viewModel.SelectedItem;
            if (selectedItem == null) return;

            var contextMenu = treeViewItem.ContextMenu;
            if (contextMenu == null) return;

            var items = contextMenu.Items.OfType<FrameworkElement>();
            var cmCreateQuestion = items.FirstOrDefault(i => i.Tag?.ToString() == "CreateQuestion") as MenuItem;
            var cmDeleteQuestion = items.FirstOrDefault(i => i.Tag?.ToString() == "DeleteQuestion") as MenuItem;
            var cmSep1 = items.FirstOrDefault(i => i.Tag?.ToString() == "Sep1") as Separator;
            var cmCreateTestCase = items.FirstOrDefault(i => i.Tag?.ToString() == "CreateTestCase") as MenuItem;
            var cmDeleteTestCase = items.FirstOrDefault(i => i.Tag?.ToString() == "DeleteTestCase") as MenuItem;

            if (cmCreateQuestion == null || cmDeleteQuestion == null || cmSep1 == null ||
                cmCreateTestCase == null || cmDeleteTestCase == null)
            {
                return;
            }

            cmCreateQuestion.Visibility = Visibility.Collapsed;
            cmDeleteQuestion.Visibility = Visibility.Collapsed;
            cmSep1.Visibility = Visibility.Collapsed;
            cmCreateTestCase.Visibility = Visibility.Collapsed;
            cmDeleteTestCase.Visibility = Visibility.Collapsed;

            if (!selectedItem.IsFolder)
            {
                e.Handled = true;
                return;
            }

            string projectRootPath = _viewModel.RootItems.Count > 0 ? _viewModel.RootItems[0].FullPath : string.Empty;
            if (string.IsNullOrEmpty(projectRootPath)) return;

            if (selectedItem.FullPath.Equals(projectRootPath, StringComparison.OrdinalIgnoreCase))
            {
                cmCreateQuestion.Visibility = Visibility.Visible;
            }
            else if (Path.GetDirectoryName(selectedItem.FullPath) != null &&
                     Path.GetDirectoryName(selectedItem.FullPath).Equals(projectRootPath, StringComparison.OrdinalIgnoreCase))
            {
                cmCreateTestCase.Visibility = Visibility.Visible;
                cmDeleteQuestion.Visibility = Visibility.Visible;
                cmSep1.Visibility = Visibility.Visible;
            }
            else
            {
                cmDeleteTestCase.Visibility = Visibility.Visible;
            }
        }

        private async void BtnCreateTestCase_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedItem == null || !_viewModel.SelectedItem.IsFolder ||
                (_viewModel.RootItems.Count > 0 && _viewModel.SelectedItem.FullPath.Equals(_viewModel.RootItems[0].FullPath, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Bạn phải chọn một thư mục Question để tạo TestCase.", "Warning");
                return;
            }

            var dialog = new InputBoxWindow("Enter new test case name:", $"TestCase_{DateTime.Now:yyyyMMdd_HHmmss}");
            dialog.Owner = this;

            string clientPath = Settings.Default.ClientExePath;
            string serverPath = Settings.Default.ServerExePath;
            if (string.IsNullOrEmpty(clientPath) || string.IsNullOrEmpty(serverPath))
            {
                MessageBox.Show("Hãy cấu hình (Config) đường dẫn Client/Server trước khi tạo testcase.", "Warning");
                return;
            }

            if (dialog.ShowDialog() == true)
            {
                string testcaseName = dialog.InputText.Trim();
                string testcasePath = await _viewModel?.CreateNewTestCase(dialog.InputText);

                if (string.IsNullOrEmpty(testcasePath)) return;

                LogManager.Instance.LogInfomation(dialog.InputText.ToString());
                bool isHttp = Settings.Default.Protocol == "HTTP";

            }
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnResetDb_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ResetDbWindow();
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        private void CmCreateQuestion_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CreateQuestionWindow();
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                _viewModel?.CreateNewQuestion(dialog.QuestionName, dialog.UseDatabase);
            }
        }

        private void CmCreateTestCase_Click(object sender, RoutedEventArgs e)
        {
            BtnCreateTestCase_Click(sender, e);
        }

        private void CmDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedItem == null) return;
            _viewModel?.DeleteSelectedItem();
        }

        private void TreeViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = sender as TreeViewItem;
            if (item != null)
            {
                item.Focus();
                item.IsSelected = true;
                e.Handled = false;
            }
        }
    }
}