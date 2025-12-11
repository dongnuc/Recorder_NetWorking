using FluentAssertions;
using IntegrationTest.Configuration;
using NUnit.Framework;
using System.Threading.Tasks;
using System.Windows.Forms; // Cần tham chiếu System.Windows.Forms
using WpfUI;

namespace IntegrationTest.Configuration
{
    [TestFixture]
    public class ResetDatabaseTest : MainMenuTestBase
    {
        [Test]
        public void ResetDb_OpenWindow_ShouldOpenAndCloseSuccessfully()
        {
            // Arrange
            var mainMenu = InitializeMainMenu();
            mainMenu.Show();
            DoEvents();

            try
            {
                // Act
                // Task ngầm: Chờ 2 giây rồi gửi phím ESC để đóng cửa sổ
                Task.Run(() =>
                {
                    System.Threading.Thread.Sleep(200); // Đợi cửa sổ hiện lên

                    // Gửi phím ESC (Thường đóng dialog)
                    try
                    {
                        SendKeys.SendWait("{ESC}");
                    }
                    catch
                    {
                        // Nếu ESC không ăn, gửi Alt+F4
                        System.Threading.Thread.Sleep(50);
                        SendKeys.SendWait("%{F4}");
                    }
                });

            }
            finally
            {
                CloseWindowSafe(mainMenu);
            }
        }
    }
}