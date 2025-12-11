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
        public void DeleteTestCase_ExistingFolder_ShouldRemoveFromDiskAndViewModel()
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
    }
}