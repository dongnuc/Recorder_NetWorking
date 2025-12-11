using Common.Interfaces.IOFile;
using Common.Interfaces.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Threading;
using WpfUI;

using WpfTextBox = System.Windows.Controls.TextBox;
using WpfListBox = System.Windows.Controls.ListBox;
using WpfButton = System.Windows.Controls.Button;

namespace IntegrationTest.ProjectManagementTest
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class ProjectExplorerTestBase
    {
        protected string _testRootPath = string.Empty;

        protected Mock<IOFileHandler>? _mockFileHandler;
        protected Mock<IOFileManagement>? _mockFileManager;
        protected Mock<IServiceProvider>? _mockServiceProvider;
        protected Mock<IOFolderHandler>? _mockFolderHandler;

        protected ISystemLogger? _realLogger;

        [SetUp]
        public virtual void Setup()
        {
            string? assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(assemblyPath)) throw new InvalidOperationException("Cannot determine assembly path");

            _testRootPath = Path.Combine(assemblyPath, "TestResources", "RecentProjects");
            CleanupTestPath();
            Directory.CreateDirectory(_testRootPath);

            _mockFileHandler = new Mock<IOFileHandler>();
            _mockFileManager = new Mock<IOFileManagement>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockFolderHandler = new Mock<IOFolderHandler>();

            // Khởi tạo Logger thật
            _realLogger = CreateRealLogManagerInstance();

            ResetSettings();
        }

        [TearDown]
        public virtual void TearDown()
        {
            CleanupTestPath();
            ResetSettings();
        }

        private void CleanupTestPath()
        {
            if (Directory.Exists(_testRootPath))
            {
                try { Directory.Delete(_testRootPath, recursive: true); } catch { }
            }
        }

        protected ProjectExplorerWindow InitializeWindow()
        {
            return new ProjectExplorerWindow(
                _mockServiceProvider!.Object,
                _mockFolderHandler!.Object,
                _mockFileHandler!.Object,
                _mockFileManager!.Object,
                _realLogger!
            );
        }

        // --- CẬP NHẬT: HÀM TÌM VÀ TẠO LOGMANAGER MẠNH MẼ HƠN ---
        private ISystemLogger CreateRealLogManagerInstance()
        {
            Type? logManagerType = null;

            // Cách 1: Tìm trong Assembly của ProjectExplorerWindow (WpfUI)
            logManagerType = typeof(ProjectExplorerWindow).Assembly.GetTypes()
                .FirstOrDefault(t => t.Name == "LogManager" && !t.IsInterface);

            // Cách 2: Nếu không thấy, QUÉT TẤT CẢ Assembly đang load (bao gồm cả Common, Services...)
            if (logManagerType == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        logManagerType = asm.GetTypes().FirstOrDefault(t => t.Name == "LogManager" && !t.IsInterface);
                        if (logManagerType != null) break;
                    }
                    catch { /* Bỏ qua lỗi nếu không đọc được Type từ Assembly nào đó */ }
                }
            }

            if (logManagerType == null)
            {
                throw new Exception("CRITICAL ERROR: Không tìm thấy class 'LogManager' trong bất kỳ DLL nào. Test không thể chạy vì LogViewerControl yêu cầu instance thật.");
            }

            // Thử khởi tạo với các Constructor khác nhau
            var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // Thử 1: Constructor rỗng ()
            try
            {
                return (ISystemLogger)Activator.CreateInstance(logManagerType, bindingFlags, null, null, null)!;
            }
            catch { }

            // Thử 2: Constructor nhận đường dẫn string (path)
            try
            {
                return (ISystemLogger)Activator.CreateInstance(logManagerType, bindingFlags, null, new object[] { _testRootPath }, null)!;
            }
            catch { }

            // Thử 3: Constructor nhận IOFolderHandler (Mock)
            try
            {
                return (ISystemLogger)Activator.CreateInstance(logManagerType, bindingFlags, null, new object[] { _mockFolderHandler!.Object }, null)!;
            }
            catch { }

            // Thử 4: Constructor nhận IServiceProvider (Mock)
            try
            {
                return (ISystemLogger)Activator.CreateInstance(logManagerType, bindingFlags, null, new object[] { _mockServiceProvider!.Object }, null)!;
            }
            catch { }

            throw new Exception($"Tìm thấy class '{logManagerType.FullName}' nhưng không thể khởi tạo constructor nào phù hợp. Vui lòng kiểm tra tham số của constructor LogManager.");
        }

        protected void CloseWindowSafe(ProjectExplorerWindow window, int delayMs = 500)
        {
            if (window == null) return;

            // 1. Force UI update lần cuối để đảm bảo mọi thứ đã vẽ xong
            DoEvents();

            // 2. Thực hiện Delay (Vòng lặp nhỏ + DoEvents để UI không bị "đơ" khi chờ)
            if (delayMs > 0)
            {
                int step = 50; // Mỗi bước chờ 50ms
                for (int i = 0; i < delayMs; i += step)
                {
                    Thread.Sleep(step);
                    DoEvents(); // Quan trọng: Giữ cho cửa sổ phản hồi, không bị quay vòng tròn
                }
            }

            // 3. Hack: Set biến private _isNavigatingToMainMenu = true
            // Điều này ngăn chặn Application.Current.Shutdown() được gọi trong OnClosed
            var field = typeof(ProjectExplorerWindow).GetField("_isNavigatingToMainMenu",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(window, true);
            }

            // 4. Đóng window
            window.Close();
        }

        protected void DoEvents()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(obj => {
                ((DispatcherFrame)obj!).Continue = false;
                return null;
            }), frame);
            Dispatcher.PushFrame(frame);
        }

        protected void InvokePrivateMethod(object target, string methodName, params object?[]? parameters)
        {
            MethodInfo? method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null) throw new InvalidOperationException($"Method not found: {methodName}");
            method.Invoke(target, parameters);
        }

        protected void SetPrivateTextBoxValue(object target, string controlName, string value)
        {
            var control = GetPrivateField<WpfTextBox>(target, controlName);
            if (control != null) control.Text = value;
        }

        protected T? GetPrivateField<T>(object target, string fieldName) where T : class
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (field != null) return field.GetValue(target) as T;
            var prop = type.GetProperty(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (prop != null) return prop.GetValue(target) as T;
            return null;
        }

        private object GetSettingsDefaultInstance()
        {
            var settingsType = typeof(ProjectExplorerWindow).Assembly.GetType("WpfUI.Properties.Settings");
            var defaultProp = settingsType!.GetProperty("Default", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return defaultProp!.GetValue(null)!;
        }

        protected void AddProjectToSettings(string path)
        {
            var settingsInstance = GetSettingsDefaultInstance();
            var settingsType = settingsInstance.GetType();
            var recentProjectsProp = settingsType.GetProperty("RecentProjects", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var collection = recentProjectsProp!.GetValue(settingsInstance) as StringCollection;

            if (collection == null)
            {
                collection = new StringCollection();
                recentProjectsProp.SetValue(settingsInstance, collection);
            }

            if (collection.Contains(path)) collection.Remove(path);
            collection.Insert(0, path);

            var saveMethod = settingsType.GetMethod("Save", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            saveMethod!.Invoke(settingsInstance, null);
        }

        protected void ResetSettings()
        {
            var settingsInstance = GetSettingsDefaultInstance();
            var settingsType = settingsInstance.GetType();
            var recentProjectsProp = settingsType.GetProperty("RecentProjects", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            recentProjectsProp!.SetValue(settingsInstance, new StringCollection());

            var projectPathProp = settingsType.GetProperty("ProjectPath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (projectPathProp != null) projectPathProp.SetValue(settingsInstance, "");

            var saveMethod = settingsType.GetMethod("Save");
            saveMethod!.Invoke(settingsInstance, null);
        }

        protected bool IsPathInSettings(string path)
        {
            var settingsInstance = GetSettingsDefaultInstance();
            var recentProjectsProp = settingsInstance.GetType().GetProperty("RecentProjects");
            var collection = recentProjectsProp!.GetValue(settingsInstance) as StringCollection;
            return collection != null && collection.Contains(path);
        }

        protected string GetCurrentProjectPathFromSettings()
        {
            var settingsInstance = GetSettingsDefaultInstance();
            var prop = settingsInstance.GetType().GetProperty("ProjectPath");
            return prop!.GetValue(settingsInstance) as string ?? string.Empty;
        }

        protected void CreateDummyProject(string projectName)
        {
            string path = Path.Combine(_testRootPath, projectName);
            Directory.CreateDirectory(path);
            AddProjectToSettings(path);
        }
    }
}