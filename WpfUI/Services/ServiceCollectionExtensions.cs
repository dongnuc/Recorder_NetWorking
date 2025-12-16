using Common.Helper.Kernel32API;
using Common.Helper.Kernel32API.Implement;
using Common.Helper.Kernel32API.Interface;
using Common.Interfaces.IOFile;
using Common.Interfaces.Logging;
using Common.Interfaces.Services;
using Common.Logging;
using FileManagement.FileHelper;
using FileManagement.FileHelper.FileHandler;
using FileManagement.FolderHelper;
using Microsoft.Extensions.DependencyInjection;
using ProcessManagement.Services;
using TestKitManagement.Services;

namespace WpfUI.Services
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// ✅ Register tất cả services cho UITestKit với full Dependency Injection
        /// </summary>
        public static IServiceCollection AddUITestKitServices(this IServiceCollection services)
        {
            // ✅ Logging Service (Singleton - shared across application)
            services.AddSingleton<ISystemLogger, LogManager>();

            // ✅ Core Process Management Dependencies (Scoped - same as ProcessManager)
            services.AddScoped<IProcessManager, ProcessManager>();
            services.AddScoped<IProcessStarter, ProcessStarter>();
            services.AddScoped<IConsolePoller, ConsolePoller>();
            services.AddScoped<IKeyListener, KeyListener>();
            services.AddScoped<IMutexManager, MutexManager>();
            services.AddScoped<IConsoleManager, ConsoleManager>();
            services.AddScoped<IProcessWaiter, ProcessWaiter>();

            // ✅ Core Services
            services.AddSingleton<IOFileHandler, ExcelExecution>();
            services.AddSingleton<IOFileManagement, FileManage>();
            services.AddSingleton<IOFolderHandler, FolderHandler>();

            // ✅ Main Services (với đầy đủ DI)
            services.AddScoped<ITestkitManagerService, TestkitManagerService>();

            // ViewModels - not registered here since they need runtime parameters

            // Windows
            services.AddTransient<MainWindow>();

            return services;
        }
    }
}
