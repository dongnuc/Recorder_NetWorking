using FluentAssertions;
using IntegrationTest.TestKitManagement;
using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WpfUI.ViewModels;

namespace IntegrationTest.TestKitManagement
{
    [TestFixture]
    public class CreateQuestionFolderTest : MainMenuTestBase
    {
        [Test]
        public void CreateQuestion_ValidName_ShouldCreateFolderAndUpdateViewModel()
        {
            // Arrange
            var mainMenu = InitializeMainMenu();
            mainMenu.Show();
            DoEvents();

            var rootItem = _viewModel!.RootItems[0];
            _viewModel.SelectedItem = rootItem;

            try
            {
                // Act
                Task.Run(() =>
                {
                    // 1. Đợi cửa sổ hiện lên
                    System.Threading.Thread.Sleep(1500);

                    // 2. Nhập tên
                    System.Windows.Forms.SendKeys.SendWait("NewQuestion1");
                    System.Threading.Thread.Sleep(500);

                    // 3. Nhấn TAB để di chuyển focus
                    // Cập nhật: Nhấn TAB 3 lần theo yêu cầu của bạn

                    // Tab 1
                    System.Windows.Forms.SendKeys.SendWait("{TAB}");
                    System.Threading.Thread.Sleep(200);

                    // Tab 2
                    System.Windows.Forms.SendKeys.SendWait("{TAB}");
                    System.Threading.Thread.Sleep(200);

                    // Tab 3 (THÊM MỚI Ở ĐÂY)
                    System.Windows.Forms.SendKeys.SendWait("{TAB}");
                    System.Threading.Thread.Sleep(200);

                    // 4. Nhấn Enter để kích hoạt nút Create
                    System.Windows.Forms.SendKeys.SendWait("{ENTER}");
                });

                // Gọi event click để mở dialog (Code sẽ dừng ở đây chờ Dialog đóng)
                InvokePrivateMethod(mainMenu, "CmCreateQuestion_Click", null, null);

                DoEvents();

                // Assert
                string expectedPath = Path.Combine(_projectPath, "NewQuestion1");
                Directory.Exists(expectedPath).Should().BeTrue("Folder Question phải được tạo trên ổ cứng");
                
            }
            finally
            {
                CloseWindowSafe(mainMenu);
            }
        }
    }
}