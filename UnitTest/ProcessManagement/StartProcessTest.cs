using Common.Helper.Kernel32API;
using Common.Helper.Kernel32API.Interface;
using Common.Interfaces.Logging;
using Common.Interfaces.Services;
using FluentAssertions;
using Moq;
using ProcessManagement.Services;
using System.Collections.Concurrent;
using System.Reflection;

namespace ProcessManagement.Test
{
    [TestFixture]
    public class StartProcessTest
    {
        private Mock<ITestkitManagerService> _mockTestkitService;
        private Mock<IProcessStarter> _mockProcessStarter;
        private Mock<IConsolePoller> _mockConsolePoller;
        private Mock<IKeyListener> _mockKeyListener;
        private Mock<IMutexManager> _mockMutexManager;
        private Mock<IConsoleManager> _mockConsoleManager;
        private Mock<IProcessWaiter> _mockProcessWaiter;
        private Mock<ISystemLogger> _mockLogger;

        // Class under test
        private ProcessManager _processManager;
        private string _tempExePath;

        [SetUp]
        public void Setup()
        {
            // 1. Initialize Mocks
            _mockTestkitService = new Mock<ITestkitManagerService>();
            _mockProcessStarter = new Mock<IProcessStarter>();
            _mockConsolePoller = new Mock<IConsolePoller>();
            _mockKeyListener = new Mock<IKeyListener>();
            _mockMutexManager = new Mock<IMutexManager>();
            _mockConsoleManager = new Mock<IConsoleManager>();
            _mockProcessWaiter = new Mock<IProcessWaiter>();
            _mockLogger = new Mock<ISystemLogger>();

            // 2. Setup Default Behaviors
            _mockMutexManager.Setup(m => m.Create(It.IsAny<string>())).Returns(new IntPtr(100));
            _mockProcessStarter.Setup(p => p.Start(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new ChildProcess { processId = 1, hProcess = new IntPtr(200) });

            // 3. Inject Dependencies
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

            // 4. Create dummy exe file for File.Exists check
            _tempExePath = Path.GetTempFileName();
        }

        [TearDown]
        public void TearDown()
        {
            _processManager?.Dispose();
            if (File.Exists(_tempExePath))
            {
                File.Delete(_tempExePath);
            }
        }

        private void SetPrivateField(string fieldName, object value)
        {
            var field = typeof(ProcessManager).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(_processManager, value);
        }

        private T GetPrivateField<T>(string fieldName)
        {
            var field = typeof(ProcessManager).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return (T)field?.GetValue(_processManager);
        }

        private async Task InvokePrivateMethodAsync(string methodName, params object[] parameters)
        {
            var method = typeof(ProcessManager).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            var task = (Task)method.Invoke(_processManager, parameters);
            await task;
        }

        [Test]
        public async Task TC01_StartSingleWithPollingAsync_Normal_ValidInput_ShouldStartAndLog()
        {
            // Arrange
            string name = "Client";

            // Act
            var result = await _processManager.StartSingleWithPollingAsync(_tempExePath, name);

            // Assert
            result.child.processId.Should().Be(1);
            result.cts.Should().NotBeNull();

            // Verify dependencies
            _mockProcessStarter.Verify(x => x.Start(_tempExePath, name), Times.Once);
            _mockMutexManager.Verify(x => x.Create(It.Is<string>(s => s.Contains(name))), Times.Once);

            // Check Dictionary update via Reflection
            var handles = GetPrivateField<ConcurrentDictionary<string, (ChildProcess, IntPtr)>>("_processHandles");
            handles.ContainsKey(name).Should().BeTrue();
        }

        [Test]
        public async Task TC02_StartSingleWithPollingAsync_Normal_ShowConsole_ShouldAllocConsole()
        {
            // Act
            await _processManager.StartSingleWithPollingAsync(_tempExePath, "Client", true, showConsoleMessages: true);

            // Assert
            _mockConsoleManager.Verify(x => x.Alloc(), Times.Once);
            _mockConsoleManager.Verify(x => x.Free(), Times.Once);
        }

        [Test]
        public async Task TC03_StartSingleWithPollingAsync_Abnormal_FileNotFound_ShouldThrowAndLog()
        {
            // Arrange
            string fakePath = "C:\\fake.exe";

            // Act
            Func<Task> act = async () => await _processManager.StartSingleWithPollingAsync(fakePath, "Client");

            // Assert
            await act.Should().ThrowAsync<FileNotFoundException>();
            _mockLogger.Verify(x => x.LogError(It.Is<string>(s => s.Contains("Executable not found"))), Times.Once);
        }


        [Test]
        public async Task TC04_StartSingleWithPollingAsync_Boundary_Restart_ShouldResetPreviousSnapshot()
        {
            // Scenario: Giả sử process "Client" đã chạy trước đó và có dữ liệu snapshot cũ.
            // Khi Start lại, dữ liệu cũ này BẮT BUỘC phải bị xóa để tránh tính toán sai Input/Output.

            // Arrange
            string name = "Client";

            // Inject dữ liệu rác vào _previousSnapshots thông qua Reflection
            var snapshots = GetPrivateField<ConcurrentDictionary<string, string>>("_previousSnapshots");
            snapshots.TryAdd(name, "OLD_SNAPSHOT_DATA_123");

            // Act
            await _processManager.StartSingleWithPollingAsync(_tempExePath, name);

            // Assert
            // Kiểm tra xem dữ liệu cũ có còn tồn tại không.
            // Logic đúng: Dữ liệu cũ phải bị xóa ngay đầu hàm.
            // Lưu ý: Sau 3s delay và start monitor, có thể snapshot mới được tạo, 
            // nhưng chắc chắn nó không được là "OLD_SNAPSHOT_DATA_123".

            bool hasValue = snapshots.TryGetValue(name, out string currentValue);

            if (hasValue)
            {
                currentValue.Should().NotBe("OLD_SNAPSHOT_DATA_123", "Snapshot cũ phải được reset/xóa khi start process mới");
            }
            else
            {
                // Nếu key không còn tồn tại -> Đã xóa thành công -> Pass
                hasValue.Should().BeFalse();
            }
        }

        [Test]
        public async Task TC05_StartSingleWithPollingAsync_Normal_Monitoring_ShouldTrackCancellationToken()
        {
            // Scenario: Kiểm tra xem CancellationTokenSource (CTS) có được lưu vào _monitoringTasks hay không.
            // Nếu không lưu, hàm Stop sau này sẽ không thể Cancel task được -> Memory Leak.

            // Arrange
            string name = "Server";

            // Act
            await _processManager.StartSingleWithPollingAsync(_tempExePath, name);

            // Assert
            var monitoringTasks = GetPrivateField<ConcurrentDictionary<string, CancellationTokenSource>>("_monitoringTasks");

            monitoringTasks.ContainsKey(name).Should().BeTrue("Monitoring Task phải được lưu vào dictionary để quản lý");
            monitoringTasks[name].Should().NotBeNull();
            monitoringTasks[name].Token.CanBeCanceled.Should().BeTrue();
        }

        [Test]
        public async Task TC06_StartSingleWithPollingAsync_Abnormal_ProcessStartFail_ShouldPropagateException()
        {
            // Scenario: Tầng dưới (Kernel32/ProcessStarter) bị lỗi (ví dụ: Access Denied, Out of Memory).
            // ProcessManager phải ném lỗi này ra ngoài cho UI xử lý chứ không được nuốt lỗi.

            // Arrange
            string name = "Client";
            string errorMessage = "Access Denied from Kernel";

            // Mock tầng dưới ném lỗi
            _mockProcessStarter.Setup(p => p.Start(It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new InvalidOperationException(errorMessage));

            // Act
            Func<Task> act = async () => await _processManager.StartSingleWithPollingAsync(_tempExePath, name);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage(errorMessage);

            // Verify: Đảm bảo Dictionary không lưu rác nếu start thất bại
            var handles = GetPrivateField<ConcurrentDictionary<string, (ChildProcess, IntPtr)>>("_processHandles");
            handles.ContainsKey(name).Should().BeFalse("Không được lưu handle nếu start thất bại");
        }

    }
}
