using Common.Interfaces.IOFile;
using FileManagement.FileHelper;
using FileManagement.FileHelper.FileHandler;
using FileManagement.FolderHelper;
using OfficeOpenXml;
using System.Windows;
using WpfUI.ViewModels;

namespace WpfUI
{
    public partial class App : Application
    {
        //protected override void OnStartup(StartupEventArgs e)
        //{
        //    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        //    base.OnStartup(e);

        //    IOFolderHandler folderHandler = new FolderHandler();
        //    IOFileHandler fileHandler = new ExcelExecution();

        //    ProjectSetupWindow setupView = new ProjectSetupWindow(folderHandler, fileHandler);

        //    bool? setupResult = setupView.ShowDialog();

        //    if (setupResult == true)
        //    {
        //        string projectPath = setupView.CreatedProjectPath;
        //        string clientExe = setupView.ClientExePath;
        //        string serverExe = setupView.ServerExePath;

        //        var mainViewModel = new MainMenuViewModel(
        //            folderHandler,
        //            fileHandler,
        //            projectPath,
        //            clientExe,
        //            serverExe
        //        );

        //        var mainMenuView = new MainMenu();
        //        mainMenuView.DataContext = mainViewModel;
        //        mainMenuView.Show();

        //    }
        //}
    }
}