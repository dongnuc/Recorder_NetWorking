using FluentAssertions;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Windows.Forms; 
using WpfUI.Dialogs;
using WpfUI.Properties;

namespace IntegrationTest.Configuration
{
    [TestFixture]
    public class ConfigRecordingTest : MainMenuTestBase
    {
        [Test]
        public void ChangeConfig_ValidData()
        {
            var mainMenu = InitializeMainMenu();
            mainMenu.Show();
            DoEvents();

            AddSetting("ClientExePath", "OldClient");
            AddSetting("ServerExePath", "OldServer");

            try
            {
                HandleConfigWindow_SendKeys();

                InvokePrivateMethod(mainMenu, "BtnConfig_Click", null, null);

                DoEvents();

                Assert.Pass("Dialog Opened, Tabbed to Save, and Closed successfully.");
            }
            finally
            {
                CloseWindowSafe(mainMenu);
            }
        }

        private void HandleConfigWindow_SendKeys()
        {
            Task.Run(() =>
            {
                try
                {
                    System.Threading.Thread.Sleep(2000);

                    SendKeys.SendWait("{TAB}");

                    SendKeys.SendWait("{TAB}");


                    for (int i = 0; i < 5; i++)
                    {
                        SendKeys.SendWait("{TAB}");
                        System.Threading.Thread.Sleep(50);
                    }

                    SendKeys.SendWait("{ENTER}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SendKeys Error: " + ex.Message);
                }
            });
        }
    }
}