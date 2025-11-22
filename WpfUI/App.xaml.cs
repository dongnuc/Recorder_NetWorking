using Common.Interfaces.IOFile;
using Microsoft.Extensions.DependencyInjection;
using OfficeOpenXml;
using System.Windows;
using WpfUI.Services;


namespace WpfUI
{
    public partial class App : Application
    {
        private ServiceProvider _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            try
            {
                var fileHandler = _serviceProvider.GetRequiredService<IOFileHandler>();
                var folderHandler = _serviceProvider.GetRequiredService<IOFolderHandler>();
                var fileManagement = _serviceProvider.GetRequiredService<IOFileManagement>();

                var explorerWindow = new ProjectExplorerWindow(
                    _serviceProvider,
                    folderHandler,
                    fileHandler,
                    fileManagement);

                explorerWindow.Show();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"Failed to start application:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddUITestKitServices();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }
}