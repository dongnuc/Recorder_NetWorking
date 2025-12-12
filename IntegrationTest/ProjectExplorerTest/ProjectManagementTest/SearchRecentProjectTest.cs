using FluentAssertions;
using NUnit.Framework;

using WpfTextBox = System.Windows.Controls.TextBox;
using WpfListBox = System.Windows.Controls.ListBox;
using WpfButton = System.Windows.Controls.Button;

namespace IntegrationTest.ProjectManagementTest
{
    [TestFixture]
    public class SearchRecentProjectTest : ProjectExplorerTestBase
    {
        [Test]
        public void SearchProject()
        {
            CreateDummyProject("AlphaProject");
            CreateDummyProject("BetaProject");
            CreateDummyProject("GammaProject");

            var window = InitializeWindow();
            window.Show();
            DoEvents();

            try
            {
                SetPrivateTextBoxValue(window, "TxtSearch", "Beta");

                InvokePrivateMethod(window, "BtnSearch_Click", null, null);
                DoEvents();

                var listBox = GetPrivateField<WpfListBox>(window, "LstRecentProjects");
                listBox!.Items.Count.Should().Be(1, "Should filter to 1 item");

                var item = listBox.Items[0] as WpfUI.RecentProjectItem;
                item!.Name.Should().Be("BetaProject");
            }
            finally
            {
                CloseWindowSafe(window);
            }
        }

        [Test]
        public void SearchProject_EmptyKeyword()
        {
            CreateDummyProject("AlphaProject");
            CreateDummyProject("BetaProject");

            var window = InitializeWindow();
            window.Show();
            DoEvents();

            try
            {
                SetPrivateTextBoxValue(window, "TxtSearch", "Alpha");
                InvokePrivateMethod(window, "BtnSearch_Click", null, null);
                DoEvents();

                var listBox = GetPrivateField<WpfListBox>(window, "LstRecentProjects");
                listBox!.Items.Count.Should().Be(1);

                SetPrivateTextBoxValue(window, "TxtSearch", "");
                InvokePrivateMethod(window, "BtnSearch_Click", null, null);
                DoEvents();

                listBox.Items.Count.Should().Be(2, "Empty search should return full list");
            }
            finally
            {
                CloseWindowSafe(window);
            }
        }
    }
}