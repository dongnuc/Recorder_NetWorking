using Common.Interfaces.IOFile;
using Common.Resources; // Giả định chứa FileKeywords
using OfficeOpenXml; // Cần cài đặt thư viện EPPlus (ví dụ: qua NuGet)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FileManagement.FolderHelper
{
    public class FolderHandler : IOFolderHandler
    {
        public string CreateDirectory(string path, string mainFolder, params string[] subFolders)
        {
            string mainFolderPath = Path.Combine(path, mainFolder);

            try
            {
                if (!Directory.Exists(mainFolderPath))
                {
                    Directory.CreateDirectory(mainFolderPath);
                }

                foreach (var item in subFolders)
                {
                    if (string.IsNullOrWhiteSpace(item)) continue;

                    if (item.Contains("."))
                    {
                        string directoryName = Path.GetDirectoryName(item) ?? string.Empty;
                        string fileName = Path.GetFileName(item);

                        string fileDirectoryPath = Path.Combine(mainFolderPath, directoryName);

                        if (!Directory.Exists(fileDirectoryPath))
                        {
                            Directory.CreateDirectory(fileDirectoryPath);
                        }

                        string fullFilePath = Path.Combine(fileDirectoryPath, fileName);

                        if (!File.Exists(fullFilePath))
                        {
                            if (fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                            {
                                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                                using (var package = new ExcelPackage())
                                {
                                    package.Workbook.Worksheets.Add("Test Scenario");
                                    package.SaveAs(new FileInfo(fullFilePath));
                                }
                            }
                            else
                            {
                                File.Create(fullFilePath).Dispose();
                            }
                        }
                    }
                    else
                    {
                        string subFolderPath = Path.Combine(mainFolderPath, item);
                        if (!Directory.Exists(subFolderPath))
                        {
                            Directory.CreateDirectory(subFolderPath);
                        }
                    }
                }

                return mainFolderPath; 
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi khi tạo thư mục/file tại đường dẫn '{mainFolderPath}'");
                return string.Empty;
            }
        }

        public void CopyTemplateFromResource(string path, string srcDirectory, bool overwrite = true, params string[] specialFiles)
        {

            foreach (var fileName in specialFiles)
            {
                string srcPath = Path.Combine(srcDirectory, fileName);
                string targetPath = Path.Combine(path, fileName);

                if (!File.Exists(srcPath))
                {
                    throw new FileNotFoundException(
                        $"Template file not found: {srcPath}. \n\n" +
                        $"Please make sure the file '{fileName}' exists in '{srcDirectory}' " +
                        "and its 'Copy to Output Directory' property is set to 'Copy if newer'.",
                        srcPath);
                }
                Copy(srcPath, targetPath, overwrite);
            }
        }

        public void ReplaceSheetExcel(string srcPath, string desPath, string sheetName)
        {
            var srcFile = new FileInfo(srcPath);
            var desFile = new FileInfo(desPath);

            using (var srcPackage = new ExcelPackage(srcFile))
            using (var desPackage = new ExcelPackage(desFile))
            {
                var srcSheet = srcPackage.Workbook.Worksheets[sheetName];

                if (srcSheet == null)
                {
                    throw new Exception($"Sheet ${sheetName} not found in the source template file: {srcPath}");
                }

                var desSheet = desPackage.Workbook.Worksheets[sheetName];
                if (desSheet != null)
                {
                    desPackage.Workbook.Worksheets.Delete(desSheet);
                }

                desPackage.Workbook.Worksheets.Add(sheetName, srcSheet);

                desPackage.Save();
            }
        }

        public void Copy(string sourcePath, string destinationPath, bool overwrite)
        {
            if (File.Exists(sourcePath))
            {
                string targetFilePath = destinationPath;

                if (Directory.Exists(destinationPath))
                {
                    targetFilePath = Path.Combine(destinationPath, Path.GetFileName(sourcePath));
                }
                File.Copy(sourcePath, targetFilePath, overwrite);
            }
            else if (Directory.Exists(sourcePath))
            {
                if (!Directory.Exists(destinationPath))
                {
                    Directory.CreateDirectory(destinationPath);
                }

                CopyDirectory(new DirectoryInfo(sourcePath), new DirectoryInfo(destinationPath), overwrite);
            }
            else
            {
                throw new FileNotFoundException("Source file or directory not found.", sourcePath);
            }
        }

        private void CopyDirectory(DirectoryInfo source, DirectoryInfo target, bool overwrite)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
            {
                CopyDirectory(dir, target.CreateSubdirectory(dir.Name), overwrite);
            }

            foreach (FileInfo file in source.GetFiles())
            {
                string targetFilePath = Path.Combine(target.FullName, file.Name);
                file.CopyTo(targetFilePath, overwrite);
            }
        }

        public void DeleteFileOrFolder(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                else if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
                else
                {
                    throw new FileNotFoundException($"The file or directory at path '{path}' does not exist.");
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void OpenFolderInExplorer(string path)
        {
            if (Directory.Exists(path))
            {
                Process.Start("explorer.exe", path);
            }
            else
            {
                throw new DirectoryNotFoundException("The specified folder does not exist.");
            }
        }

        public bool SearchFiles(string folderPath, string searchString)
        {
            foreach (var file in Directory.GetFiles(folderPath))
            {
                if (Path.GetFileName(file).IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        public List<string> SearchAllFolderPathInFolder(string bigFolderPath)
        {
            var folderPaths = new List<string>();
            foreach (var folder in Directory.GetDirectories(bigFolderPath, "*", SearchOption.TopDirectoryOnly))
            {
                folderPaths.Add(folder);
            }
            return folderPaths;
        }
    }
}