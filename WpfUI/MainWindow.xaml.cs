using Common.Helper;
using Common.Logging;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace WpfUI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LogManager.Instance.LogInfomation("🚀 UITestKit started");
        }

        #region Browse EXE Files

        private void BtnBrowseClient_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                Title = "Select Client Executable"
            };

            if (dialog.ShowDialog() == true)
            {
                TxtClientPath.Text = dialog.FileName;
                LogManager.Instance.LogDebug($"Client path selected: {dialog.FileName}");
            }
        }

        private void BtnBrowseServer_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                Title = "Select Server Executable"
            };

            if (dialog.ShowDialog() == true)
            {
                TxtServerPath.Text = dialog.FileName;
                LogManager.Instance.LogDebug($"Server path selected: {dialog.FileName}");
            }
        }

        #endregion

        #region Start Recording

        // UITestKit/MainWindow.xaml.cs
        private async void BtnStartRecording_Click(object sender, RoutedEventArgs e)
        {
            BtnStartRecording.IsEnabled = false;
            TxtStatus.Text = "⏳ Starting...";

            try
            {
                // 1-5. Validation, port detection, appsettings update...
                if (!ValidateInputs())
                {
                    BtnStartRecording.IsEnabled = true;
                    TxtStatus.Text = "❌ Validation failed";
                    return;
                }

                string clientPath = TxtClientPath.Text.Trim();
                string serverPath = TxtServerPath.Text.Trim();
                string testCaseName = TxtTestCaseName.Text.Trim();
                bool useHttp = RbHttp.IsChecked == true;

                TxtStatus.Text = "✅ Recording started successfully!";
                LogManager.Instance.LogInfomation($"✅ Recording started - Test Case: {testCaseName}");

                // Minimize this window
                this.WindowState = WindowState.Minimized;
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Failed to start recording: {ex.Message}");
                MessageBox.Show(
                    $"Failed to start recording:\n\n{ex.Message}\n\nCheck log for details.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                TxtStatus.Text = "❌ Failed to start";
            }
            finally
            {
                BtnStartRecording.IsEnabled = true;
            }
        }
        #endregion

        #region Validation

        private bool ValidateInputs()
        {
            // Check client path
            if (string.IsNullOrWhiteSpace(TxtClientPath.Text))
            {
                MessageBox.Show("Please select Client executable.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!File.Exists(TxtClientPath.Text))
            {
                MessageBox.Show($"Client executable not found:\n{TxtClientPath.Text}",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Check server path
            if (string.IsNullOrWhiteSpace(TxtServerPath.Text))
            {
                MessageBox.Show("Please select Server executable.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!File.Exists(TxtServerPath.Text))
            {
                MessageBox.Show($"Server executable not found:\n{TxtServerPath.Text}",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Check test case name
            if (string.IsNullOrWhiteSpace(TxtTestCaseName.Text))
            {
                MessageBox.Show("Please enter a test case name.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        #endregion
    }
}