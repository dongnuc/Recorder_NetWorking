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

namespace ProjectExplorerTest
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class CreateProjectTest
    {
        private string _testRootPath = string.Empty;
        private Mock<IOFileHandler>? _mockFileHandler;
        private Mock<IOFileManagement>? _mockFileManager;
        private Mock<IServiceProvider>? _mockServiceProvider;
        private Mock<IOFolderHandler>? _mockFolderHandler;
        private Mock<ISystemLogger>? _mockLogger;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

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
                catch { /* Ignore */ }
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
                catch { /* Ignore */ }
            }
        }

        // --- TEST CASES ---

        [Test]
        public void CreateNewProject_ValidInput_ShouldCreateFolderStructure()
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

                // Process pending messages
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
        public void CreateNewProject_EmptyProjectName_ShouldShowValidationError()
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
                result.Text.Should().Contain("required", "Error message should mention required fields");
                setupWindow.ProjectCreatedSuccessfully.Should().BeFalse();
            }
            finally
            {
                setupWindow.Close();
            }
        }

        [Test]
        public void CreateNewProject_DuplicateName_UserClicksNo_ShouldNotOverwrite()
        {
            string projectName = "DuplicateProj";
            string fullPath = Path.Combine(_testRootPath, projectName);
            Directory.CreateDirectory(fullPath);

            var setupWindow = InitializeWindow();
            setupWindow.Show();

            try
            {
                SetPrivateTextBoxValue(setupWindow, "TxtProjectName", projectName);
                SetPrivateTextBoxValue(setupWindow, "TxtProjectLocation", _testRootPath);

                var result = HandleMessageBoxWithAction(
                    action: () => InvokePrivateMethod(setupWindow, "BtnCreateProject_Click", null, null),
                    expectedTitle: "Cảnh báo",
                    buttonToClick: "No"
                );

                result.Found.Should().BeTrue("MessageBox warning should appear for duplicate project");
                setupWindow.ProjectCreatedSuccessfully.Should().BeFalse("Project should not be created when user clicks No");
            }
            finally
            {
                setupWindow.Close();
            }
        }

        [Test]
        public void CreateNewProject_DuplicateName_UserClicksYes_ShouldOverwrite()
        {
            string projectName = "OverwriteProj";
            string fullPath = Path.Combine(_testRootPath, projectName);
            Directory.CreateDirectory(fullPath);

            _mockFolderHandler!.Setup(x => x.CreateDirectory(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string[]>()))
                .Returns(fullPath);

            var setupWindow = InitializeWindow();
            setupWindow.Show();

            try
            {
                SetPrivateTextBoxValue(setupWindow, "TxtProjectName", projectName);
                SetPrivateTextBoxValue(setupWindow, "TxtProjectLocation", _testRootPath);

                var result = HandleMessageBoxWithAction(
                    action: () => InvokePrivateMethod(setupWindow, "BtnCreateProject_Click", null, null),
                    expectedTitle: "Cảnh báo",
                    buttonToClick: "Yes"
                );

                result.Found.Should().BeTrue("MessageBox warning should appear");

                // Wait for processing
                Thread.Sleep(800);
                DoEvents();

                setupWindow.ProjectCreatedSuccessfully.Should().BeTrue(
                    "Project should be created when user clicks Yes");
            }
            finally
            {
                setupWindow.Close();
            }
        }

        // --- HELPER METHODS ---

        private ProjectSetupWindow InitializeWindow()
        {
            return new ProjectSetupWindow(
                _mockFileHandler!.Object,
                _mockServiceProvider!.Object,
                _mockFileManager!.Object,
                _mockLogger!.Object
            );
        }

        /// <summary>
        /// IMPROVED: Handle MessageBox with better timing and multiple detection strategies
        /// </summary>
        private (bool Found, string Text) HandleMessageBoxWithAction(
            Action action,
            string expectedTitle,
            string buttonToClick,
            int timeoutMs = 8000)
        {
            string foundText = string.Empty;
            bool found = false;

            // Button ID mapping
            string targetAutomationId = buttonToClick.ToLower() switch
            {
                "no" or "không" => "7",
                "yes" or "có" => "6",
                "ok" or "đồng ý" => "1",
                "cancel" or "hủy" => "2",
                _ => "1"
            };

            Debug.WriteLine($"[UIA] Initiating Handler for '{expectedTitle}' -> Button: '{buttonToClick}'");

            // 1. Create a dedicated STA Thread for UIA
            // Task.Run uses MTA, which can fail to see STA windows
            Thread uiaThread = new Thread(() =>
            {
                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < timeoutMs && !found)
                {
                    try
                    {
                        // Searching...
                        var desktop = AutomationElement.RootElement;
                        var condition = new PropertyCondition(AutomationElement.ClassNameProperty, "#32770");
                        var dialogs = desktop.FindAll(TreeScope.Children, condition);

                        foreach (AutomationElement dialog in dialogs)
                        {
                            string title = dialog.Current.Name;

                            // Debug only first finding to avoid spam
                            // Debug.WriteLine($"[UIA Scan] Visible Window: {title}");

                            if (title.Contains(expectedTitle, StringComparison.OrdinalIgnoreCase))
                            {
                                Debug.WriteLine($"[UIA Match] Found Dialog: '{title}'");

                                // Get Text
                                try
                                {
                                    var texts = dialog.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
                                    foreach (AutomationElement t in texts) foundText += t.Current.Name + " ";
                                }
                                catch { }

                                // Find Button
                                var allButtons = dialog.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));

                                foreach (AutomationElement btn in allButtons)
                                {
                                    string btnName = btn.Current.Name;
                                    string btnId = btn.Current.AutomationId;

                                    if (btnName.Equals(buttonToClick, StringComparison.OrdinalIgnoreCase) ||
                                        btnId == targetAutomationId ||
                                        btnName.Contains(buttonToClick, StringComparison.OrdinalIgnoreCase))
                                    {
                                        Debug.WriteLine($"[UIA Action] Clicking '{btnName}'...");

                                        // Try Invoke
                                        if (btn.TryGetCurrentPattern(InvokePattern.Pattern, out object pat))
                                        {
                                            ((InvokePattern)pat).Invoke();
                                        }
                                        else
                                        {
                                            btn.SetFocus();
                                            System.Windows.Forms.SendKeys.SendWait("{ENTER}");
                                        }
                                        found = true;
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[UIA Error] {ex.Message}");
                    }

                    Thread.Sleep(200);
                }
            });

            // Set STA is crucial for UIA compatibility
            uiaThread.SetApartmentState(ApartmentState.STA);
            uiaThread.IsBackground = true;
            uiaThread.Start();

            // 2. Main Thread triggers the Blocking Action
            Thread.Sleep(500); // Give UIA thread time to warm up
            Debug.WriteLine("[Main] Invoking Action...");

            try
            {
                action.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Main Error] Action failed: {ex.Message}");
            }

            // 3. Wait for UIA thread
            uiaThread.Join(1000);

            return (found, foundText.Trim());
        }

        /// <summary>
        /// Process pending Windows messages (like Application.DoEvents)
        /// </summary>
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