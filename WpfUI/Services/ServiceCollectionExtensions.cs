using Common.Interfaces.IOFile;
using Common.Interfaces.Services;
using FileManagement.FileHelper;
using FileManagement.FileHelper.FileHandler;
using FileManagement.FolderHelper;
using Microsoft.Extensions.DependencyInjection;
using ProcessManagement.Services;
using TestKitManagement.Services;
using WpfUI.Controls;
using WpfUI.ViewModels;

namespace WpfUI.Services
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// ✅ Register tất cả services cho UITestKit
        /// </summary>
        public static IServiceCollection AddUITestKitServices(this IServiceCollection services)
        {
            // Logging
            //services.AddSingleton<ISystemLogger>(sp => LogManager.Instance);

            // Core Services
            services.AddSingleton<IOFileHandler, ExcelExecution>();
            services.AddSingleton<IOFileManagement, FileManage>();
            services.AddSingleton<IOFolderHandler, FolderHandler>();

            services.AddScoped<IProcessManager, ProcessManager>();
            services.AddScoped<ITestkitManagerService, TestkitManagerService>();
            // ViewModels
            services.AddTransient<MainMenuViewModel>();

            // Windows
            services.AddTransient<MainWindow>();

            // Controls
            //services.AddTransient<LogViewerControl>();

            return services;
        }
    }
}
