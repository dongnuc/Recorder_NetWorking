using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Interfaces.IOFile
{
    public interface IOFolderHandler
    {
        string CreateDirectory(string path, string mainFolder, params string[] subFolders);
        void CopyTemplateFromResource(string path, string srcDirectory, bool overwrite, params string[] specialFiles);
        void Copy(string sourcePath, string destinationPath, bool overwrite);
        void DeleteFileOrFolder(string path);
        void OpenFolderInExplorer(string path);
        void ReplaceSheetExcel(string srcPath, string desPath);
        bool SearchFiles(string folderPath, string searchString);
        List<string> SearchAllFolderPathInFolder(string bigFolderPath);
    }
}
