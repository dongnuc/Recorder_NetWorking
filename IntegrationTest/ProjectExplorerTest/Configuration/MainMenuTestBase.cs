using Common.Interfaces.IOFile;
using Common.Interfaces.Logging;
using Common.Interfaces.Services;
using IntegrationTest.ProjectManagementTest;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using WpfUI;
using WpfUI.ViewModels;

namespace IntegrationTest.Configuration
{
    public class MainMenuTestBase : ProjectExplorerTestBase
    {
        protected string _projectPath = string.Empty;
        protected string _templateRootPath = string.Empty;
        protected MainMenuViewModel? _viewModel;

        protected Mock<IProcessManager>? _mockProcessManager;
        protected Mock<ITestkitManagerService>? _mockTestkitManager;

        protected Mock<ISystemLogger>? _mockLogger;

        protected ISystemLogger? _realLogger;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            _projectPath = Path.Combine(_testRootPath, "IntegrationTestProject");
            Directory.CreateDirectory(_projectPath);

            SetupDummyTemplates();

            _mockProcessManager = new Mock<IProcessManager>();
            _mockTestkitManager = new Mock<ITestkitManagerService>();
            _mockLogger = new Mock<ISystemLogger>();

            _realLogger = CreateRealLogManagerInstance();

            _mockServiceProvider!.Setup(x => x.GetService(typeof(IProcessManager))).Returns(_mockProcessManager.Object);
            _mockServiceProvider.Setup(x => x.GetService(typeof(ITestkitManagerService))).Returns(_mockTestkitManager.Object);

            _mockServiceProvider.Setup(x => x.GetService(typeof(ISystemLogger))).Returns(_realLogger);

            _mockServiceProvider.Setup(x => x.GetService(typeof(IOFileHandler))).Returns(_mockFileHandler!.Object);

            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockScope = new Mock<IServiceScope>();
            mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);
            mockScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
            _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
                .Returns(mockScopeFactory.Object);

            SetupRealFileSystemMocks();

            AddSetting("ClientExePath", @"C:\Fake\Client.exe");
            AddSetting("ServerExePath", @"C:\Fake\Server.exe");
            AddSetting("Protocol", "TCP");

            _viewModel = new MainMenuViewModel(
                _mockFolderHandler!.Object,
                _mockFileHandler!.Object,
                _projectPath,
                _realLogger! 
            );
        }

        private ISystemLogger CreateRealLogManagerInstance()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                Type? logType = null;

                foreach (var asm in assemblies)
                {
                    logType = asm.GetTypes().FirstOrDefault(t => t.Name == "LogManager" && !t.IsInterface);
                    if (logType != null) break;
                }

                if (logType == null) throw new Exception("Không tìm thấy class LogManager");

                try
                {
                    return (ISystemLogger)Activator.CreateInstance(logType,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, new object[] { _testRootPath }, null)!;
                }
                catch
                {
                    return (ISystemLogger)Activator.CreateInstance(logType)!;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create LogManager: {ex.Message}");
            }
        }

        protected MainMenu InitializeMainMenu()
        {
            return new MainMenu(
                _viewModel!,
                _mockFileHandler!.Object,
                _mockServiceProvider!.Object,
                _mockFileManager!.Object,
                _realLogger!
            );
        }

        protected string GetSetting(string key)
        {
            var settingsType = typeof(WpfUI.App).Assembly.GetType("WpfUI.Properties.Settings");
            var defaultProp = settingsType!.GetProperty("Default", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var settingsInstance = defaultProp!.GetValue(null)!;
            var prop = settingsType.GetProperty(key);
            return prop?.GetValue(settingsInstance)?.ToString() ?? string.Empty;
        }

        protected void AddSetting(string key, string value)
        {
            var settingsType = typeof(WpfUI.App).Assembly.GetType("WpfUI.Properties.Settings");
            var defaultProp = settingsType!.GetProperty("Default", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var settingsInstance = defaultProp!.GetValue(null)!;
            var prop = settingsType.GetProperty(key);
            prop?.SetValue(settingsInstance, value);
        }

        protected void CloseWindowSafe(MainMenu window, int delayMs = 500)
        {
            if (window == null) return;
            DoEvents();
            if (delayMs > 0)
            {
                int step = 50;
                for (int i = 0; i < delayMs; i += step)
                {
                    System.Threading.Thread.Sleep(step);
                    DoEvents();
                }
            }
            window.Close();
        }

        private void SetupDummyTemplates()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _templateRootPath = Path.Combine(baseDir, "Resources", "Templates", "DotnetNetworking");
            Directory.CreateDirectory(Path.Combine(_templateRootPath, "Question"));
            Directory.CreateDirectory(Path.Combine(_templateRootPath, "TestCase"));
            CreateEmptyFile(Path.Combine(_templateRootPath, "Question", "Header.xlsx"));
            CreateEmptyFile(Path.Combine(_templateRootPath, "Question", "Environment.xlsx"));
            CreateEmptyFile(Path.Combine(_templateRootPath, "Question", "EnvRunDB.xlsx"));
            CreateEmptyFile(Path.Combine(_templateRootPath, "Question", "EnvRunNoDB.xlsx"));
            CreateEmptyFile(Path.Combine(_templateRootPath, "TestCase", "Header.xlsx"));
            CreateEmptyFile(Path.Combine(_templateRootPath, "TestCase", "Environment.xlsx"));
            CreateEmptyFile(Path.Combine(_templateRootPath, "TestCase", "Detail.xlsx"));
            CreateEmptyFile(Path.Combine(_templateRootPath, "TestCase", "EnvRunDB.xlsx"));
            CreateEmptyFile(Path.Combine(_templateRootPath, "TestCase", "EnvRunNoDB.xlsx"));
            CreateEmptyFile(Path.Combine(_templateRootPath, "TestCase", "NetworkTCP.xlsx"));
            CreateEmptyFile(Path.Combine(_templateRootPath, "TestCase", "NetworkHTTP.xlsx"));
        }

        protected void CreateEmptyFile(string path)
        {
            string? dir = Path.GetDirectoryName(path);
            if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (!File.Exists(path)) File.WriteAllText(path, "Dummy Content");
        }

        private void SetupRealFileSystemMocks()
        {
            _mockFolderHandler!.Setup(x => x.CreateDirectory(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>()))
                .Returns((string parent, string name, string[] subs) =>
                {
                    string fullPath = Path.Combine(parent, name);
                    Directory.CreateDirectory(fullPath);
                    return fullPath;
                });

            _mockFolderHandler.Setup(x => x.CopyTemplateFromResource(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string[]>()))
                .Callback((string destPath, string srcDir, bool overwrite, string[] files) =>
                {
                    foreach (var file in files) CreateEmptyFile(Path.Combine(destPath, file));
                });

            _mockFolderHandler.Setup(x => x.ReplaceSheetExcel(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()));
            _mockFolderHandler.Setup(x => x.DeleteFileOrFolder(It.IsAny<string>()))
                .Callback((string path) => { if (Directory.Exists(path)) Directory.Delete(path, true); });
        }
    }
}