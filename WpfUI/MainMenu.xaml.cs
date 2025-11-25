using Common.Interfaces.IOFile;
using Common.Interfaces.Services;
using Common.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfUI.Dialogs;
using WpfUI.Properties;
using WpfUI.Services;
using WpfUI.ViewModels;


namespace WpfUI
{
    public class TabItemData
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string TestCaseName { get; set; } = string.Empty;
        public TabItem TabItem { get; set; }
        public RecorderWindow RecorderWindow { get; set; }
    }


    public partial class MainMenu : Window
    {
        private MainMenuViewModel _viewModel;
        private readonly IOFileManagement _fileManager;

        private Dictionary<Guid, TabItemData> _activeTabs = new Dictionary<Guid, TabItemData>();
        private IOFileHandler _fileHandler;
        private readonly IServiceProvider _serviceProvider;

        public MainMenu(MainMenuViewModel viewModel,
            IOFileHandler fileHandler,
            IServiceProvider serviceProvider,
            IOFileManagement fileManager)
        {
            InitializeComponent();
            _viewModel = viewModel;
            this.DataContext = _viewModel;
            _fileHandler = fileHandler;
            _serviceProvider = serviceProvider;
            _fileManager = fileManager;
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

                await CreateRecorderTab(testcasePath, testcaseName, clientPath, serverPath, isHttp);
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

        #region recorder tab (Code cũ của bạn, giữ nguyên)
        private async Task CreateRecorderTab(string testcasePath, string testcaseName, string clientPath, string serverPath, bool isHttp)
        {
            RecorderWindowScope scope = null;
            try
            {
                Guid tabId = Guid.NewGuid();

                scope = new RecorderWindowScope(_serviceProvider);
                var processManager = scope.ServiceProvider.GetRequiredService<IProcessManager>();
                var testkitManagerSerive = scope.ServiceProvider.GetRequiredService<ITestkitManagerService>();
                var recorderWindow = new RecorderWindow(testcasePath, testcaseName, clientPath,
                    serverPath, isHttp,
                    processManager, _fileHandler, testkitManagerSerive);

                var windowContent = recorderWindow.Content as FrameworkElement;
                recorderWindow.Content = null;

                var tabItem = new TabItem
                {
                    Header = testcaseName,
                    Tag = tabId,
                    Content = windowContent
                };

                if (windowContent != null)
                {
                    windowContent.DataContext = recorderWindow;
                }

                var tabData = new TabItemData
                {
                    Id = tabId,
                    TestCaseName = testcaseName,
                    TabItem = tabItem,
                    RecorderWindow = recorderWindow,
                };

                _activeTabs[tabId] = tabData;

                if (_activeTabs.Count == 1)
                {
                    WelcomeTab.Visibility = Visibility.Collapsed;
                }

                TestCaseTabControl.Items.Add(tabItem);
                TestCaseTabControl.SelectedItem = tabItem;
                await recorderWindow.InitializeAsync();
            }
            catch (Exception ex)
            {
                LogManager.Instance?.LogError($" Error creating tab: {ex.Message}");
            }
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current == null || Application.Current.MainWindow == null) return;
            if (!this.IsLoaded) return;
            var button = sender as Button;
            if (button?.Tag is TabItem tabItem && tabItem.Tag is Guid tabId)
            {
                if (_activeTabs.TryGetValue(tabId, out var tabData))
                {
                    var result = MessageBox.Show(
                            $"Bạn có chắc muốn đóng test case '{tabData.TestCaseName}'?\n\nDữ liệu chưa lưu sẽ bị mất.",
                            "Xác nhận đóng",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        CloseTabById(tabId);
                    }
                }
            }
        }

        private async void CloseTabById(Guid id)
        {
            try
            {
                if (_activeTabs.TryGetValue(id, out var tabData))
                {
                    if (tabData.RecorderWindow != null)
                    {
                        try
                        {
                            await tabData.RecorderWindow.CleanupAsync();
                            LogManager.Instance?.LogDebug("RecorderWindow cleanup completed");
                        }
                        catch (Exception ex)
                        {
                            LogManager.Instance?.LogWarning($"Error cleaning up RecorderWindow: {ex.Message}");
                        }
                    }

                    TestCaseTabControl.Items.Remove(tabData.TabItem);
                    _activeTabs.Remove(id);

                    if (_activeTabs.Count == 0)
                    {
                        WelcomeTab.Visibility = Visibility.Visible;
                        TestCaseTabControl.SelectedItem = WelcomeTab;
                    }

                    LogManager.Instance?.LogInfomation($" Tab closed successfully: {tabData.TestCaseName}");
                }
                else
                {
                    LogManager.Instance?.LogWarning($"Attempted to close non-existent tab with ID: {id}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Instance?.LogError($" Error closing tab (ID: {id}): {ex.Message}");
                MessageBox.Show($"Error closing tab:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private RecorderWindow GetCurrentRecorderWindowns()
        {
            var selectedTab = TestCaseTabControl.SelectedItem as TabItem;
            if (selectedTab?.Tag is Guid tabId)
            {
                if (_activeTabs.TryGetValue(tabId, out var tabData))
                {
                    return tabData.RecorderWindow;
                }
            }
            return null;
        }

        private void TestCaseTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedTab = TestCaseTabControl.SelectedItem as TabItem;
            if (selectedTab?.Tag is Guid tabId)
            {
                if (_activeTabs.TryGetValue(tabId, out var tabData))
                {
                    // (Logic khi chọn tab)
                }
            }
        }

        #endregion
    }
}