using FileManagement.FolderHelper;

namespace FileManagement.Test
{
    [TestFixture]
    public class FileCopierTests
    {
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
        public void TC01_Copy_FileToNewFile_ShouldSuccess()
        {
            // Arrange
            string sourceFile = Path.Combine(_testDirectory, "source.txt");
            string destFile = Path.Combine(_testDirectory, "dest.txt");
            File.WriteAllText(sourceFile, "Hello World");

            // Act
            _folderHandler.Copy(sourceFile, destFile, false);

            // Assert
            Assert.That(File.Exists(destFile), Is.True, "File đích phải được tạo thành công.");
            Assert.That(File.ReadAllText(destFile), Is.EqualTo("Hello World"), "Nội dung file copy phải khớp với file gốc.");
        }

        [Test]
        public void TC02_Copy_FileToExistingFolder_ShouldCopyFileIntoFolder()
        {
            // Arrange
            string sourceFile = Path.Combine(_testDirectory, "data.txt");
            File.WriteAllText(sourceFile, "Content");

            string destFolder = Path.Combine(_testDirectory, "Backup");
            Directory.CreateDirectory(destFolder);

            // Act
            _folderHandler.Copy(sourceFile, destFolder, false);

            // Assert
            string expectedFilePath = Path.Combine(destFolder, "data.txt");
            Assert.That(File.Exists(expectedFilePath), Is.True, "File phải nằm bên trong thư mục đích.");
        }

        [Test]
        public void TC03_Copy_FolderToNewFolder_ShouldCopyRecursively()
        {
            // Arrange
            string srcFolder = Path.Combine(_testDirectory, "SrcDir");
            Directory.CreateDirectory(srcFolder);
            File.WriteAllText(Path.Combine(srcFolder, "file1.txt"), "1");

            string subDir = Path.Combine(srcFolder, "SubDir");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "file2.txt"), "2");

            string destFolder = Path.Combine(_testDirectory, "DestDir");

            // Act
            _folderHandler.Copy(srcFolder, destFolder, false);

            // Assert
            Assert.That(Directory.Exists(destFolder), Is.True, "Thư mục đích phải được tạo.");
            Assert.That(File.Exists(Path.Combine(destFolder, "file1.txt")), Is.True, "File cấp 1 phải được copy.");
            Assert.That(Directory.Exists(Path.Combine(destFolder, "SubDir")), Is.True, "Thư mục con phải được copy.");
            Assert.That(File.Exists(Path.Combine(destFolder, "SubDir", "file2.txt")), Is.True, "File trong thư mục con phải được copy.");
        }

        [Test]
        public void TC04_Copy_SourcePathIsEmpty_ShouldThrowFileNotFoundException()
        {
            // Arrange
            string source = "";
            string dest = Path.Combine(_testDirectory, "dest.txt");

            // Act & Assert
            // Sử dụng cú pháp Throws.TypeOf trong Assert.That
            var ex = Assert.Throws<FileNotFoundException>(() => _folderHandler.Copy(source, dest, false));

            Assert.That(ex.Message, Is.EqualTo("Source file or directory not found."), "Message lỗi không đúng như kỳ vọng.");
        }

        [Test]
        public void TC05_Copy_SourceDoesNotExist_ShouldThrowFileNotFoundException()
        {
            // Arrange
            string ghostFile = Path.Combine(_testDirectory, "ghost.txt");
            string dest = Path.Combine(_testDirectory, "dest.txt");

            // Act & Assert
            Assert.That(() => _folderHandler.Copy(ghostFile, dest, false), Throws.TypeOf<FileNotFoundException>(), "Phải ném lỗi nếu source không tồn tại.");
        }

        [Test]
        public void TC06_Copy_FileOverwriteFalse_DestFileExists_ShouldThrowIOException()
        {
            // Arrange
            string source = Path.Combine(_testDirectory, "source.txt");
            string dest = Path.Combine(_testDirectory, "dest.txt");

            File.WriteAllText(source, "New Content");
            File.WriteAllText(dest, "Old Content");

            // Act & Assert
            Assert.That(() => _folderHandler.Copy(source, dest, false), Throws.TypeOf<IOException>(), "Phải ném IOException khi file đích đã tồn tại và overwrite=false.");
        }

        [Test]
        public void TC07_Copy_FileToNonExistentDirectory_ShouldAutoCreateDirectoryAndSuccess()
        {
            // Arrange
            string source = Path.Combine(_testDirectory, "source.txt");
            File.WriteAllText(source, "content");

            string nonExistentFolder = Path.Combine(_testDirectory, "NewFolder");
            string dest = Path.Combine(nonExistentFolder, "copied.txt");

            // Act
            try
            {
                _folderHandler.Copy(source, dest, true);
            }
            catch (DirectoryNotFoundException)
            {
            }

            // Assert
            Assert.That(File.Exists(dest), Is.True, "Test này FAIL vì code không tự tạo thư mục cha 'NewFolder'.");
        }
    }
}
