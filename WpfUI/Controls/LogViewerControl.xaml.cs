// WpfUI/Controls/LogViewerControl.xaml.cs
using Common.Logging;
using Common.Models.Entities;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace WpfUI.Controls
{
    public partial class LogViewerControl : UserControl
    {
        private readonly ObservableCollection<LogEntryViewModel> _displayedLogs = new ObservableCollection<LogEntryViewModel>();
        private LogLevel? _selectedLogLevel = null;
        private string _searchKeyword = string.Empty;

        public LogViewerControl()
        {
            InitializeComponent();

            // Bind ListView
            LstLogs.ItemsSource = _displayedLogs;

            // Load existing logs
            LoadExistingLogs();

            // Subscribe to new log events
            LogManager.Instance.OnLogAdded += OnLogAdded;

            // Unsubscribe when unloaded
            Unloaded += (s, e) => LogManager.Instance.OnLogAdded -= OnLogAdded;

            // Show log file path
            TxtLogFilePath.Text = $"📄 {System.IO.Path.GetFileName(LogManager.Instance.LogFilePath)}";

            UpdateLogCount();
        }

        /// <summary>
        /// Load existing logs from LogManager
        /// </summary>
        private void LoadExistingLogs()
        {
            var existingLogs = LogManager.Instance.GetAllLogs();
            foreach (var log in existingLogs)
            {
                AddLogToDisplay(log);
            }
        }

        /// <summary>
        /// Event handler for new logs
        /// </summary>
        private void OnLogAdded(LogEntry logEntry)
        {
            // Must invoke on UI thread
            Dispatcher.Invoke(() =>
            {
                AddLogToDisplay(logEntry);
                AutoScrollIfEnabled();
            });
        }

        private void AddLogToDisplay(LogEntry logEntry)
        {
            // Apply filters
            if (_selectedLogLevel.HasValue && logEntry.Level != _selectedLogLevel.Value)
                return;

            if (!string.IsNullOrEmpty(_searchKeyword) &&
                !logEntry.Message.Contains(_searchKeyword, StringComparison.OrdinalIgnoreCase))
                return;

            var viewModel = new LogEntryViewModel(logEntry);
            _displayedLogs.Add(viewModel);

            // Keep only last 500 in UI (performance)
            while (_displayedLogs.Count > 500)
            {
                _displayedLogs.RemoveAt(0);
            }

            UpdateLogCount();
        }

        private void UpdateLogCount()
        {
            TxtLogCount.Text = $"{_displayedLogs.Count} logs";
        }

        private void AutoScrollIfEnabled()
        {
            if (ChkAutoScroll.IsChecked == true && LstLogs.Items.Count > 0)
            {
                LstLogs.ScrollIntoView(LstLogs.Items[LstLogs.Items.Count - 1]);
            }
        }

        private void BtnClearLogs_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Clear all logs?\n\nThis will clear displayed logs and memory cache.",
                "Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _displayedLogs.Clear();
                LogManager.Instance.ClearLogs();
                TxtStatus.Text = "✅ Logs cleared";
                UpdateLogCount();
            }
        }

        private void CmbLogLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbLogLevel.SelectedItem is ComboBoxItem selectedItem)
            {
                string tag = selectedItem.Tag?.ToString();

                _selectedLogLevel = tag switch
                {
                    "Debug" => LogLevel.Debug,
                    "Information" => LogLevel.Information,
                    "Warning" => LogLevel.Warning,
                    "Error" => LogLevel.Error,
                    "Critical" => LogLevel.Critical,
                    _ => null
                };

                RefreshDisplay();
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchKeyword = TxtSearch.Text;
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            _displayedLogs.Clear();
            LoadExistingLogs();
        }

        private void BtnOpenLogFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = LogManager.Instance.LogFilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open log file:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// ViewModel for WPF binding
    /// </summary>
    public class LogEntryViewModel
    {
        private readonly LogEntry _logEntry;

        public LogEntryViewModel(LogEntry logEntry)
        {
            _logEntry = logEntry;
        }

        public string FormattedTimestamp => _logEntry.FormattedTimestamp;
        public string LevelText => _logEntry.LevelText;
        public string Source => _logEntry.Source;
        public string Message => _logEntry.Message;
        public LogLevel Level => _logEntry.Level;

        public System.Windows.Media.Brush LevelBrush
        {
            get
            {
                return _logEntry.Level switch
                {
                    LogLevel.Debug => System.Windows.Media.Brushes.Gray,
                    LogLevel.Information => System.Windows.Media.Brushes.DodgerBlue,
                    LogLevel.Warning => System.Windows.Media.Brushes.Orange,
                    LogLevel.Error => System.Windows.Media.Brushes.Red,
                    LogLevel.Critical => System.Windows.Media.Brushes.DarkRed,
                    _ => System.Windows.Media.Brushes.Black
                };
            }
        }
    }
}