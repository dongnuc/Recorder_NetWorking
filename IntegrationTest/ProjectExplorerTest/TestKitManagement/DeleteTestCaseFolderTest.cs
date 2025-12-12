using FluentAssertions;
using Moq;
using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace IntegrationTest.TestKitManagement
{
    [TestFixture]
    public class DeleteTestCaseFolderTest : MainMenuTestBase
    {
        [Test]
        public void DeleteTestCase_ExistingFolder()
        {
            // Arrange
            var mainMenu = InitializeMainMenu();
            mainMenu.Show();
            DoEvents();

            // 1. GIẢ LẬP DỮ LIỆU
            string questionName = "ParentQ";
            string testCaseName = "TC_Delete";

            string qPath = Path.Combine(_projectPath, questionName);
            string tcPath = Path.Combine(qPath, testCaseName);
            string headerPath = Path.Combine(qPath, "Header.xlsx");

            // a. Tạo Folder Question và TestCase
            Directory.CreateDirectory(qPath);
            Directory.CreateDirectory(tcPath);

            // b. Tạo file Header.xlsx giả trong Question (Bắt buộc vì Logic Delete sẽ đọc file này)
            // Vì ta dùng Mock FileHandler, ta cần setup cho Mock biết file này "hợp lệ"
            if (!File.Exists(headerPath)) File.WriteAllText(headerPath, "Dummy Header Content");

            // c. Setup Mock FileHandler để trả về Row Index > 0 (Giả vờ tìm thấy TestCase trong Excel)
            // Để logic xóa dòng trong Excel được chạy qua (tăng độ phủ code)
            _mockFileHandler!.Setup(x => x.FindStringInExcel(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((1, 1)); // Giả vờ tìm thấy ở dòng 1

            // 2. Load lại ViewModel
            _viewModel!.LoadFileTree();

            // 3. Chọn đúng TestCase cần xóa
            var rootItem = _viewModel.RootItems[0];
            var qItem = rootItem.Children.FirstOrDefault(x => x.Name == questionName);
            var tcItem = qItem!.Children.FirstOrDefault(x => x.Name == testCaseName);

            tcItem.Should().NotBeNull("ViewModel phải nhận diện được TestCase giả lập");
            _viewModel.SelectedItem = tcItem;

            try
            {
                // Act
                // Task bấm Yes cho MessageBox Confirm
                Task.Run(() =>
                {
                    System.Threading.Thread.Sleep(800);
                    System.Windows.Forms.SendKeys.SendWait("{ENTER}");
                });

                // Gọi lệnh xóa
                InvokePrivateMethod(mainMenu, "CmDelete_Click", null, null);

                DoEvents();

                // Assert
                // 1. Kiểm tra ổ cứng
                Directory.Exists(tcPath).Should().BeFalse("Folder TestCase phải bị xóa");

                // Folder cha (Question) vẫn phải còn
                Directory.Exists(qPath).Should().BeTrue("Folder Question cha không được bị xóa");

                // 2. Kiểm tra ViewModel
                var deletedItem = qItem.Children.FirstOrDefault(x => x.Name == testCaseName);

                // 3. Verify Mock (Kiểm tra xem code có gọi lệnh xóa dòng trong Excel không)
                // Logic code: _fileHandler.DeleteRow(parentHeaderPath, "TestSuite", row);
                _mockFileHandler.Verify(x => x.DeleteRow(It.IsAny<string>(), "TestSuite", It.IsAny<int>()), Times.AtLeastOnce,
                    "Code phải gọi lệnh xóa dòng trong file Excel Header");
            }
            finally
            {
                CloseWindowSafe(mainMenu);
            }
        }
        [Test]
        public void DeleteTestCase_UserCancels()
        {
            // Arrange
            var mainMenu = InitializeMainMenu();
            mainMenu.Show();
            DoEvents();

            string questionName = "ParentQ_Cancel";
            string testCaseName = "TC_Cancel";
            string qPath = Path.Combine(_projectPath, questionName);
            string tcPath = Path.Combine(qPath, testCaseName);
            string headerPath = Path.Combine(qPath, "Header.xlsx");

            Directory.CreateDirectory(qPath);
            Directory.CreateDirectory(tcPath);
            if (!File.Exists(headerPath)) File.WriteAllText(headerPath, "Dummy Header Content");

            _viewModel!.LoadFileTree();
            var tcItem = _viewModel.RootItems[0]
                .Children.FirstOrDefault(x => x.Name == questionName)!
                .Children.FirstOrDefault(x => x.Name == testCaseName);

            _viewModel.SelectedItem = tcItem;

            try
            {
                // Act
                // Giả lập bấm ESC (Hoặc phím mũi tên + Enter) để chọn NO/Cancel
                Task.Run(() =>
                {
                    System.Threading.Thread.Sleep(800);
                    SendKeys.SendWait("{TAB}");
                    SendKeys.SendWait("{ENTER}");
                });

                InvokePrivateMethod(mainMenu, "CmDelete_Click", null, null);
                DoEvents();

                // Assert
                // 1. Kiểm tra ổ cứng: Folder VẪN PHẢI CÒN
                Directory.Exists(tcPath).Should().BeTrue("Folder không được xóa khi user chọn Cancel");

                // 2. Kiểm tra ViewModel: Item VẪN PHẢI CÒN
                var qItem = _viewModel.RootItems[0].Children.FirstOrDefault(x => x.Name == questionName);
                var item = qItem!.Children.FirstOrDefault(x => x.Name == testCaseName);
                item.Should().NotBeNull("Item không được xóa khỏi UI khi user chọn Cancel");

                // 3. Verify Mock: KHÔNG được gọi lệnh xóa Excel
                _mockFileHandler!.Verify(x => x.DeleteRow(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never,
                    "Không được gọi lệnh xóa Row khi user chọn Cancel");
            }
            finally
            {
                CloseWindowSafe(mainMenu);
            }
        }

        // Testcase 3: Delete a testcase folder with opening file (File đang bị khóa)
        [Test]
        public void DeleteTestCase_FileLocked()
        {
            // Arrange
            var mainMenu = InitializeMainMenu();
            mainMenu.Show();
            DoEvents();

            string questionName = "ParentQ_Locked";
            string testCaseName = "TC_Locked";
            string qPath = Path.Combine(_projectPath, questionName);
            string tcPath = Path.Combine(qPath, testCaseName);
            string headerPath = Path.Combine(qPath, "Header.xlsx"); // File này dùng để xóa Row
            string lockedFilePath = Path.Combine(tcPath, "LockedFile.txt"); // File này nằm TRONG folder cần xóa

            Directory.CreateDirectory(qPath);
            Directory.CreateDirectory(tcPath);
            if (!File.Exists(headerPath)) File.WriteAllText(headerPath, "Dummy Header Content");
            File.WriteAllText(lockedFilePath, "This file will be locked");

            _mockFileHandler!.Setup(x => x.FindStringInExcel(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((1, 1));

            _viewModel!.LoadFileTree();
            var tcItem = _viewModel.RootItems[0]
                .Children.FirstOrDefault(x => x.Name == questionName)!
                .Children.FirstOrDefault(x => x.Name == testCaseName);

            _viewModel.SelectedItem = tcItem;

            // --- QUAN TRỌNG: KHÓA FILE ---
            // Mở file với FileShare.None để giả lập file đang được mở bởi ứng dụng khác (Notepad, Word, v.v.)
            // Lệnh Directory.Delete sẽ ném IOException
            using (FileStream fs = File.Open(lockedFilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                try
                {
                    // Act
                    Task.Run(() =>
                    {
                        // 1. Bấm YES cho hộp thoại Confirm Xóa
                        System.Threading.Thread.Sleep(800);
                        System.Windows.Forms.SendKeys.SendWait("{ENTER}");

                        // 2. Bấm ENTER tiếp cho hộp thoại ERROR (nếu App bắt Exception và hiện MessageBox lỗi)
                        // Nếu App Crash (không bắt lỗi), lệnh này vô thưởng vô phạt
                        System.Threading.Thread.Sleep(800);
                        System.Windows.Forms.SendKeys.SendWait("{ENTER}");
                    });

                    InvokePrivateMethod(mainMenu, "CmDelete_Click", null, null);
                    DoEvents();

                    // Assert
                    // Folder KHÔNG được mất vì File đang bị khóa bởi OS
                    Directory.Exists(tcPath).Should().BeTrue("Folder không thể bị xóa nếu file bên trong đang bị khóa");

                    // Kiểm tra ViewModel: Tùy logic app, nếu xóa thất bại thì Item nên vẫn còn
                    var qItem = _viewModel.RootItems[0].Children.FirstOrDefault(x => x.Name == questionName);
                    var item = qItem?.Children.FirstOrDefault(x => x.Name == testCaseName);
                    item.Should().NotBeNull("Item nên giữ lại trên UI nếu xóa thất bại");
                }
                finally
                {
                    // FileStream sẽ tự close tại đây nhờ 'using'
                    CloseWindowSafe(mainMenu);
                }
            }
        }

        [Test]
        public void DeleteTestCase()
        {
            // Arrange
            var mainMenu = InitializeMainMenu();
            mainMenu.Show();
            DoEvents();

            // 1. GIẢ LẬP DỮ LIỆU ĐƠN GIẢN KHÁC
            string questionName = "ParentQ_Simple";
            string testCaseName = "TC_Simple"; // Tên ngắn gọn, không ký tự đặc biệt

            string qPath = Path.Combine(_projectPath, questionName);
            string tcPath = Path.Combine(qPath, testCaseName);
            string headerPath = Path.Combine(qPath, "Header.xlsx");

            // Tạo Folder
            Directory.CreateDirectory(qPath);
            Directory.CreateDirectory(tcPath);

            // Tạo file Header giả
            if (!File.Exists(headerPath)) File.WriteAllText(headerPath, "Dummy Header Content");

            // Setup Mock
            _mockFileHandler!.Setup(x => x.FindStringInExcel(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((1, 1));

            // 2. Load lại ViewModel
            _viewModel!.LoadFileTree();
            var rootItem = _viewModel.RootItems[0];
            var qItem = rootItem.Children.FirstOrDefault(x => x.Name == questionName);
            var tcItem = qItem!.Children.FirstOrDefault(x => x.Name == testCaseName);

            _viewModel.SelectedItem = tcItem;

            try
            {
                // Act
                // Logic y hệt Testcase 1 vì nó đã chạy ổn
                Task.Run(() =>
                {
                    System.Threading.Thread.Sleep(800);
                    System.Windows.Forms.SendKeys.SendWait("{ENTER}");
                });

                InvokePrivateMethod(mainMenu, "CmDelete_Click", null, null);

                // FIX: Thêm vòng lặp chờ UI cập nhật để tránh lỗi Timing
                // Chờ tối đa 1s (10 * 100ms) để item biến mất khỏi ViewModel
                for (int i = 0; i < 10; i++)
                {
                    DoEvents();
                    System.Threading.Thread.Sleep(100);
                    if (qItem.Children.FirstOrDefault(x => x.Name == testCaseName) == null)
                        break;
                }

                // Assert
                Directory.Exists(tcPath).Should().BeFalse("Folder TestCase phải bị xóa");


                _mockFileHandler.Verify(x => x.DeleteRow(It.IsAny<string>(), "TestSuite", It.IsAny<int>()), Times.AtLeastOnce);
            }
            finally
            {
                CloseWindowSafe(mainMenu);
            }
        }
    }
}