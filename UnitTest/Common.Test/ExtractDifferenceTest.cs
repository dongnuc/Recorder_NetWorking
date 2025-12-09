using Common.Helper;

namespace Common.Tests
{
    [TestFixture]
    public class DataInspectorTests
    {
        /// <summary>
        /// Test Case 1: Console đứng yên (Polling).
        /// Mô phỏng: Tool quét console liên tục nhưng không có gì mới xuất hiện.
        /// Before: Log server đang chạy.
        /// After: Log y hệt như cũ.
        /// Mong đợi: Chuỗi rỗng (không có dữ liệu mới).
        /// </summary>
        [Test]
        public void ExtractDifference_ConsoleIdle_ReturnsEmpty()
        {
            // Arrange
            string before = "[INFO] Server started at port 8080...\n[INFO] Waiting for connections...";
            string after = "[INFO] Server started at port 8080...\n[INFO] Waiting for connections...";

            // Act
            string result = DataInspector.ExtractDifference(before, after);

            // Assert
            Assert.That(result, Is.EqualTo(string.Empty));
        }

        /// <summary>
        /// Test Case 2: Xuất log dòng mới (Standard Logging).
        /// Mô phỏng: Server in thêm các dòng log mới xuống dưới cùng.
        /// Before: Log khởi động.
        /// After: Log khởi động + Log request mới.
        /// Mong đợi: Chỉ trả về phần log request mới.
        /// </summary>
        [Test]
        public void ExtractDifference_NewLogLinesAppended_ReturnsOnlyNewLines()
        {
            // Arrange
            string before = "Server Ready.";
            string after = "Server Ready.\n[Request] GET /api/users\n[Response] 200 OK";

            // Act
            string result = DataInspector.ExtractDifference(before, after);

            // Assert
            // Chú ý: Ký tự xuống dòng ở đầu chuỗi kết quả vì nó nối tiếp vào chuỗi cũ
            Assert.That(result, Is.EqualTo("\n[Request] GET /api/users\n[Response] 200 OK"));
        }

        /// <summary>
        /// Test Case 3: Người dùng nhập liệu trên cùng dòng (Inline Input).
        /// Mô phỏng: Chương trình hỏi "Username:" và người dùng gõ "admin" (không xuống dòng).
        /// Before: Prompt nhập liệu.
        /// After: Prompt nhập liệu + text người dùng gõ.
        /// Mong đợi: Trả về text người dùng gõ ("admin").
        /// </summary>
        [Test]
        public void ExtractDifference_UserTypingOnSameLine_ReturnsTypedCharacters()
        {
            // Arrange
            string before = "Please enter username: ";
            string after = "Please enter username: admin";

            // Act
            string result = DataInspector.ExtractDifference(before, after);

            // Assert
            Assert.That(result, Is.EqualTo("admin"));
        }

        /// <summary>
        /// Test Case 4: Màn hình bị xóa (Console Clear / cls).
        /// Mô phỏng: Ứng dụng chạy lệnh xóa màn hình và hiển thị menu mới.
        /// Before: Log cũ rất dài.
        /// After: Màn hình Menu ngắn gọn (không chứa nội dung cũ).
        /// Mong đợi: Trả về toàn bộ nội dung 'after' (vì ngữ cảnh đã thay đổi hoàn toàn).
        /// </summary>
        [Test]
        public void ExtractDifference_ConsoleCleared_ReturnsFullNewScreen()
        {
            // Arrange
            string before = "Loading modules...\nLoading Db...\nDone.";
            string after = "=== MAIN MENU ===\n1. Start\n2. Exit";

            // Act
            string result = DataInspector.ExtractDifference(before, after);

            // Assert
            Assert.That(result, Is.EqualTo(after));
        }

        /// <summary>
        /// Test Case 5: Khởi động lần đầu (First Capture).
        /// Mô phỏng: Lần đầu tiên tool kết nối vào process, chưa có snapshot trước đó.
        /// Before: Rỗng.
        /// After: Màn hình hiện tại của process.
        /// Mong đợi: Trả về toàn bộ màn hình hiện tại.
        /// </summary>
        [Test]
        public void ExtractDifference_FirstSnapshot_ReturnsFullContent()
        {
            // Arrange
            string before = string.Empty;
            string after = "Microsoft Windows [Version 10.0.19045]\n(c) Microsoft Corporation.";

            // Act
            string result = DataInspector.ExtractDifference(before, after);

            // Assert
            Assert.That(result, Is.EqualTo(after));
        }

        /// <summary>
        /// Test Case 6: Ghi đè bộ đệm (Buffer Rollover/Rewrite).
        /// Mô phỏng: Ứng dụng dạng thanh tiến trình (Progress Bar) vẽ lại màn hình liên tục
        /// hoặc nội dung mới ngắn hơn nội dung cũ nhưng lại giống phần đầu.
        /// Before: "Downloading: 50%"
        /// After: "Done." (Ngắn hơn Before và khác biệt).
        /// Mong đợi: Trả về "Done."
        /// </summary>
        [Test]
        public void ExtractDifference_ContentRewrittenOrShorter_ReturnsFullNewContent()
        {
            // Arrange
            string before = "Downloading update: [#####-----] 50%";
            string after = "Update Complete.";

            // Act
            string result = DataInspector.ExtractDifference(before, after);

            // Assert
            Assert.That(result, Is.EqualTo(after));
        }

        /// <summary>
        /// Test Case 7: Lỗi Capture (Snapshot mới bị rỗng).
        /// Mô phỏng: Không lấy được dữ liệu console (do process crash hoặc lỗi handle), trả về chuỗi rỗng.
        /// Before: Log đang chạy.
        /// After: Rỗng.
        /// Mong đợi: Trả về rỗng (không coi là thay đổi nội dung để tránh log rác).
        /// </summary>
        [Test]
        public void ExtractDifference_CaptureFailedReturningEmpty_ReturnsEmpty()
        {
            // Arrange
            string before = "System running...";
            string after = "";

            // Act
            string result = DataInspector.ExtractDifference(before, after);

            // Assert
            Assert.That(result, Is.EqualTo(string.Empty));
        }
    }
}