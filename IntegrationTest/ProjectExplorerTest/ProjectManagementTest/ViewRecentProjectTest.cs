using FluentAssertions;
using NUnit.Framework;
using WpfUI;
using WpfListBox = System.Windows.Controls.ListBox;

namespace IntegrationTest.ProjectManagementTest
{
    [TestFixture]
    public class ViewRecentProjectTest : ProjectExplorerTestBase
    {
        [Test]
        public void ViewRecentProjects()
        {
            CreateDummyProject("ProjectA");
            CreateDummyProject("ProjectB"); 

            var window = InitializeWindow();

            try
            {
                window.Show();
                DoEvents();

                var listBox = GetPrivateField<WpfListBox>(window, "LstRecentProjects");
                listBox.Should().NotBeNull();
                listBox!.Items.Count.Should().Be(2);

                var firstItem = listBox.Items[0] as RecentProjectItem;

                firstItem!.Name.Should().Be("ProjectB", "The most recently added project should appear at the top");

                var secondItem = listBox.Items[1] as RecentProjectItem;
                secondItem!.Name.Should().Be("ProjectA");
            }
            finally
            {
                CloseWindowSafe(window);
            }
        }

        [Test]
        public void ViewRecentProjects_WhenFolderMissing()
        {
            CreateDummyProject("ExistingProject");
            AddProjectToSettings(@"C:\NonExistent\Path\FakeProject");

            var window = InitializeWindow();

            try
            {
                window.Show();
                DoEvents();

                var listBox = GetPrivateField<WpfListBox>(window, "LstRecentProjects");
                listBox!.Items.Count.Should().Be(1);

                var item = listBox.Items[0] as RecentProjectItem;
                item!.Name.Should().Be("ExistingProject");
            }
            finally
            {
                CloseWindowSafe(window);
            }
        }
    }
}