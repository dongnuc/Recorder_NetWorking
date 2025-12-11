using Common.Interfaces.IOFile;
using Common.Interfaces.Logging;
using Common.Interfaces.Services;
using IntegrationTest.ProjectManagementTest;
using Microsoft.Extensions.DependencyInjection; // Cần namespace này
using Moq;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using WpfUI;
using WpfUI.ViewModels;

namespace IntegrationTest.TestKitManagement
{
    public class MainMenuTestBase : ProjectExplorerTestBase
    {
        protected string _projectPath = string.Empty;
        protected string _templateRootPath = string.Empty;
        protected MainMenuViewModel? _viewModel;
        protected Mock<IProcessManager>? _mockProcessManager;
        protected Mock<ITestkitManagerService>? _mockTestkitManager;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            _projectPath = Path.Combine(_testRootPath, "IntegrationTestProject");
            Directory.CreateDirectory(_projectPath);

            SetupDummyTemplates();

            // --- SETUP MOCK SERVICES ---
            _mockProcessManager = new Mock<IProcessManager>();
            _mockTestkitManager = new Mock<ITestkitManagerService>();

            // Mock các service cơ bản
            _mockServiceProvider!.Setup(x => x.GetService(typeof(IProcessManager)))
                .Returns(_mockProcessManager.Object);
            _mockServiceProvider.Setup(x => x.GetService(typeof(ITestkitManagerService)))
                .Returns(_mockTestkitManager.Object);
            _mockServiceProvider.Setup(x => x.GetService(typeof(ISystemLogger)))
                .Returns(_realLogger);
            _mockServiceProvider.Setup(x => x.GetService(typeof(IOFileHandler)))
                .Returns(_mockFileHandler!.Object);

            // --- QUAN TRỌNG: MOCK IServiceScopeFactory ---
            // Code RecorderWindowScope cần cái này, nếu thiếu sẽ bị Crash và không tạo được Tab
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockScope = new Mock<IServiceScope>();

            // Setup dây chuyền: Factory -> Scope -> ServiceProvider
            mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);
            mockScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);

            _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
                .Returns(mockScopeFactory.Object);
            // ------------------------------------------------

            SetupRealFileSystemMocks();

            // Bypass Config Check
            AddSetting("ClientExePath", "C:\\Fake\\Client.exe");
            AddSetting("ServerExePath", "C:\\Fake\\Server.exe");
            AddSetting("Protocol", "TCP");

            _viewModel = new MainMenuViewModel(
                _mockFolderHandler!.Object,
                _mockFileHandler!.Object,
                _projectPath,
                _realLogger!
            );
        }

        // ... (Giữ nguyên các hàm SetupDummyTemplates, CreateEmptyFile, SetupRealFileSystemMocks như cũ)
        private void SetupDummyTemplates()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _templateRootPath = Path.Combine(baseDir, "Resources", "Templates", "DotnetNetworking");
            Directory.CreateDirectory(Path.Combine(_templateRootPath, "Question"));
            Directory.CreateDirectory(Path.Combine(_templateRootPath, "TestCase"));

            // Tạo file giả để ViewModel không lỗi
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

        protected void AddSetting(string key, string value)
        {
            var settingsType = typeof(WpfUI.App).Assembly.GetType("WpfUI.Properties.Settings");
            var defaultProp = settingsType!.GetProperty("Default", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var settingsInstance = defaultProp!.GetValue(null)!;
            var prop = settingsType.GetProperty(key);
            prop?.SetValue(settingsInstance, value);
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

        protected void CloseWindowSafe(MainMenu window)
        {
            if (window == null) return;
            DoEvents();
            window.Close();
        }
    }
}