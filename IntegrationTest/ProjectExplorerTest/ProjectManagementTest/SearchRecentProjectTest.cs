using FluentAssertions;
using NUnit.Framework;

// Alias để tránh nhầm lẫn với WinForms
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfListBox = System.Windows.Controls.ListBox;
using WpfButton = System.Windows.Controls.Button;

namespace IntegrationTest.ProjectManagementTest
{
    [TestFixture]
    public class SearchRecentProjectTest : ProjectExplorerTestBase
    {
        [Test]
        public void SearchProject_ValidKeyword_ShouldFilterList()
        {
            // Arrange
            CreateDummyProject("AlphaProject");
            CreateDummyProject("BetaProject");
            CreateDummyProject("GammaProject");

            var window = InitializeWindow();
            window.Show();
            DoEvents();

            try
            {
                // Act: Nhập từ khóa "Beta"
                SetPrivateTextBoxValue(window, "TxtSearch", "Beta");

                // Click nút Search
                InvokePrivateMethod(window, "BtnSearch_Click", null, null);
                DoEvents();

                // Assert
                var listBox = GetPrivateField<WpfListBox>(window, "LstRecentProjects");
                listBox!.Items.Count.Should().Be(1, "Should filter to 1 item");

                var item = listBox.Items[0] as WpfUI.RecentProjectItem;
                item!.Name.Should().Be("BetaProject");
            }
            finally
            {
                // QUAN TRỌNG: Dùng CloseWindowSafe để tránh tắt luôn Test Runner
                CloseWindowSafe(window);
            }
        }

        [Test]
        public void SearchProject_EmptyKeyword_ShouldShowAll()
        {
            // Arrange
            CreateDummyProject("AlphaProject");
            CreateDummyProject("BetaProject");

            var window = InitializeWindow();
            window.Show();
            DoEvents();

            try
            {
                // Act 1: Search "Alpha" trước để lọc danh sách
                SetPrivateTextBoxValue(window, "TxtSearch", "Alpha");
                InvokePrivateMethod(window, "BtnSearch_Click", null, null);
                DoEvents();

                // Kiểm tra sơ bộ
                var listBox = GetPrivateField<WpfListBox>(window, "LstRecentProjects");
                listBox!.Items.Count.Should().Be(1);

                // Act 2: Xóa search (Empty)
                SetPrivateTextBoxValue(window, "TxtSearch", "");
                InvokePrivateMethod(window, "BtnSearch_Click", null, null);
                DoEvents();

                // Assert: Phải hiện lại đủ 2 item
                listBox.Items.Count.Should().Be(2, "Empty search should return full list");
            }
            finally
            {
                // QUAN TRỌNG: Dùng CloseWindowSafe
                CloseWindowSafe(window);
            }
        }
    }
}