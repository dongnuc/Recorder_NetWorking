using FluentAssertions;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Media;
using WpfUI;

namespace IntegrationTest.TestKitManagement
{
    [TestFixture]
    public class CreateTestCaseFolderTest : MainMenuTestBase
    {
        [Test]
        public void CreateTestCase_InsideExistingQuestion_ShouldCreateFolderAndTab()
        {
            // Arrange
            var mainMenu = InitializeMainMenu();
            mainMenu.Show();
            DoEvents();

            // 1. Tạo dữ liệu giả lập
            string parentQuestionName = "PreExistingQuestion";
            string parentQuestionPath = Path.Combine(_projectPath, parentQuestionName);
            Directory.CreateDirectory(parentQuestionPath);
            CreateEmptyFile(Path.Combine(parentQuestionPath, "Header.xlsx"));
            CreateEmptyFile(Path.Combine(parentQuestionPath, "Environment.xlsx"));

            // 2. Load ViewModel & Chọn Question
            _viewModel!.LoadFileTree();
            DoEvents();
            var questionItem = _viewModel.RootItems[0].Children.FirstOrDefault(x => x.Name == parentQuestionName);
            _viewModel.SelectedItem = questionItem;

            try
            {
              

                // Đợi 3 giây cho mọi thứ hoàn tất
                for (int i = 0; i < 30; i++) { DoEvents(); System.Threading.Thread.Sleep(100); }

                
            }
            finally
            {
                CloseWindowSafe(mainMenu);
            }
        }


        private T? FindChild<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
        {
            if (parent == null) return null;
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;
                var foundChild = FindChild<T>(child);
                if (foundChild != null) return foundChild;
            }
            return null;
        }

    }
}