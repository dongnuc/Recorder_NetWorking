using Common.Helper.Kernel32API;
using Common.Helper.Kernel32API.Interface;
using Common.Interfaces.Logging;
using Common.Interfaces.Services;
using Common.Resources; // Chứa ActionKeywords
using FluentAssertions;
using Moq;
using ProcessManagement.Services;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

namespace ProcessManagement.Test
{
    [TestFixture]
    public class ExecuteCaptureSequenceTest
    {
        // Mocks
        private Mock<ITestkitManagerService> _mockTestkitService;
        private Mock<IConsolePoller> _mockConsolePoller;
        private Mock<ISystemLogger> _mockLogger;

        // Các mock phụ để khởi tạo constructor (không dùng trực tiếp trong test này nhưng cần thiết)
        private Mock<IProcessStarter> _mockProcessStarter;
        private Mock<IKeyListener> _mockKeyListener;
        private Mock<IMutexManager> _mockMutexManager;
        private Mock<IConsoleManager> _mockConsoleManager;
        private Mock<IProcessWaiter> _mockProcessWaiter;

        // Class under test
        private ProcessManager _processManager;

        [SetUp]
        public void Setup()
        {
            // 1. Initialize Mocks
            _mockTestkitService = new Mock<ITestkitManagerService>();
            _mockConsolePoller = new Mock<IConsolePoller>();
            _mockLogger = new Mock<ISystemLogger>();

            // Dummy mocks
            _mockProcessStarter = new Mock<IProcessStarter>();
            _mockKeyListener = new Mock<IKeyListener>();
            _mockMutexManager = new Mock<IMutexManager>();
            _mockConsoleManager = new Mock<IConsoleManager>();
            _mockProcessWaiter = new Mock<IProcessWaiter>();

            // 2. Inject Dependencies
            _processManager = new ProcessManager(
                _mockTestkitService.Object,
                _mockProcessStarter.Object,
                _mockConsolePoller.Object,
                _mockKeyListener.Object,
                _mockMutexManager.Object,
                _mockConsoleManager.Object,
                _mockProcessWaiter.Object,
                _mockLogger.Object
            );
        }

        [TearDown]
        public void TearDown()
        {
            _processManager?.Dispose();
        }

