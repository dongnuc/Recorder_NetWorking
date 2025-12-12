using NUnit.Framework;
using Moq; 
using Common.Interfaces.Logging;
using WpfUI;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices; 
using System.Windows.Threading; 

namespace IntegrationTest.Configuration
{
    [TestFixture]
    [Apartment(ApartmentState.STA)] 
    public class ResetDatabaseTest
    {
        private string _tempSqlFilePath;
        private const string TestConnectionString = "server=.\\SQLEXPRESS;database=LibraryTest;uid=sa;pwd=12345;TrustServerCertificate=True;Trusted_Connection=True;";

        [SetUp]
        public void Setup()
        {
            if (System.Windows.Application.Current == null)
            {
                new System.Windows.Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };
            }

            _tempSqlFilePath = Path.Combine(Path.GetTempPath(), "test_reset.sql");
            File.WriteAllText(_tempSqlFilePath, "SELECT 1; -- Script test an toàn");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tempSqlFilePath)) File.Delete(_tempSqlFilePath);
        }

        [Test]
        public void ResetDbTest()
        {
            var mockLogger = new Mock<ISystemLogger>();

            var window = new ResetDbWindow(mockLogger.Object);

            window.Show();

            DoEvents();
            Thread.Sleep(1000);

            SetPrivateField(window, "TxtSqlFilePath", _tempSqlFilePath);
            SetPrivateField(window, "TxtConnectionString", TestConnectionString);

            DoEvents();
            Thread.Sleep(1000);

            bool messageBoxAppeared = false;
            var popupTask = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    IntPtr hWnd = FindWindow(null, "Success"); 

                    if (hWnd != IntPtr.Zero)
                    {
                        messageBoxAppeared = true;
                        Thread.Sleep(1000); 
                        SendMessage(hWnd, 0x0010, IntPtr.Zero, IntPtr.Zero); 
                        return;
                    }
                    Thread.Sleep(100);
                }
            });

            InvokePrivateMethod(window, "BtnRunReset_Click", null, null);

            popupTask.Wait();

            window.Close();

            Assert.IsTrue(messageBoxAppeared, "Test thất bại: Không thấy MessageBox 'Success' hiện ra.");
            mockLogger.Verify(x => x.LogInfomation(It.Is<string>(s => s.Contains("Executing SQL script"))), Times.Once);
        }

        [Test]
        public void ResetDbTest_MissingSqlPath()
        {
            var mockLogger = new Mock<ISystemLogger>();
            var window = new ResetDbWindow(mockLogger.Object);
            window.Show();

            DoEvents();
            Thread.Sleep(1000);

            SetPrivateField(window, "TxtConnectionString", TestConnectionString);
            SetPrivateField(window, "TxtSqlFilePath", ""); 

            DoEvents();

            bool warningBoxAppeared = false;
            var popupTask = Task.Run(() =>
            {
                for (int i = 0; i < 50; i++) 
                {
                    IntPtr hWnd = FindWindow(null, "Warning");

                    if (hWnd != IntPtr.Zero)
                    {
                        warningBoxAppeared = true;
                        SendMessage(hWnd, 0x0010, IntPtr.Zero, IntPtr.Zero);
                        return;
                    }
                    Thread.Sleep(100);
                }
            });

            InvokePrivateMethod(window, "BtnRunReset_Click", null, null);

            popupTask.Wait();
            window.Close();

            Assert.IsTrue(warningBoxAppeared, "Test thất bại: Không thấy MessageBox 'Warning' hiện ra khi thiếu SQL Path.");
        }

        [Test]
        public void ResetDbTest_MissingConnectionString()
        {
            var mockLogger = new Mock<ISystemLogger>();
            var window = new ResetDbWindow(mockLogger.Object);
            window.Show();

            DoEvents();
            Thread.Sleep(1000);

            SetPrivateField(window, "TxtSqlFilePath", _tempSqlFilePath);
            SetPrivateField(window, "TxtConnectionString", "");

            DoEvents();

            InvokePrivateMethod(window, "BtnRunReset_Click", null, null);

            DoEvents();
            Thread.Sleep(2000);

            window.Close();

            mockLogger.Verify(x => x.LogError(It.Is<string>(s => s.Contains("Executing Sql fail") || s.Contains("fail"))), Times.Once,
                "Test thất bại: Khi thiếu ConnectionString, hệ thống phải log lỗi.");
        }

        private void DoEvents()
        {
            DispatcherFrame frame = new DispatcherFrame();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new DispatcherOperationCallback(ExitFrame),
                frame);
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }

        private object ExitFrame(object frame)
        {
            ((DispatcherFrame)frame).Continue = false;
            return null;
        }

        private void SetPrivateField(object target, string fieldName, string value)
        {
            if (target is Window window)
            {
                var control = window.FindName(fieldName) as System.Windows.Controls.TextBox;
                if (control != null)
                {
                    control.Text = value;
                    return;
                }
            }

            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
            {
                var textBox = field.GetValue(target) as System.Windows.Controls.TextBox;
                if (textBox != null) textBox.Text = value;
            }
            else
            {
                throw new Exception($"Không tìm thấy TextBox có tên {fieldName}");
            }
        }

        private void InvokePrivateMethod(object target, string methodName, object sender, RoutedEventArgs e)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(target, new object[] { sender, e });
            }
            else
            {
                throw new Exception($"Không tìm thấy hàm {methodName}");
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);
    }
}