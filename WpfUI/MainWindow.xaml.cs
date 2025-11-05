// UITestKit/MainWindow.xaml.cs
using Common.Helper;
using Common.Logging;
using Microsoft.Win32;
using Middleware.Services;
using ProcessManagement.Services;
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

                // Get available ports
                TxtStatus.Text = "🔍 Finding available ports...";
                var (proxyPort, serverPort) = PortChecker.GetTwoAvailablePorts(8000, 9000);

                TxtPortInfo.Text = $"Proxy Port: {proxyPort} | Server Port: {serverPort}";
                LogManager.Instance.LogInfomation($"📡 Ports allocated - Proxy: {proxyPort}, Server: {serverPort}");

                // Update appsettings.json
                TxtStatus.Text = "📝 Updating appsettings.json...";
                if (!UpdateAppSettings(clientPath, proxyPort, serverPath, serverPort))
                {
                    MessageBox.Show("Failed to update appsettings.json. Check log for details.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    BtnStartRecording.IsEnabled = true;
                    TxtStatus.Text = "❌ Failed to update config";
                    return;
                }

                // Start Middleware
                TxtStatus.Text = "🌐 Starting middleware...";
                await MiddlewareStart.Instance.StartAsync(proxyPort, serverPort, useHttp);
                LogManager.Instance.LogInfomation($"✅ Middleware started - Protocol: {(useHttp ? "HTTP" : "TCP")}");

                // Create ProcessManager
                var processManager = new ProcessManager();

                // Create RecorderWindow
                TxtStatus.Text = "🎙️ Opening recorder...";
                var recorderWindow = new RecorderWindow(testCaseName, clientPath,serverPath);
                recorderWindow.Title = $"Recording: {testCaseName}";
                recorderWindow.Show();


                TxtStatus.Text = "✅ Recording started successfully!";
                LogManager.Instance.LogInfomation($"✅ Recording started - Test Case: {testCaseName}");

                // Show success message
                MessageBox.Show(
                    $"Recording started successfully!\n\n" +
                    $"Test Case: {testCaseName}\n" +
                    $"Protocol: {(useHttp ? "HTTP" : "TCP")}\n" +
                    $"Proxy Port: {proxyPort}\n" +
                    $"Server Port: {serverPort}\n\n" +
                    $"Press Enter in client console to create new stages.",
                    "Recording Started",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

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

        #region Update AppSettings

        private bool UpdateAppSettings(string clientPath, int clientPort, string serverPath, int serverPort)
        {
            try
            {
                // Get or create appsettings.json for client
                string clientAppSettings = AppSettingsPathResolver.GetAppSettingsPath(clientPath);
                if (string.IsNullOrEmpty(clientAppSettings))
                {
                    LogManager.Instance.LogWarning("Client appsettings.json not found, creating default...");
                }
                else
                {
                    var clientManager = new AppSettingsManager(clientAppSettings);
                    clientManager.UpdatePort(clientPort, createBackup: true);
                    LogManager.Instance.LogInfomation($"✅ Client appsettings updated - Port: {clientPort}");
                }

                // Get or create appsettings.json for server
                string serverAppSettings = AppSettingsPathResolver.GetAppSettingsPath(serverPath);
                if (string.IsNullOrEmpty(serverAppSettings))
                {
                    LogManager.Instance.LogWarning("Server appsettings.json not found, creating default...");
                }
                else
                {
                    var serverManager = new AppSettingsManager(serverAppSettings);
                    serverManager.UpdatePort(serverPort, createBackup: true);
                    LogManager.Instance.LogInfomation($"✅ Server appsettings updated - Port: {serverPort}");
                }

                return true;
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Failed to update appsettings: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}