using Common.Models.Entities;

namespace Common.Interfaces.Services
{
    public interface IExcelService
    {
        void ExportToExcelParams(string filePath, params (string SheetName, ICollection<object> Data)[] sheetsData);
        Dictionary<int, TestStage> LoadTestDataFromDetailFile(string detailFilePath);
        void CreateTemplateFile(string filePath, string[] sheetNames);
        void ExportToExcel(string filePath, Dictionary<int, TestStage> testStages);
        bool ValidateExcelFile(string filePath);
    }
}
