using Common.Resources;

namespace Common.Interfaces.Services
{
    public interface IFolderManager
    {
        string TestKitsRootFolder { get; }
        string ProjectName { get; }
        event Action<string> OnFolderStructureChanged;
        string Initialize(string saveLocation, string projectName);
        bool IsInitialized();
        string CreateTestKitFolder(string testKitName, bool createExcelTemplates = true);
        void CreateProjectHeaderFile(string protocol);
        string GetDetailExcelPath(string testKitFolderPath);

    }
}
