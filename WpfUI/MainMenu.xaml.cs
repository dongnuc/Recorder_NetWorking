using Common.Interfaces.IOFile;
using FileManagement.FileHelper;
using FileManagement.FileHelper.FileHandler;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfUI.Dialogs;
using WpfUI.ViewModels;

namespace WpfUI
{
    public partial class MainMenu : Window
    {
        private MainMenuViewModel _viewModel;
        private readonly IOFileManagement _fileManager;

        public MainMenu(MainMenuViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            this.DataContext = _viewModel;
            IOFileHandler fileHandler = new ExcelExecution();
            _fileManager = new FileManage(fileHandler);
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
                    catch (Win32Exception ex)
                    {
                        MessageBox.Show($"Không thể mở file. Máy của bạn không có chương trình nào được liên kết với file '.xlsx'.\n\nLỗi: {ex.Message}",
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
            dialog.ShowDialog();

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

        private void BtnCreateTestCase_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedItem == null || !_viewModel.SelectedItem.IsFolder ||
                (_viewModel.RootItems.Count > 0 && _viewModel.SelectedItem.FullPath.Equals(_viewModel.RootItems[0].FullPath, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Bạn phải chọn một thư mục Question (không phải thư mục gốc) để tạo TestCase.", "Warning");
                return;
            }

            var dialog = new InputBoxWindow("Enter new test case name:", $"TestCase_{DateTime.Now:yyyyMMdd_HHmmss}");
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                _viewModel?.CreateNewTestCase(dialog.InputText);
            }
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
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