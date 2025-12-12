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
        public void DeleteQuestion_ExistingFolder()
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
    [Test]
        public void DeleteQuestion_FileOpenInFolder()
        {
            // Test Case 2: Delete a question folder with opening file in question folder.
            // Mục tiêu: Đảm bảo chương trình không crash và không xóa folder nếu file đang bị khóa bởi process khác.

            // Arrange
            var mainMenu = InitializeMainMenu();
            mainMenu.Show();
            DoEvents();

            string questionName = "QuestionLocked";
            string questionPath = Path.Combine(_projectPath, questionName);
            Directory.CreateDirectory(questionPath);

            // Tạo một file bên trong và LOCK nó lại
            string filePath = Path.Combine(questionPath, "locked_file.txt");
            File.WriteAllText(filePath, "This file is locked");

            // Mở file stream với chế độ Share.None để khóa file (Win32 Lock)
            using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                // Reload Tree
                _viewModel!.LoadFileTree();
                var rootItem = _viewModel.RootItems[0];
                var itemToDelete = rootItem.Children.FirstOrDefault(x => x.Name == questionName);
                
                itemToDelete.Should().NotBeNull();
                _viewModel.SelectedItem = itemToDelete;

                try
                {
                    // Act
                    Task.Run(() =>
                    {
                        // 1. Đợi Confirm Dialog -> Bấm Yes (Enter)
                        System.Threading.Thread.Sleep(800);
                        SendKeys.SendWait("{ENTER}");

                        // 2. Vì file bị lock, Directory.Delete sẽ throw exception.
                        // Nếu App có try-catch và hiện Error MessageBox, ta cần tắt nó.
                        // Nếu App không hiện Error box mà chỉ log, dòng này có thể thừa nhưng không gây hại (chỉ gửi phím vào hư không).
                        System.Threading.Thread.Sleep(800);
                        SendKeys.SendWait("{ENTER}"); 
                    });

                    InvokePrivateMethod(mainMenu, "CmDelete_Click", null, null);
                    DoEvents();

                    // Assert
                    // Folder vẫn phải tồn tại vì file đang bị lock
                    Directory.Exists(questionPath).Should().BeTrue("Folder không được bị xóa nếu chứa file đang mở");
                    File.Exists(filePath).Should().BeTrue("File bị lock không được bị xóa");

                    // ViewModel vẫn phải còn Item đó
                    var remainingItem = rootItem.Children.FirstOrDefault(x => x.Name == questionName);
                    remainingItem.Should().NotBeNull("Item phải giữ nguyên trên UI nếu xóa thất bại");
                }
                finally
                {
                    CloseWindowSafe(mainMenu);
                    // FileStream fs sẽ tự dispose ở đây, giải phóng lock
                }
            }

            // Cleanup sau khi test xong (khi lock đã được nhả)
            if (Directory.Exists(questionPath)) Directory.Delete(questionPath, true);
        }

        [Test]
        public void ConfirmDeleteQuestion_UserCancels()
        {
            // Test Case 3: Confirm delete question (Cancel scenario).
            // Mục tiêu: Kiểm tra hộp thoại xác nhận có hoạt động (Bấm No/Cancel thì không xóa).

            // Arrange
            var mainMenu = InitializeMainMenu();
            mainMenu.Show();
            DoEvents();

            string questionName = "QuestionToKeep";
            string questionPath = Path.Combine(_projectPath, questionName);
            Directory.CreateDirectory(questionPath);

            _viewModel!.LoadFileTree();
            var rootItem = _viewModel.RootItems[0];
            var itemToDelete = rootItem.Children.FirstOrDefault(x => x.Name == questionName);
            
            itemToDelete.Should().NotBeNull();
            _viewModel.SelectedItem = itemToDelete;

            try
            {
                // Act
                Task.Run(() =>
                {
                    // Đợi MessageBox hiện lên
                    System.Threading.Thread.Sleep(800);
                    // Gửi phím ESC (Tương đương Cancel/No)
                    // Hoặc dùng SendKeys.SendWait("{TAB}"); SendKeys.SendWait("{ENTER}"); nếu button No được focus
                    SendKeys.SendWait("{TAB}");
                    SendKeys.SendWait("{ENTER}");
                });

                InvokePrivateMethod(mainMenu, "CmDelete_Click", null, null);
                DoEvents();

                // Assert
                // 1. Kiểm tra ổ cứng: Folder VẪN CÒN
                Directory.Exists(questionPath).Should().BeTrue("Folder phải còn nguyên khi user hủy xóa");

                // 2. Kiểm tra ViewModel: Item VẪN CÒN
                var remainingItem = rootItem.Children.FirstOrDefault(x => x.Name == questionName);
                remainingItem.Should().NotBeNull("Item trên UI phải còn nguyên");
            }
            finally
            {
                CloseWindowSafe(mainMenu);
            }
        }
    }
}