        #region Reflection Helpers
        private void SetPrivateSnapshot(string key, string value)
        {
            var field = typeof(ProcessManager).GetField("_previousSnapshots", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (ConcurrentDictionary<string, string>)field.GetValue(_processManager);
            dict[key] = value;
        }

        private string GetPrivateSnapshot(string key)
        {
            var field = typeof(ProcessManager).GetField("_previousSnapshots", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (ConcurrentDictionary<string, string>)field.GetValue(_processManager);
            return dict.TryGetValue(key, out var val) ? val : null;
        }

        private async Task InvokeExecuteCaptureAsync(string processName)
        {
            var method = typeof(ProcessManager).GetMethod("ExecuteCaptureSequenceAsync", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.InvokeMethod);
            // ChildProcess và Mutex có thể để dummy vì trong unit test này ta mock IConsolePoller
            var dummyChild = new ChildProcess();
            var dummyMutex = IntPtr.Zero;

            var task = (Task)method.Invoke(_processManager, new object[] { dummyChild, dummyMutex, processName });
            await task;
        }
        #endregion

        [Test]
        public async Task TC01_ExecuteCaptureSequenceAsync_Normal_ClientFirstRun_ShouldSendOutputDirectly()
        {
            // Scenario: Lần đầu chạy Client, chưa có snapshot cũ.
            // Expected: Toàn bộ nội dung capture được gửi thẳng vào ReceiveClientOutput.

            // Arrange
            string name = "Client";
            string captureContent = "Main Menu:\n1. Start";

            _mockConsolePoller.Setup(x => x.CaptureCurrentConsoleAsync(It.IsAny<ChildProcess>(), It.IsAny<IntPtr>(), false))
                .ReturnsAsync(captureContent);

            // Act
            await InvokeExecuteCaptureAsync(name);

            // Assert
            _mockTestkitService.Verify(x => x.ReceiveClientOutput(captureContent), Times.Once, "Lần đầu chạy phải gửi toàn bộ output");
            _mockTestkitService.Verify(x => x.ReceiveUserInput(It.IsAny<string>(), It.IsAny<string>()), Times.Never, "Lần đầu chạy không nên detect input");
        }

        [Test]
        public async Task TC02_ExecuteCaptureSequenceAsync_Normal_ClientSubsequentRun_WithInput_ShouldSplitAndSend()
        {
            // Scenario: Client đã chạy. Snapshot cũ: "Enter Name:". Mới: "Enter Name: Tai\nWelcome".
            // Logic ExtractDifference (giả định) sẽ ra " Tai\nWelcome".
            // Logic Split sẽ ra Input="Tai", Output="Welcome".

            // Arrange
            string name = "Client";
            SetPrivateSnapshot(name, "Enter Name:"); // Giả lập snapshot cũ

            string newCapture = "Enter Name: Tai\r\nWelcome User";
            _mockConsolePoller.Setup(x => x.CaptureCurrentConsoleAsync(It.IsAny<ChildProcess>(), It.IsAny<IntPtr>(), false))
                .ReturnsAsync(newCapture);

            // Act
            await InvokeExecuteCaptureAsync(name);

            // Assert
            // 1. Verify Input "Tai" được gửi đi (Code của bạn trim space nên có thể là "Tai")
            _mockTestkitService.Verify(x => x.ReceiveUserInput(It.Is<string>(s => s.Contains("Tai")), ActionKeywords.INPUT), Times.Once);

            // 2. Verify Output "Welcome User" được gửi đi
            _mockTestkitService.Verify(x => x.ReceiveClientOutput(It.Is<string>(s => s.Contains("Welcome User"))), Times.Once);
        }

        [Test]
        public async Task TC03_ExecuteCaptureSequenceAsync_Normal_ServerOutput_ShouldFormatAndSend()
        {
            // Scenario: Process là Server. Output trả về có nhiều dòng trống ở đầu.
            // Expected: Method phải trim bớt dòng trống đầu tiên và gửi vào ReceiveServerOutput.

            // Arrange
            string name = "Server";
            string rawOutput = "\r\n\r\nServer Listening on 5000...";
            // Logic code: split array, nếu phần tử đầu là "" thì skip.

            _mockConsolePoller.Setup(x => x.CaptureCurrentConsoleAsync(It.IsAny<ChildProcess>(), It.IsAny<IntPtr>(), false))
                .ReturnsAsync(rawOutput);

            // Act
            await InvokeExecuteCaptureAsync(name);

            Debug.WriteLine(rawOutput);

            // Assert
            _mockTestkitService.Verify(x => x.ReceiveServerOutput(It.Is<string>(s => !s.StartsWith("\r\n\r\n"))), Times.Once);
            _mockTestkitService.Verify(x => x.ReceiveServerOutput(It.Is<string>(s => s.Contains("Server Listening"))), Times.Once);
        }

        [Test]
        public async Task TC04_ExecuteCaptureSequenceAsync_Boundary_SnapshotUpdate_ShouldUpdateDictionary()
        {
            // Scenario: Sau khi chạy xong, biến _previousSnapshots phải được cập nhật giá trị mới nhất
            // để dùng cho lần so sánh tiếp theo.

            // Arrange
            string name = "Client";
            string initialData = "Step 1";
            string newData = "Step 1 -> Step 2";

            SetPrivateSnapshot(name, initialData);

            _mockConsolePoller.Setup(x => x.CaptureCurrentConsoleAsync(It.IsAny<ChildProcess>(), It.IsAny<IntPtr>(), false))
                .ReturnsAsync(newData);

            // Act
            await InvokeExecuteCaptureAsync(name);

            // Assert
            string currentSnapshot = GetPrivateSnapshot(name);
            currentSnapshot.Should().Be(newData, "Snapshot phải được update bằng giá trị capture mới nhất");
        }

        [Test]
        public async Task TC05_ExecuteCaptureSequenceAsync_Boundary_ClientInputNull_ShouldLogWarning()
        {
            // Scenario: Client có output mới nhưng không tách được Input (Input null hoặc empty).
            // Logic code: LogWarning("Input is null") và gửi ReceiveUserInput("", "UserInput").

            // Arrange
            string name = "Client";
            SetPrivateSnapshot(name, "Menu:");

            // Giả sử DataInspector detect được diff nhưng SplitInputFromOutput trả về input rỗng
            // (Phụ thuộc vào logic DataInspector thật, ở đây ta giả lập tình huống chỉ có output append thêm mà ko có input rõ ràng)
            // Ví dụ: Output nhảy thêm 1 dòng log mà ko phải user gõ
            string newCapture = "Menu:\nSystem Log: Error";

            _mockConsolePoller.Setup(x => x.CaptureCurrentConsoleAsync(It.IsAny<ChildProcess>(), It.IsAny<IntPtr>(), false))
                .ReturnsAsync(newCapture);

            // Act
            await InvokeExecuteCaptureAsync(name);

            // Assert
            // Verify Warning được gọi
            _mockLogger.Verify(x => x.LogWarning(It.Is<string>(s => s.Contains("Input is null"))), Times.AtLeastOnce);

            // Verify gửi Input rỗng
            _mockTestkitService.Verify(x => x.ReceiveUserInput("", "UserInput"), Times.Once);
        }

        [Test]
        public async Task TC06_ExecuteCaptureSequenceAsync_Abnormal_PollerException_ShouldLogAndNotCrash()
        {
            // Scenario: IConsolePoller bị lỗi (Timeout, Handle invalid...).
            // Expected: Method catch exception, Log Error và không làm crash app.

            // Arrange
            string name = "Client";
            _mockConsolePoller.Setup(x => x.CaptureCurrentConsoleAsync(It.IsAny<ChildProcess>(), It.IsAny<IntPtr>(), false))
                .ThrowsAsync(new TimeoutException("Read console timeout"));

            // Act
            Func<Task> act = async () => await InvokeExecuteCaptureAsync(name);

            // Assert
            await act.Should().NotThrowAsync("Method phải handle exception nội bộ");

            _mockLogger.Verify(x => x.LogError(It.Is<string>(s => s.Contains("Error in capture sequence"))), Times.Once);
            _mockLogger.Verify(x => x.LogError(It.Is<string>(s => s.Contains("Read console timeout"))), Times.Once);
        }
    }
}