using Common.Helper.Kernel32API;
using Common.Helper.Kernel32API.Implement;
using Common.Helper.Kernel32API.Interface;
using Common.Interfaces.Logging;
using Common.Interfaces.Services;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using ProcessManagement.Services;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ProcessManagement.Test
{
    [TestFixture]
    public class HandleF12PressTest
    {
        // Mocks
        private Mock<IConsolePoller> _mockConsolePoller;
        private Mock<ISystemLogger> _mockLogger;
        private Mock<ITestkitManagerService> _mockTestkitService;

        // Dummy Mocks (Required for Constructor)
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
            _mockConsolePoller = new Mock<IConsolePoller>();
            _mockLogger = new Mock<ISystemLogger>();
            _mockTestkitService = new Mock<ITestkitManagerService>();

            _mockProcessStarter = new Mock<IProcessStarter>();
            _mockKeyListener = new Mock<IKeyListener>();
            _mockMutexManager = new Mock<IMutexManager>();
            _mockConsoleManager = new Mock<IConsoleManager>();
            _mockProcessWaiter = new Mock<IProcessWaiter>();

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

        // Helper để giả lập dictionary chứa handle process
        private void SetProcessHandles(string key, ChildProcess child, IntPtr mutex)
        {
            var field = typeof(ProcessManager).GetField("_processHandles", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (ConcurrentDictionary<string, (ChildProcess, IntPtr)>)field.GetValue(_processManager);
            dict[key] = (child, mutex);
        }

        private async Task InvokeHandleF12Async(string triggerProcess)
        {
            var method = typeof(ProcessManager).GetMethod("HandleF12PressAsync", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var task = (Task)method.Invoke(_processManager, new object[] { triggerProcess });
            await task;
        }

        #endregion

        #region N - Normal Cases (Logic Priority)

        [Test]
        public async Task TC01_HandleF12PressAsync_Normal_BothProcesses_ShouldCaptureClientTHENServer()
        {
            // Scenario: Cả Client và Server đều đang chạy.
            // Requirement: Phải chụp Client trước, sau đó delay 100ms, rồi mới chụp Server.

            // Arrange
            var clientMutex = new IntPtr(10);
            var serverMutex = new IntPtr(20);

            SetProcessHandles("Client", new ChildProcess { processId = 1 }, clientMutex);
            SetProcessHandles("Server", new ChildProcess { processId = 2 }, serverMutex);

            // Dùng MockSequence để bắt buộc thứ tự gọi
            var sequence = new MockSequence();

            // Kỳ vọng 1: Gọi Capture cho Client (Mutex 10)
            _mockConsolePoller.InSequence(sequence)
                .Setup(x => x.CaptureCurrentConsoleAsync(It.IsAny<ChildProcess>(), clientMutex, false))
                .ReturnsAsync("Client Output");

            // Kỳ vọng 2: Gọi Capture cho Server (Mutex 20)
            _mockConsolePoller.InSequence(sequence)
                .Setup(x => x.CaptureCurrentConsoleAsync(It.IsAny<ChildProcess>(), serverMutex, false))
                .ReturnsAsync("Server Output");

            // Act
            await InvokeHandleF12Async("Client"); // Trigger bởi ai không quan trọng bằng flow logic

            // Assert
            _mockConsolePoller.Verify(x => x.CaptureCurrentConsoleAsync(It.IsAny<ChildProcess>(), clientMutex, false), Times.Once);
            _mockConsolePoller.Verify(x => x.CaptureCurrentConsoleAsync(It.IsAny<ChildProcess>(), serverMutex, false), Times.Once);
        }

        [Test]
        public async Task TC02_HandleF12PressAsync_Normal_OnlyClient_ShouldCaptureClientOnly()
        {
            // Scenario: Chỉ bật Client.

            // Arrange
            var clientMutex = new IntPtr(10);
            SetProcessHandles("Client", new ChildProcess { processId = 1 }, clientMutex);

            // Act
            await InvokeHandleF12Async("Client");

            // Assert
            _mockConsolePoller.Verify(x => x.CaptureCurrentConsoleAsync(It.IsAny<ChildProcess>(), clientMutex, false), Times.Once);

            // Verify Server không bao giờ được gọi (dùng It.IsAny<IntPtr> để check chung)
            _mockConsolePoller.Verify(x => x.CaptureCurrentConsoleAsync(It.IsAny<ChildProcess>(), It.Is<IntPtr>(p => p != clientMutex), false), Times.Never);
        }

        [Test]
        public async Task TC03_HandleF12PressAsync_Normal_OnlyServer_ShouldCaptureServerOnly()
        {
            // Scenario: Chỉ bật Server (ví dụ test Server độc lập).

            // Arrange
            var serverMutex = new IntPtr(20);
            SetProcessHandles("Server", new ChildProcess { processId = 2 }, serverMutex);

            // Act
            await InvokeHandleF12Async("Server");

            // Assert
            _mockConsolePoller.Verify(x => x.CaptureCurrentConsoleAsync(It.IsAny<ChildProcess>(), serverMutex, false), Times.Once);

            // Verify Client không bao giờ được gọi
            _mockTestkitService.Verify(x => x.ReceiveClientOutput(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region B - Boundary Cases

        [Test]
        public async Task TC04_HandleF12PressAsync_Boundary_NoProcessRunning_ShouldDoNothing()
        {
            // Scenario: Bấm F12 nhưng không có process nào trong list (ví dụ vừa stop xong).
            // Code không được crash.

            // Arrange
            // Không set handles nào cả (Empty Dictionary)

            // Act
            await InvokeHandleF12Async("Client");

            // Assert
            _mockConsolePoller.Verify(x => x.CaptureCurrentConsoleAsync(It.IsAny<ChildProcess>(), It.IsAny<IntPtr>(), It.IsAny<bool>()), Times.Never);
        }

        #endregion

        #region A - Abnormal Cases (Concurrency & Exceptions)

        [Test]
        public async Task TC05_HandleF12PressAsync_Abnormal_ExceptionInCapture_ShouldReleaseLock()
        {
            // Scenario: Trong quá trình chụp Client bị lỗi (Exception).
            // Requirement: Lock (SemaphoreSlim) phải được release trong block `finally`.
            // Nếu không release, lần gọi tiếp theo sẽ bị treo vĩnh viễn (Deadlock).

            // Arrange
            SetProcessHandles("Client", new ChildProcess(), IntPtr.Zero);

            // Mock Poller ném lỗi
            _mockConsolePoller.Setup(x => x.CaptureCurrentConsoleAsync(It.IsAny<ChildProcess>(), It.IsAny<IntPtr>(), false))
                .ThrowsAsync(new Exception("Console Access Denied"));

            // Act 1: Gọi lần đầu -> Sẽ văng lỗi
            // ProcessManager catch lỗi bên trong ExecuteCaptureSequenceAsync nên Method này không văng ra ngoài,
            // nhưng quan trọng là luồng chạy phải thoát khỏi block lock.
            await InvokeHandleF12Async("Client");

            // Act 2: Gọi lần 2
            // Nếu Lock chưa được release, dòng này sẽ bị treo (hoặc timeout test).
            // Ta dùng Timeout cho Test này để đảm bảo.
            var task2 = InvokeHandleF12Async("Client");

            if (await Task.WhenAny(task2, Task.Delay(1000)) == task2)
            {
                // Task hoàn thành -> Lock đã được release -> Pass
                Assert.Pass();
            }
            else
            {
                // Task chưa hoàn thành sau 1s -> Deadlock -> Fail
                Assert.Fail("Deadlock detected! Semaphore was not released after exception.");
            }
        }

        #endregion
    }
}