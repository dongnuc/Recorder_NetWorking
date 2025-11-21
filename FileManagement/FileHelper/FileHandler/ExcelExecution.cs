using Common.Interfaces.IOFile;
using Common.Logging;
using Common.Models.Entities;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using LicenseContext = OfficeOpenXml.LicenseContext;
using LisenceContext = OfficeOpenXml.LicenseContext;

namespace FileManagement.FileHelper.FileHandler
{
    public class ExcelExecution : IOFileHandler
    {
        private string _excelFilePath;

        public ExcelExecution()
        {
            ExcelPackage.LicenseContext = LisenceContext.NonCommercial;
        }

        public void ConfigForWritingFile<TConfig>(TConfig config, string type)
        {
            if (!(config is ConfigModel configModel))
            {
                throw new ArgumentException("Config phải là loại ConfigModel.", nameof(config));
            }

            string excelFileName = $"{configModel.ProjectName}_Scenario.xlsx";
            _excelFilePath = Path.Combine(configModel.SaveLocation, configModel.ProjectName, excelFileName);

            if (!File.Exists(_excelFilePath))
            {
                throw new FileNotFoundException($"File Excel không tồn tại: {_excelFilePath}");
            }

            try
            {
                using (var package = new ExcelPackage(new FileInfo(_excelFilePath)))
                {
                    // --- 1. Ghi vào sheet "Config" ---
                    var configSheet = package.Workbook.Worksheets["Config"];
                    configSheet.Cells["A1"].Value = "Project Name:";
                    configSheet.Cells["B1"].Value = configModel.ProjectName;
                    configSheet.Cells["A2"].Value = "Client Path:";
                    configSheet.Cells["B2"].Value = configModel.ClientPath;
                    configSheet.Cells["A3"].Value = "Server Path:";
                    configSheet.Cells["B3"].Value = configModel.ServerPath;
                    configSheet.Cells["A4"].Value = "Protocol:";
                    configSheet.Cells["B4"].Value = configModel.Protocol;
                    configSheet.Cells["A1:A4"].Style.Font.Bold = true;
                    configSheet.Cells["A1:B4"].AutoFitColumns();

                    // --- 2. Ghi tiêu đề "InputClient" ---
                    var inputClientSheet = package.Workbook.Worksheets["InputClient"];
                    WriteHeaders(inputClientSheet, 1, typeof(User));

                    // --- 3. Ghi tiêu đề "OutputClient" ---
                    var outputClientSheet = package.Workbook.Worksheets["OutputClient"];
                    WriteHeaders(outputClientSheet, 1, typeof(Client));

                    // --- 4. Ghi tiêu đề "OutputServer" ---
                    var outputServerSheet = package.Workbook.Worksheets["OutputServer"];
                    WriteHeaders(outputServerSheet, 1, typeof(Server));

                    package.Save();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Không thể ghi Config vào file Excel: {ex.Message}", ex);
            }
        }

        private void WriteHeaders(ExcelWorksheet worksheet, int row, Type modelType)
        {
            var properties = modelType.GetProperties();
            for (int col = 0; col < properties.Length; col++)
            {
                var cell = worksheet.Cells[row, col + 1];
                cell.Value = properties[col].Name;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }
            worksheet.Cells[row, 1, row, properties.Length].AutoFitColumns();
        }

        private string GetSheetNameForType(Type type)
        {
            if (type == typeof(User)) return "InputClient";
            if (type == typeof(Client)) return "OutputClient";
            if (type == typeof(Server)) return "OutputServer";

            return "Config";
        }

        public async Task<int> WriteFileDataAsync<TData>(BindingList<TData> listData, int startIndex, int startColumn)
        {
            if (listData == null) throw new ArgumentNullException(nameof(listData));
            if (string.IsNullOrEmpty(_excelFilePath))
                throw new InvalidOperationException("ConfigForWritingFile phải được gọi trước khi ghi dữ liệu.");

            string sheetName = GetSheetNameForType(typeof(TData));
            int row = (startIndex == 0) ? 2 : startIndex;
            int col = (startColumn == 0) ? 1 : startColumn;

            using (var package = new ExcelPackage(new FileInfo(_excelFilePath)))
            {
                var worksheet = package.Workbook.Worksheets[sheetName];
                if (worksheet == null) throw new Exception($"Sheet '{sheetName}' không tồn tại.");

                if (worksheet.Dimension != null && worksheet.Dimension.Rows >= row)
                {
                    worksheet.DeleteRow(row, worksheet.Dimension.Rows - row + 1);
                }

                PropertyInfo[] props = typeof(TData).GetProperties();
                foreach (var item in listData)
                {
                    for (int i = 0; i < props.Length; i++)
                    {
                        var value = props[i].GetValue(item)?.ToString() ?? string.Empty;
                        worksheet.Cells[row, col + i].Value = value;
                    }
                    row++;
                }

                await package.SaveAsync();
                return row - 1;
            }
        }

        public BindingList<TData> ReadFileData<TData>(string path, string pattern, int rowDataStart, int columnDataStart) where TData : new()
        {
            var result = new BindingList<TData>();
            FileInfo fileReader = new FileInfo(path);
            if (!fileReader.Exists) throw new FileNotFoundException("File Not Found!", path);

            string sheetName = GetSheetNameForType(typeof(TData));
            int rowStart = (rowDataStart == 0) ? 2 : rowDataStart;
            int colStart = (columnDataStart == 0) ? 1 : columnDataStart;

            using (ExcelPackage p = new ExcelPackage(fileReader))
            {
                var worksheet = p.Workbook.Worksheets[sheetName];
                if (worksheet == null) return result;

                int rows = worksheet.Dimension?.Rows ?? 0;
                if (rows < rowStart) return result;

                PropertyInfo[] props = typeof(TData).GetProperties();

                for (int i = rowStart; i <= rows; i++)
                {
                    TData newData = new TData();
                    bool checkData = false;

                    for (int propIndex = 0; propIndex < props.Length; propIndex++)
                    {
                        var prop = props[propIndex];
                        int j = colStart + propIndex;

                        string cellData = worksheet.Cells[i, j].Text?.Trim() ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(cellData)) continue;
                        checkData = true;

                        try
                        {
                            var convertedValue = Convert.ChangeType(cellData, prop.PropertyType);
                            prop.SetValue(newData, convertedValue);
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException($"Lỗi parse dữ liệu tại hàng {i}, cột {j} (Sheet: {sheetName}): {ex.Message}");
                        }
                    }

                    if (checkData) result.Add(newData);
                }
            }
            return result;
        }


