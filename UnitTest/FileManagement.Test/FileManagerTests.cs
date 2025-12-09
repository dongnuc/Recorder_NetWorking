using FileManagement.FolderHelper;

namespace FileManagement.Test
{
    [TestFixture]
    public class FileManagerTests
    {
        // delete folder
        private string _testDirectory;
        private FolderHandler _folderHandler;

        [SetUp]
        public void SetUp()
        {
            _folderHandler = new FolderHandler();
            _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }


        [Test]
        public void TC01_DeleteFileOrFolder_PathIsExistingFile_ShouldDeleteFile()
        {
            string fileName = "testfile.txt";
            string filePath = Path.Combine(_testDirectory, fileName);
            File.WriteAllText(filePath, "Dummy content");

            Assert.IsTrue(File.Exists(filePath), "Pre-condition: File should exist before delete.");
            _folderHandler.DeleteFileOrFolder(filePath);
            Assert.IsFalse(File.Exists(filePath), "File should be deleted.");
        }


        [Test]
        public void TC02_DeleteFileOrFolder_PathIsExistingFolder_ShouldDeleteFolder()
        {
            string subFolderName = "SubFolder";
            string folderPath = Path.Combine(_testDirectory, subFolderName);
            Directory.CreateDirectory(folderPath); // Tạo folder thật

            Assert.IsTrue(Directory.Exists(folderPath), "Pre-condition: Folder should exist.");

            _folderHandler.DeleteFileOrFolder(folderPath);

            Assert.IsFalse(Directory.Exists(folderPath), "Folder should be deleted.");
        }

        [Test]
        public void TC03_DeleteFileOrFolder_PathIsFolderWithContent_ShouldDeleteRecursively()
        {
            string folderPath = Path.Combine(_testDirectory, "FolderWithData");
            Directory.CreateDirectory(folderPath);
            string childFile = Path.Combine(folderPath, "child.txt");
            File.WriteAllText(childFile, "content"); 

            // 2. Act
            _folderHandler.DeleteFileOrFolder(folderPath);

            // 3. Assert
            Assert.IsFalse(Directory.Exists(folderPath), "Parent folder should be deleted.");
            Assert.IsFalse(File.Exists(childFile), "Child file should be deleted.");
        }

        [Test]
        public void TC04_DeleteFileOrFolder_PathDoesNotExist_ShouldThrowFileNotFoundException()
        {
            // 1. Arrange
            string nonExistentPath = Path.Combine(_testDirectory, "GhostFile.txt");

            // 2. Act & 3. Assert
            var ex = Assert.Throws<FileNotFoundException>(() => _folderHandler.DeleteFileOrFolder(nonExistentPath));

            StringAssert.Contains(nonExistentPath, ex.Message);
        }

        [Test]
        public void TC05_DeleteFileOrFolder_PathIsEmptyString_ShouldThrowFileNotFoundException()
        {
            // 1. Arrange
            string emptyPath = "";

            // 2. Act & 3. Assert
            Assert.Throws<FileNotFoundException>(() => _folderHandler.DeleteFileOrFolder(emptyPath));
        }

        [Test]
        public void TC06_DeleteFileOrFolder_PathIsNull_ShouldThrowFileNotFoundException()
        {
            // 1. Arrange
            string nullPath = null;

            // 2. Act & 3. Assert
            Assert.Throws<FileNotFoundException>(() => _folderHandler.DeleteFileOrFolder(nullPath));
        }
    }
}
