using System.ComponentModel;

namespace Common.Interfaces.IOFile
{
    public interface IOFileManagement
    {
        void OpenExcelFile(string filePath);
        void CreateNewFile(string path, params string[] files);
        void ConfigForWritingFile<TConfig>(TConfig config, string type = "UI");

        // Dictionary<int, TestStage> ReadFileDetail(string path, int rowDataStart);
        // Task WriteFileDetailAsync(Dictionary<int, TestStage> list, int startIndex);

        Task<int> WriteFileAsync<TData>(BindingList<TData> listData, int startIndex = 0, int startColumn = 0);
        BindingList<TData> ReadFile<TData>(string path, string pattern, int rowDataStart, int columnDataStart) where TData : new();
    }
}