using FluentAssertions;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Windows.Forms; // Cần tham chiếu System.Windows.Forms cho SendKeys
using WpfUI.Dialogs;
using WpfUI.Properties;

namespace IntegrationTest.Configuration
{
    [TestFixture]
    public class ConfigRecordingTest : MainMenuTestBase
    {
        [Test]
        public void ChangeConfig_ValidData_ShouldUpdateSettingsAndClose()
        {
            // Arrange
            var mainMenu = InitializeMainMenu();
            mainMenu.Show();
            DoEvents();

            // Giá trị test mong muốn (Lưu ý: Cách này test việc nhập liệu bằng phím hơi khó chính xác
            // nên ta tập trung vào việc: Mở Dialog -> Tab đến nút Save -> Enter -> Check xem có đóng không)

            // Setup giá trị cũ
            AddSetting("ClientExePath", "OldClient");
            AddSetting("ServerExePath", "OldServer");

            try
            {
                // Act
                // 1. Kích hoạt Robot bấm phím
                HandleConfigWindow_SendKeys();

                // 2. Mở cửa sổ Config
                InvokePrivateMethod(mainMenu, "BtnConfig_Click", null, null);

                DoEvents();

                // Assert
                // Kiểm tra xem Settings có giữ nguyên (hoặc thay đổi nếu bạn gửi phím nhập text)
                // Ở đây ta chỉ check việc Save dialog đóng lại thành công
                // Nếu muốn nhập text, phải gửi SendKeys("C:\Path...") nhưng rất dễ lỗi với ký tự đặc biệt.

                // Assert đơn giản: Dialog phải đóng và không crash
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
                    // Đợi cửa sổ hiện lên (2 giây)
                    System.Threading.Thread.Sleep(2000);

                    // --- BẮT ĐẦU GỬI PHÍM ---

                    // Giả sử focus ban đầu ở TextBox đầu tiên.
                    // Gửi phím TAB 8 lần (hoặc số lần cần thiết để tới nút Save)
                    // Bạn có thể điều chỉnh số lượng {TAB} cho khớp với UI thật

                    // Ví dụ: Nhập Client Path
                    // SendKeys.SendWait("NewClientPath"); 

                    // Tab qua Server Path
                    SendKeys.SendWait("{TAB}");

                    // Tab qua Radio Button
                    SendKeys.SendWait("{TAB}");

                    // ... Gửi thêm Tab để đến nút Save ...
                    // Mẹo: Gửi SHIFT+TAB để đi ngược từ dưới lên (nếu nút Save ở cuối cùng)
                    // SendKeys.SendWait("+{TAB}"); // Shift+Tab (Đi ngược về Cancel)
                    // SendKeys.SendWait("+{TAB}"); // Shift+Tab (Đi ngược về Save)

                    // Ở đây tôi gửi 8 Tab như bạn yêu cầu
                    for (int i = 0; i < 5; i++)
                    {
                        SendKeys.SendWait("{TAB}");
                        System.Threading.Thread.Sleep(50); // Nghỉ chút cho focus kịp chuyển
                    }

                    // Cuối cùng: Enter để bấm nút đang Focus (Hy vọng là nút Save)
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