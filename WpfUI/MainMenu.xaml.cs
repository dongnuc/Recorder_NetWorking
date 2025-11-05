using System;
using System.Windows;
using WpfUI.ViewModels;

namespace WpfUI
{
    public partial class MainMenu : Window
    {
        private MainMenuViewModel _viewModel;

        public MainMenu(MainMenuViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            this.DataContext = _viewModel;
        }

        private void FileTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (FileTree.SelectedItem is FileSystemItemViewModel selectedItem && !selectedItem.IsFolder)
            {
                if (_viewModel != null) _viewModel.SelectedItem = selectedItem;

                var textBlock = MainContentArea.Content as System.Windows.Controls.TextBlock;
                if (textBlock != null)
                {
                    textBlock.Text = $"Đã chọn file: {selectedItem.Name}\nĐường dẫn: {selectedItem.FullPath}";
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
    }
}