using Common.Interfaces.Logging;
using DatabaseServices.Services;
using Microsoft.Win32; // <<< THÊM USING NÀY
using System.IO;
using System.Windows;

namespace WpfUI
{
    public partial class ResetDbWindow : Window
    {
        private readonly ISystemLogger _logger;
        public ResetDbWindow( ISystemLogger logger)
        {
            InitializeComponent();
            _logger = logger;
        }

        private void BtnBrowseSql_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Select SQL Script";
            dialog.Filter = "SQL Script Files (*.sql)|*.sql|All Files (*.*)|*.*";

            if (dialog.ShowDialog() == true)
            {
                TxtSqlFilePath.Text = dialog.FileName;
            }
        }

        private async void BtnRunReset_Click(object sender, RoutedEventArgs e)
        {
            string selectedFilePath = TxtSqlFilePath.Text;

            string connectionString = TxtConnectionString.Text;

            if (string.IsNullOrWhiteSpace(selectedFilePath))
            {
                MessageBox.Show("Please select a SQL script file first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(selectedFilePath))
            {
                MessageBox.Show($"File not found:\n{selectedFilePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var service = new ResetDatabaseService(connectionString,_logger);
            var cts = new CancellationToken();
            var isSuccess= await service.ExecuteSqlWithConnectionString(connectionString, selectedFilePath, cts);

            if (isSuccess)
            {
                _logger.LogInfomation($"Executing SQL script: {selectedFilePath}");
                MessageBox.Show($"Reset Database SuccessFully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                _logger.LogError("Executing Sql fail");
            }

        }
    }
}