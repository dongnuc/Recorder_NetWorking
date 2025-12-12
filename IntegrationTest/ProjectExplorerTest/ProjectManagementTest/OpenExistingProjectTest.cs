using FluentAssertions;
using NUnit.Framework;
using System.IO;
using WpfUI;
using WpfListBox = System.Windows.Controls.ListBox;

namespace IntegrationTest.ProjectManagementTest
{
    [TestFixture]
    public class OpenExistingProjectTest : ProjectExplorerTestBase
    {
        [Test]
        public void OpenProject_SelectFromList()
        {
            string projName = "TargetProject";
            CreateDummyProject(projName);
            string fullPath = Path.Combine(_testRootPath, projName);

            var window = InitializeWindow();
            window.Show();
            DoEvents();

            try
            {
                var listBox = GetPrivateField<WpfListBox>(window, "LstRecentProjects");
                var itemToSelect = listBox!.Items[0] as RecentProjectItem;

                listBox.SelectedItem = itemToSelect;
                InvokePrivateMethod(window, "LstRecentProjectsOpen", listBox, null);
                DoEvents();

                GetCurrentProjectPathFromSettings().Should().Be(fullPath);

                window.Visibility.Should().Be(System.Windows.Visibility.Hidden);
            }
            finally
            {
                window.Close();
            }
        }

    }
}