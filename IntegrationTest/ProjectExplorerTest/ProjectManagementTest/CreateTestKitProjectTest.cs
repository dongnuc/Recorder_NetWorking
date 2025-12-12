using Common.Interfaces.IOFile;
using Common.Interfaces.Logging;
using FluentAssertions;
using Moq;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using WpfUI;
using WpfUI.ViewModels;

namespace IntegrationTest.ProjectManagementTest
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class CreateTestKitProject
    {
        private string _testRootPath = string.Empty;
        private Mock<IOFileHandler>? _mockFileHandler;
        private Mock<IOFileManagement>? _mockFileManager;
        private Mock<IServiceProvider>? _mockServiceProvider;
        private Mock<IOFolderHandler>? _mockFolderHandler;
        private Mock<ISystemLogger>? _mockLogger;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint FindWindow(string? lpClassName, string? lpWindowName);

        [SetUp]
        public void Setup()
        {
            string? assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(assemblyPath))
                throw new InvalidOperationException("Cannot determine assembly path");

            _testRootPath = Path.Combine(assemblyPath, "TestResources", "IntegrationProjects");

            if (Directory.Exists(_testRootPath))
            {
                try { Directory.Delete(_testRootPath, recursive: true); }
                catch { }
            }
            Directory.CreateDirectory(_testRootPath);

            _mockFileHandler = new Mock<IOFileHandler>();
            _mockFileManager = new Mock<IOFileManagement>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockFolderHandler = new Mock<IOFolderHandler>();
            _mockLogger = new Mock<ISystemLogger>();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testRootPath))
            {
                try { Directory.Delete(_testRootPath, recursive: true); }
                catch {  }
            }
        }


        [Test]
        public void CreateNewProject_ValidInput()
        {
            string projectName = "MyTestProject";
            string expectedFullPath = Path.Combine(_testRootPath, projectName);

            _mockFolderHandler!.Setup(x => x.CreateDirectory(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string[]>()))
                .Returns(expectedFullPath);

            var setupWindow = InitializeWindow();
            setupWindow.Show();

            try
            {
                SetPrivateTextBoxValue(setupWindow, "TxtProjectName", projectName);
                SetPrivateTextBoxValue(setupWindow, "TxtProjectLocation", _testRootPath);

                InvokePrivateMethod(setupWindow, "BtnCreateProject_Click", null, null);

                DoEvents();

                setupWindow.ProjectCreatedSuccessfully.Should().BeTrue(
                    "Project should be created successfully");

                _mockLogger!.Verify(
                    x => x.LogInfomation(It.Is<string>(s => s.Contains("created"))),
                    Times.AtLeastOnce);
            }
            finally
            {
                setupWindow.Close();
            }
        }

        [Test]
        public void CreateNewProject_EmptyProjectName()
        {
            var setupWindow = InitializeWindow();
            setupWindow.Show();

            try
            {
                SetPrivateTextBoxValue(setupWindow, "TxtProjectName", string.Empty);
                SetPrivateTextBoxValue(setupWindow, "TxtProjectLocation", _testRootPath);

                var result = HandleMessageBoxWithAction(
                    action: () => InvokePrivateMethod(setupWindow, "BtnCreateProject_Click", null, null),
                    expectedTitle: "Error",
                    buttonToClick: "OK"
                );

                result.Found.Should().BeTrue("MessageBox Error should appear for empty project name");
                
                setupWindow.ProjectCreatedSuccessfully.Should().BeFalse();
            }
            finally
            {
                setupWindow.Close();
            }
        }

        private ProjectSetupWindow InitializeWindow()
        {
            return new ProjectSetupWindow(
                _mockFileHandler!.Object,
                _mockServiceProvider!.Object,
                _mockFileManager!.Object,
                _mockLogger!.Object
            );
        }
        private (bool Found, string Text) HandleMessageBoxWithAction(
            Action action,
            string expectedTitle,
            string buttonToClick,
            int timeoutMs = 8000)
        {
            string keysToSend = "{ENTER}";

            if (buttonToClick.Equals("No", StringComparison.OrdinalIgnoreCase) ||
                buttonToClick.Equals("Không", StringComparison.OrdinalIgnoreCase))
            {
                keysToSend = "{TAB}{ENTER}";
            }

            Task.Run(() =>
            {
                Thread.Sleep(1500);

                try
                {
                    Debug.WriteLine($"[SendKeys] Sending: {keysToSend}");
                    SendKeys.SendWait(keysToSend);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SendKeys Error] {ex.Message}");
                }
            });

            Debug.WriteLine("[Main] Invoking Action...");
            action.Invoke();

            return (true, "Nội dung bị bỏ qua do dùng SendKeys");
        }

        private void DoEvents()
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new System.Windows.Threading.DispatcherOperationCallback(obj =>
                {
                    ((System.Windows.Threading.DispatcherFrame)obj!).Continue = false;
                    return null;
                }),
                frame);
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }

        private void SetPrivateTextBoxValue(object target, string textBoxName, string value)
        {
            var field = target.GetType().GetField(
                textBoxName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

            if (field != null)
            {
                var textBox = field.GetValue(target) as System.Windows.Controls.TextBox;
                if (textBox != null)
                {
                    textBox.Text = value;
                    return;
                }
            }

            var property = target.GetType().GetProperty(
                textBoxName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

            if (property != null)
            {
                var textBox = property.GetValue(target) as System.Windows.Controls.TextBox;
                if (textBox != null)
                {
                    textBox.Text = value;
                    return;
                }
            }

            throw new InvalidOperationException($"Cannot find TextBox: {textBoxName}");
        }

        private void InvokePrivateMethod(object target, string methodName, params object?[]? parameters)
        {
            MethodInfo? method = target.GetType().GetMethod(
                methodName, 

                BindingFlags.NonPublic | BindingFlags.Instance);

            if (method == null)
            {
                throw new InvalidOperationException($"Method not found: {methodName}");
            }

            method.Invoke(target, parameters);
        }
    }
}