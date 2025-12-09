// WpfUI/Controls/LogViewerControl.xaml.cs
using Common.Interfaces.Logging;
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
        private LogManager? _logManager;
        private LogLevel? _selectedLogLevel = null;
        private string _searchKeyword = string.Empty;

        /// <summary>
        /// Constructor with dependency injection for LogManager
        /// </summary>
        public LogViewerControl(ISystemLogger logger)
        {
            InitializeComponent();

            // Bind ListView
            LstLogs.ItemsSource = _displayedLogs;

            // Cast ISystemLogger to LogManager to access event and specific features
            var logManager = logger as LogManager ?? throw new ArgumentException("Logger must be LogManager instance", nameof(logger));

            InitializeLogManager(logManager);
        }

        /// <summary>
        /// Parameterless constructor for XAML designer ONLY
        /// DO NOT USE in production code - use DI constructor instead
        /// </summary>
        public LogViewerControl()
        {
            InitializeComponent();

            // Bind ListView
            LstLogs.ItemsSource = _displayedLogs;

            // For design-time only - will be replaced by DI in runtime
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            {
                // Create a temporary instance for designer
                InitializeLogManager(new LogManager());
            }
            else
            {
                // This should not happen in production - log a warning
                Debug.WriteLine("⚠️ WARNING: LogViewerControl created without logger injection!");
                // Create fallback instance
                InitializeLogManager(new LogManager());
            }
        }

        /// <summary>
        /// Common initialization logic for LogManager
        /// </summary>
        private void InitializeLogManager(LogManager logManager)
        {
            _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));

            // Load existing logs
            LoadExistingLogs();

            // Subscribe to new log events
            _logManager.OnLogAdded += OnLogAdded;

            // Unsubscribe when unloaded
            Unloaded += OnUnloaded;

            // Show log file path and session info
            if (TxtLogFilePath != null)
            {
                TxtLogFilePath.Text = $"📄 {System.IO.Path.GetFileName(_logManager.LogFilePath)} (Session: {_logManager.SessionId})";
            }

            UpdateLogCount();
        }

        /// <summary>
        /// Cleanup event subscriptions
        /// </summary>
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_logManager is not null)
            {
                _logManager.OnLogAdded -= OnLogAdded;
            }
        }

        /// <summary>
        /// Load existing logs from LogManager
        /// </summary>
        private void LoadExistingLogs()
        {
            if (_logManager is null)
            {
                Debug.WriteLine("⚠️ LogManager is null in LoadExistingLogs");
                return;
            }

            var existingLogs = _logManager.GetAllLogs();
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
            if (TxtLogCount != null)
            {
                TxtLogCount.Text = $"{_displayedLogs.Count} logs";
            }
        }

        private void AutoScrollIfEnabled()
        {
            if (ChkAutoScroll?.IsChecked == true && LstLogs?.Items.Count > 0)
            {
                LstLogs.ScrollIntoView(LstLogs.Items[LstLogs.Items.Count - 1]);
            }
        }

        private void BtnClearLogs_Click(object sender, RoutedEventArgs e)
        {
            if (_logManager is null)
            {
                MessageBox.Show("LogManager is not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var result = MessageBox.Show(
                "Clear all logs?\n\nThis will clear displayed logs and memory cache.",
                "Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _displayedLogs.Clear();
                _logManager.ClearLogs();
                if (TxtStatus != null)
                {
                    TxtStatus.Text = "✅ Logs cleared";
                }
                UpdateLogCount();
            }
        }

        private void CmbLogLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbLogLevel?.SelectedItem is ComboBoxItem selectedItem)
            {
                string? tag = selectedItem.Tag?.ToString();

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
            _searchKeyword = TxtSearch?.Text ?? string.Empty;
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            _displayedLogs.Clear();
            LoadExistingLogs();
        }

        private void BtnOpenLogFile_Click(object sender, RoutedEventArgs e)
        {
            if (_logManager is null)
            {
                MessageBox.Show("LogManager is not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = _logManager.LogFilePath,
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