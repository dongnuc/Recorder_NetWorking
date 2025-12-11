using FluentAssertions;
using NUnit.Framework;
using System.IO;
using System.Windows;
using WpfListBox = System.Windows.Controls.ListBox;
using WpfButton = System.Windows.Controls.Button;

namespace IntegrationTest.ProjectManagementTest
{
    [TestFixture]
    public class RemoveRecentProjectTest : ProjectExplorerTestBase
    {
        [Test]
        public void RemoveRecent_ClickButton_ShouldRemoveFromSettingsAndList()
        {
            string projName = "ProjectToRemove";
            CreateDummyProject(projName);
            string fullPath = Path.Combine(_testRootPath, projName);

            var window = InitializeWindow();
            window.Show();
            DoEvents();

            try
            {
                var listBox = GetPrivateField<WpfListBox>(window, "LstRecentProjects");
                listBox!.Items.Count.Should().Be(1);

                var fakeButton = new WpfButton { Tag = fullPath };

                // --- SỬA Ở ĐÂY ---
                // Thay vì new RoutedEventArgs(), hãy chỉ định rõ đây là sự kiện Click
                var args = new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent);

                InvokePrivateMethod(window, "BtnRemoveRecent_Click", fakeButton, args);

                DoEvents();

                listBox.Items.Count.Should().Be(0);
                IsPathInSettings(fullPath).Should().BeFalse();
            }
            finally
            {
                CloseWindowSafe(window);
            }
        }
    }
}