        public void ModifyExcelCellContent(string excelPath, string sheetName, int row, int column, string newText)
        {
            try
            {
                if (!File.Exists(excelPath)) return;
                using (ExcelPackage package = new ExcelPackage(new FileInfo(excelPath)))
                {
                    ExcelWorksheet worksheet = package.Workbook.Worksheets[sheetName];
                    if (worksheet == null) return;
                    worksheet.Cells[row, column].Value = newText;
                    package.Save();
                }
            }
            catch (Exception) { return; }
        }

        public (int Row, int Column) FindStringInExcel(string excelPath, string sheetName, string searchString)
        {
            if (!File.Exists(excelPath)) return (-1, -1);
            using (var package = new ExcelPackage(new FileInfo(excelPath)))
            {
                var worksheet = package.Workbook.Worksheets[sheetName];
                if (worksheet == null || worksheet.Dimension == null) return (-1, -1);

                for (int row = worksheet.Dimension.Start.Row; row <= worksheet.Dimension.End.Row; row++)
                {
                    for (int column = worksheet.Dimension.Start.Column; column <= worksheet.Dimension.End.Column; column++)
                    {
                        var cellValue = worksheet.Cells[row, column].Text;
                        if (cellValue != null && cellValue.Contains(searchString, StringComparison.OrdinalIgnoreCase))
                        {
                            return (row, column);
                        }
                    }
                }
                return (-1, -1);
            }
        }

        public async Task AppendNewRow(string excelPath, string sheetName, string name, string description)
        {
            if (!File.Exists(excelPath)) return;
            using (var package = new ExcelPackage(new FileInfo(excelPath)))
            {
                var worksheet = package.Workbook.Worksheets[sheetName];
                if (worksheet == null) return;
                int row = (worksheet.Dimension?.Rows ?? 1) + 1;
                worksheet.Cells[row, 1].Value = name;
                worksheet.Cells[row, 2].Value = description;
                await package.SaveAsync();
            }
        }

        public void DeleteRow(string excelPath, string sheetName, int rowToDelete)
        {
            if (!File.Exists(excelPath)) return;
            using (var package = new ExcelPackage(new FileInfo(excelPath)))
            {
                var worksheet = package.Workbook.Worksheets[sheetName];
                if (worksheet == null) return;
                if (rowToDelete < 1 || rowToDelete > worksheet.Dimension?.End.Row) return;
                worksheet.DeleteRow(rowToDelete);
                package.Save();
            }
        }

