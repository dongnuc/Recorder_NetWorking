using FileManagement.FolderHelper;

namespace FileManagement.Test
{
    [TestFixture]
    public class FolderHandlerTest
    {
        // create folder
        private FolderHandler _folderHandler;
        private string _testBasePath;

        [SetUp]
        public void Setup()
        {
            _folderHandler = new FolderHandler();
            _testBasePath = Path.Combine(Path.GetTempPath(),"Resource" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testBasePath);
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_testBasePath))
            {
                try
                {
                    Directory.Delete(_testBasePath, true);
                }
                catch (Exception ex)
                {

                }
            }
        }
        // create main folder
        [Test]
        public void TC1_CreateDirectory_OnlyMainFolder_ShouldCreateDirectory()
        {
            // Arrange
            string mainFolderName = "MainProject";

            // Act
            string result = _folderHandler.CreateDirectory(_testBasePath, mainFolderName);

            // Assert
            string expectedPath = Path.Combine(_testBasePath, mainFolderName);
            Assert.That(result, Is.EqualTo(expectedPath));
            Assert.That(Directory.Exists(expectedPath), Is.True, "Thư mục chính không được tạo.");
        }

        // create sub folder
        [Test]
        public void TC2_CreateDirectory_WithSubFolders_ShouldCreateAllDirectories()
        {
            // Arrange
            string mainFolder = "ProjectWithSubs";
            string[] subFolders = { "Client", "Server", "Database" };

            // Act
            _folderHandler.CreateDirectory(_testBasePath, mainFolder, subFolders);

            // Assert
            foreach (var sub in subFolders)
            {
                string subPath = Path.Combine(_testBasePath, mainFolder, sub);
                Assert.That(Directory.Exists(subPath), Is.True, $"Subfolder {sub} không được tạo.");
            }
        }

        [Test]
        public void TC3_CreateDirectory_WithTextFile_ShouldCreateFile()
        {
            // Arrange
            string mainFolder = "ProjectWithFile";
            string fileName = "readme.txt";

            // Act
            _folderHandler.CreateDirectory(_testBasePath, mainFolder, fileName);

            // Assert
            string filePath = Path.Combine(_testBasePath, mainFolder, fileName);
            Assert.That(File.Exists(filePath), Is.True, "File text không được tạo.");
        }

        /// <summary>
        /// Tạo file nằm trong đường dẫn lồng nhau (Nested Path).
        /// Ví dụ: "Meta/Config.json" -> Phải tạo folder Meta rồi tạo file Config.json.
        /// </summary>
        [Test]
        public void TC4_CreateDirectory_NestedFilePath_ShouldCreateParentFolderAndFile()
        {
            // Arrange
            string mainFolder = "NestedProject";
            // Giả lập đường dẫn tương đối: Meta\Config.json
            string nestedFile = Path.Combine("Meta", "Config.json");

            // Act
            _folderHandler.CreateDirectory(_testBasePath, mainFolder, nestedFile);

            // Assert
            string fullPath = Path.Combine(_testBasePath, mainFolder, nestedFile);
            Assert.That(File.Exists(fullPath), Is.True, "Nested file không được tạo.");

            string parentDir = Path.GetDirectoryName(fullPath);
            Assert.That(Directory.Exists(parentDir), Is.True, "Folder cha của file không được tạo.");
        }

        [Test]
        public void TC5_CreateDirectory_InvalidPath_ShouldReturnEmptyStringAndLog()
        {
            // Arrange
            string invalidPath = "Inva|id?P@th";

            // Act
            var ex = Assert.Throws<Exception>(() =>
            {
                _folderHandler.CreateDirectory(_testBasePath, invalidPath);
            });
            // Assert
            Assert.That(ex.Message, Does.Contain($"Error to create folder/file in path '{_testBasePath}'"));
        }

        [Test]
        public void TC6_CreateDirectory_FolderWithDotInName_ShouldCreateDirectory_NotFile()
        {
            // Arrange
            string mainFolder = "DotFolderProject";
            string folderNameWithDot = "Version1.0"; // Mong muốn đây là FOLDER

            _folderHandler.CreateDirectory(_testBasePath, mainFolder, folderNameWithDot);

            string folderPath = Path.Combine(_testBasePath, mainFolder, folderNameWithDot);

            Assert.That(Directory.Exists(folderPath), Is.True,
                $"Lỗi: '{folderNameWithDot}' bị tạo thành File thay vì Folder!");
        }

    }
}
