using FluentAssertions;
using IntegrationTest.TestKitManagement;
using Moq;
using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms; 
namespace IntegrationTest.TestKitManagement
{
    [TestFixture]
    public class CreateQuestionFolderTest : MainMenuTestBase
    {
        private const int DELAY_UI_WAIT = 1000; 
        private const int DELAY_ACTION = 500;   
        private const int DELAY_MSG_BOX = 1000; 

        [Test]
        public void CreateQuestion_WithRequiredTemplates()
        {
            var mainMenu = InitializeMainMenu();
            mainMenu.Show();
            DoEvents();

            var rootItem = _viewModel!.RootItems[0];
            _viewModel.SelectedItem = rootItem;

            string newFolderName = "QuestionWithTemplates";

            _mockFolderHandler!.Setup(x => x.CreateDirectory(It.IsAny<string>(), newFolderName, It.IsAny<string[]>()))
                .Returns((string parent, string name, string[] subs) =>
                {
                    string fullPath = Path.Combine(_projectPath, name);
                    if (!Directory.Exists(fullPath))
                    {
                        Directory.CreateDirectory(fullPath);
                    }
                    return fullPath;
                });

            _mockFolderHandler.Setup(x => x.CopyTemplateFromResource(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string[]>()))
                .Callback((string destPath, string srcDir, bool overwrite, string[] files) =>
                {
                    if (!Directory.Exists(destPath)) Directory.CreateDirectory(destPath);
                    foreach (var file in files)
                    {
                        string filePath = Path.Combine(destPath, file);
                        File.WriteAllText(filePath, "Dummy Content for Test");
                    }
                });

            try
            {
                Task.Run(() =>
                {
                    System.Threading.Thread.Sleep(DELAY_UI_WAIT);

                    SendKeys.SendWait(newFolderName);
                    System.Threading.Thread.Sleep(DELAY_ACTION);

                    SendKeys.SendWait("{TAB}");
                    System.Threading.Thread.Sleep(200);
                    SendKeys.SendWait("{TAB}");
                    System.Threading.Thread.Sleep(200);
                    SendKeys.SendWait("{TAB}");
                    System.Threading.Thread.Sleep(200);

                    SendKeys.SendWait("{ENTER}");
                });

                InvokePrivateMethod(mainMenu, "CmCreateQuestion_Click", null, null);
                DoEvents();

                string expectedFolderPath = Path.Combine(_projectPath, newFolderName);

                _mockFolderHandler!.Verify(x => x.CreateDirectory(
                    It.IsAny<string>(),
                    newFolderName,
                    It.IsAny<string[]>())
                    );

                Directory.Exists(expectedFolderPath).Should().BeTrue("Folder Question phải được tạo trên ổ cứng sau khi Mock thực thi");

                
            }
            finally
            {
                CloseWindowSafe(mainMenu);
            }
        }

        [Test]
        public void CreateQuestion_EmptyName()
        {
            var mainMenu = InitializeMainMenu();
            mainMenu.Show();
            DoEvents();

            var rootItem = _viewModel!.RootItems[0];
            _viewModel.SelectedItem = rootItem;
            int initialCount = rootItem.Children.Count;

            try
            {
                Task.Run(() =>
                {
                    System.Threading.Thread.Sleep(DELAY_UI_WAIT);

                    SendKeys.SendWait("{TAB}");
                    System.Threading.Thread.Sleep(200);
                    SendKeys.SendWait("{TAB}");
                    System.Threading.Thread.Sleep(200);
                    SendKeys.SendWait("{TAB}");
                    System.Threading.Thread.Sleep(200);

                    SendKeys.SendWait("{ENTER}");

                    System.Threading.Thread.Sleep(DELAY_MSG_BOX);

                    SendKeys.SendWait("{ENTER}");
                    System.Threading.Thread.Sleep(DELAY_ACTION);

                    SendKeys.SendWait("{ESC}");
                });

                InvokePrivateMethod(mainMenu, "CmCreateQuestion_Click", null, null);
                DoEvents();

                var directories = Directory.GetDirectories(_projectPath);
                directories.Should().NotContain(d => string.IsNullOrEmpty(new DirectoryInfo(d).Name));

                rootItem.Children.Count.Should().Be(initialCount, "Số lượng item không được thay đổi khi nhập tên rỗng");
            }
            finally
            {
                CloseWindowSafe(mainMenu);
            }
        }

        [Test]
        public void CreateQuestion_ExistedName()
        {
            var mainMenu = InitializeMainMenu();
            mainMenu.Show();
            DoEvents();

            var rootItem = _viewModel!.RootItems[0];
            _viewModel.SelectedItem = rootItem;

            string existingName = "DuplicateQuestion";
            string existingPath = Path.Combine(_projectPath, existingName);
            Directory.CreateDirectory(existingPath);

            int initialCount = Directory.GetDirectories(_projectPath).Length;

            try
            {
                Task.Run(() =>
                {
                    System.Threading.Thread.Sleep(DELAY_UI_WAIT);

                    SendKeys.SendWait(existingName);
                    System.Threading.Thread.Sleep(DELAY_ACTION);

                    SendKeys.SendWait("{TAB}");
                    System.Threading.Thread.Sleep(200);
                    SendKeys.SendWait("{TAB}");
                    System.Threading.Thread.Sleep(200);
                    SendKeys.SendWait("{TAB}");
                    System.Threading.Thread.Sleep(200);

                    SendKeys.SendWait("{ENTER}");

                    System.Threading.Thread.Sleep(DELAY_MSG_BOX);

                    SendKeys.SendWait("{ENTER}");
                    System.Threading.Thread.Sleep(DELAY_ACTION);

                    SendKeys.SendWait("{ESC}");
                });

                InvokePrivateMethod(mainMenu, "CmCreateQuestion_Click", null, null);
                DoEvents();

                Directory.GetDirectories(_projectPath).Length.Should().Be(initialCount, "Không được tạo thêm folder nếu tên đã tồn tại");
            }
            finally
            {
                CloseWindowSafe(mainMenu);
                if (Directory.Exists(existingPath)) Directory.Delete(existingPath, true);
            }
        }
    }
}