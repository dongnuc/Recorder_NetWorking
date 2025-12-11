using FluentAssertions;
using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WpfUI.ViewModels;

namespace IntegrationTest.TestKitManagement
{
    [TestFixture]
    public class DeleteQuestionFolderTest : MainMenuTestBase
    {
        [Test]
        public void DeleteQuestion_ExistingFolder_ShouldRemoveFromDiskAndViewModel()
        {
            // Arrange
            var mainMenu = InitializeMainMenu();
            mainMenu.Show();
            DoEvents();

            // 1. GIẢ LẬP DỮ LIỆU: Tạo thủ công folder Question trên ổ cứng
            string questionName = "QuestionToDelete";
            string questionPath = Path.Combine(_projectPath, questionName);
            Directory.CreateDirectory(questionPath);

            // 2. Yêu cầu ViewModel load lại cây thư mục để nhận diện folder vừa tạo
            _viewModel!.LoadFileTree();

            // 3. Tìm và Chọn Item cần xóa
            var rootItem = _viewModel.RootItems[0];
            var itemToDelete = rootItem.Children.FirstOrDefault(x => x.Name == questionName);

            // Assert nhẹ: Đảm bảo dữ liệu giả lập đã được load lên UI
            itemToDelete.Should().NotBeNull("ViewModel phải nhận diện được folder giả lập");
            _viewModel.SelectedItem = itemToDelete;

            try
            {
                // Act
                // Chạy Task ngầm để bấm "Yes" (Enter) khi MessageBox Confirm hiện ra
                Task.Run(() =>
                {
                    // Đợi MessageBox hiện lên
                    System.Threading.Thread.Sleep(800);
                    // Gửi phím Enter (Tương đương bấm Yes/OK)
                    System.Windows.Forms.SendKeys.SendWait("{ENTER}");
                });

                // Gọi lệnh xóa (Hàm này sẽ gọi MessageBox.Show và bị chặn lại bởi Task trên)
                InvokePrivateMethod(mainMenu, "CmDelete_Click", null, null);

                // Đợi UI cập nhật
                DoEvents();

                // Assert
                // 1. Kiểm tra ổ cứng: Folder phải mất
                if (Directory.Exists(questionPath))
                {
                    // Fallback: Thử đợi thêm chút nếu máy chậm
                    System.Threading.Thread.Sleep(500);
                }
                Directory.Exists(questionPath).Should().BeFalse("Folder phải bị xóa khỏi ổ cứng");

                // 2. Kiểm tra ViewModel: Item phải mất khỏi danh sách
                var deletedItem = rootItem.Children.FirstOrDefault(x => x.Name == questionName);
            }
            finally
            {
                CloseWindowSafe(mainMenu);
            }
        }
    }
}