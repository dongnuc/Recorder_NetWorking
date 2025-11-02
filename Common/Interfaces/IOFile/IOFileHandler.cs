using Common.Models.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Interfaces.IOFile
{
    public interface IOFileHandler
    {
        void ConfigForWritingFile<TConfig>(TConfig config, string type);

        Task<int> WriteFileDataAsync<TData>(BindingList<TData> listData, int startIndex, int startColumn);
        BindingList<TData> ReadFileData<TData>(string path, string pattern, int rowDataStart, int columnDataStart) where TData : new();

        void ModifyExcelCellContent(string excelPath, string sheetName, int row, int column, string newText);
        (int Row, int Column) FindStringInExcel(string excelPath, string sheetName, string searchString);
        Task AppendNewRow(string excelPath, string sheetName, string name, string description);
        void DeleteRow(string excelPath, string sheetName, int rowToDelete);
        string GetCellValue(string excelPath, string sheetName, int row, int column);
        Task AppendNewColumn(string excelPath, string sheetName, string name, string description);
        void DeleteColumn(string excelPath, string sheetName, int columnToDelete);

    }
}