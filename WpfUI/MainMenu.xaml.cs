using Common.Interfaces.IOFile;
using Common.Logging;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
        public MainMenu(MainMenuViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            this.DataContext = _viewModel;
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
                //Win.Content = fileDetailTextBlock;
            }
        }

        private async void BtnCreateTestCase_Click(object sender, RoutedEventArgs e)
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
                var testcasePath = _viewModel?.CreateNewTestCase(dialog.InputText);
                var testcaseName = dialog.InputText.ToString();
                LogManager.Instance.LogInfomation(dialog.InputText.ToString());
               await CreateRecorderTab(testcasePath,testcaseName, _viewModel._clientExePath, _viewModel._serverExePath,_viewModel._isHtpp);
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
        #region recorder tab
        private async Task CreateRecorderTab(string testcasePath, string testcaseName,string clientPath,string serverPath,bool isHttp)
        {
            try
            {
                Guid tabId = Guid.NewGuid();
                var recorderWindow = new RecorderWindow(testcasePath,testcaseName, clientPath, serverPath,isHttp);

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
                TestCaseTabControl.Items.Add(tabItem);
                TestCaseTabControl.SelectedItem = tabItem;
                await recorderWindow.InitializeAsync();
            }
            catch (Exception ex)
            {
            }
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if(button?.Tag is TabItem tabItem && tabItem.Tag is Guid tabId)
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
                    LogManager.Instance?.LogInfomation($" Closing tab: {tabData.TestCaseName} (ID: {id})");

                    if (tabData.RecorderWindow != null)
                    {
                        try
                        {
                            LogManager.Instance?.LogDebug("Calling RecorderWindow.CleanupAsync()");
                            await tabData.RecorderWindow.CleanupAsync();
                            LogManager.Instance?.LogDebug("RecorderWindow cleanup completed");
                        }
                        catch (Exception ex)
                        {
                            LogManager.Instance?.LogWarning($"Error cleaning up RecorderWindow: {ex.Message}");
                        }
                    }

                    // Remove from UI
                    TestCaseTabControl.Items.Remove(tabData.TabItem);

                    // Remove from dictionary
                    _activeTabs.Remove(id);

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
            if(selectedTab?.Tag is Guid tabId)
            {
                if(_activeTabs.TryGetValue(tabId, out var tabData))
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
                }
            }
        }

        #endregion
    }
}