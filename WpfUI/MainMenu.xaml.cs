using Common.Interfaces.IOFile;
using FileManagement.FileHelper;
using FileManagement.FileHelper.FileHandler;
using Microsoft.Win32;
using System;
using System.ComponentModel; 
using System.Diagnostics; 
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
            if (FileTree.SelectedItem is FileSystemItemViewModel selectedItem && !selectedItem.IsFolder)
            {
                if (_viewModel != null) _viewModel.SelectedItem = selectedItem;

                try
                {
                    _fileManager.OpenExcelFile(selectedItem.FullPath);
                }
                catch (Win32Exception ex)
                {
                    MessageBox.Show($"Không thể mở file. Máy của bạn không có chương trình nào được liên kết với file '.xlsx'.\n\nLỗi: {ex.Message}", "Lỗi Mở File", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Đã xảy ra lỗi khi mở file:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnCreateTestCase_Click(object sender, RoutedEventArgs e)
        {
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
    }
}