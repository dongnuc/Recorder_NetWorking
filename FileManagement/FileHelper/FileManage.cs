using Common.Interfaces.IOFile;
using Common.Models.Entities;
using FileManagement.FileHelper.FileHandler; // Namespace chứa ExcelExecution
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileManagement.FileHelper
{
    public class FileManage : IOFileManagement
    {
        private readonly IOFileHandler _fileHandler;

        public FileManage()
        {
            this._fileHandler = new ExcelExecution();
        }

        public FileManage(IOFileHandler fileHandler)
        {
            _fileHandler = fileHandler ?? throw new ArgumentNullException(nameof(fileHandler));
        }


        public void OpenExcelFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Lỗi khi mở file Excel: {ex.Message}");
                }
            }
        }

        public void CreateNewFile(string path, params string[] files)
        {
            foreach (var item in files)
            {
                if (item.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string pathFile = Path.Combine(path, item);
                if (!File.Exists(pathFile))
                {
                    try
                    {
                        File.Create(pathFile).Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Lỗi khi tạo file phụ: {ex.Message}");
                    }
                }
            }
        }

        public void ConfigForWritingFile<TConfig>(TConfig config, string type = "UI")
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            _fileHandler.ConfigForWritingFile(config, type);
        }


        public async Task<int> WriteFileAsync<TData>(BindingList<TData> listData, int startIndex = 0, int startColumn = 0)
        {
            return await _fileHandler.WriteFileDataAsync(listData, startIndex, startColumn);
        }

        public BindingList<TData> ReadFile<TData>(string path, string pattern, int rowDataStart, int columnDataStart) where TData : new()
        {
            return _fileHandler.ReadFileData<TData>(path, pattern, rowDataStart, columnDataStart);
        }
    }
}