        public string GetCellValue(string excelPath, string sheetName, int row, int column)
        {
            if (!File.Exists(excelPath)) return null;
            using (var package = new ExcelPackage(new FileInfo(excelPath)))
            {
                var worksheet = package.Workbook.Worksheets[sheetName];
                if (worksheet == null) return null;
                return worksheet.Cells[row, column].Text;
            }
        }

        public async Task AppendNewColumn(string excelPath, string sheetName, string name, string description)
        {
            if (!File.Exists(excelPath)) return;
            using (var package = new ExcelPackage(new FileInfo(excelPath)))
            {
                var worksheet = package.Workbook.Worksheets[sheetName];
                if (worksheet == null) return;
                int column = (worksheet.Dimension?.Columns ?? 1) + 1;
                worksheet.Cells[1, column].Value = name;
                worksheet.Cells[2, column].Value = description;
                await package.SaveAsync();
            }
        }

        public void DeleteColumn(string excelPath, string sheetName, int columnToDelete)
        {
            if (!File.Exists(excelPath)) return;
            using (var package = new ExcelPackage(new FileInfo(excelPath)))
            {
                var worksheet = package.Workbook.Worksheets[sheetName];
                if (worksheet == null) return;
                if (columnToDelete < 1 || columnToDelete > worksheet.Dimension?.End.Column) return;
                worksheet.DeleteColumn(columnToDelete);
                package.Save();
            }
        }

        public void ExportToExcelParams(string filePath, params (string SheetName, ICollection<object> Data)[] sheetsData)
        {
            try
            {
                if (sheetsData == null || sheetsData.Length == 0)
                    throw new ArgumentException("Không có dữ liệu để xuất.");

                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                using var package = new ExcelPackage();

                foreach (var (sheetName, data) in sheetsData)
                {
                    var worksheet = package.Workbook.Worksheets.Add(sheetName);
                    if (data == null || !data.Any()) continue;

                    var firstItem = data.FirstOrDefault(d => d != null);
                    if (firstItem == null) continue;


                    var properties = firstItem.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

                    // ===== HEADER =====
                    for (int i = 0; i < properties.Length; i++)
                    {
                        worksheet.Cells[1, i + 1].Value = properties[i].Name;
                    }

                    using (var headerRange = worksheet.Cells[1, 1, 1, properties.Length])
                    {
                        headerRange.Style.Font.Bold = true;
                    }


                    // Thay thế LoadFromCollection bằng vòng lặp thủ công để đảm bảo hoạt động với ICollection<object>
                    if (data.Any())
                    {
                        int currentRow = 2;
                        foreach (var item in data)
                        {
                            if (item == null) continue;
                            for (int i = 0; i < properties.Length; i++)
                            {
                                var value = properties[i].GetValue(item);
                                worksheet.Cells[currentRow, i + 1].Value = value;
                            }
                            currentRow++;
                        }
                    }

                    // ===== TÙY CHỈNH CỘT (Giữ nguyên) =====
                    const double MAX_COLUMN_WIDTH = 60;
                    const double MIN_COLUMN_WIDTH = 10;

                    for (int i = 1; i <= properties.Length; i++)
                    {
                        var column = worksheet.Column(i);
                        var propertyName = properties[i - 1].Name;
                        column.Style.WrapText = true;
                        column.AutoFit();
                        if (column.Width > MAX_COLUMN_WIDTH) column.Width = MAX_COLUMN_WIDTH;
                        if (column.Width < MIN_COLUMN_WIDTH) column.Width = MIN_COLUMN_WIDTH;
                    }

                    if (worksheet.Dimension != null)
                    {
                        worksheet.Cells[worksheet.Dimension.Address].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
                    }
                }

                package.SaveAs(new FileInfo(filePath));

                LogManager.Instance.LogInfomation($"Xuất file Excel thành công!\nĐường dẫn: {filePath}");
            }
            catch (Exception ex)
            {
                // ... (phần xử lý lỗi giữ nguyên)
                try
                {
                    string logPath = Path.Combine(Path.GetDirectoryName(filePath) ?? AppDomain.CurrentDomain.BaseDirectory, "ExportLog.txt");
                    File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Lỗi khi export Excel:\n{ex}\n\n");
                    LogManager.Instance.LogError($"Xuất Excel thất bại!\\nChi tiết lỗi đã được ghi tại:\\n{logPath}");
                }
                catch
                {
                    LogManager.Instance.LogError($"Xuất Excel thất bại!\\nChi tiết lỗi đã được ghi tại:\\n{ex.Message}");
                }
            }
        }

    }
}