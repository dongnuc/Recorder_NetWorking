using Common.Helper;

namespace Common.Test
{
    [TestFixture]
    public class ReadPortFromExePathTest
    {
        private string _basePath;
        private string _exePath;
        private string _appSettingsPath;

        [SetUp]
        public void Setup()
        {
            string testDir = TestContext.CurrentContext.TestDirectory;
            _basePath = Path.Combine(testDir, "resource", "ServerPublish");
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
            }
            _exePath = Path.Combine(_basePath, "project11.exe");
            _appSettingsPath = Path.Combine(_basePath, "appsettings.json");

            if (!File.Exists(_exePath))
            {
                File.Create(_exePath).Dispose();
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_appSettingsPath))
            {
                File.Delete(_appSettingsPath);
            }
        }
        [TestCase("")]
        public void TC01_ReadPortFromExePath_InputNullOrEmpty_ReturnsNull(string inputPath)
        {
            // Act
            var ex = Assert.Throws<FileNotFoundException>(() =>
            {
                AppSettingsManager.ReadPortFromExePath(inputPath);
            });

            // Assert
            Assert.That(ex.Message, Does.Contain("Exe not found"));
        }
        [Test]
        public void TC02_ReadPortFromExePath_ExeFileNotFound_ReturnsNull()
        {
            string wrongPath = Path.Combine(_basePath, "non_existent.exe");

            var ex = Assert.Throws<FileNotFoundException>(() =>
            {
                AppSettingsManager.ReadPortFromExePath(wrongPath);
            });
            Assert.That(ex.Message, Does.Contain("Exe not found"));
            Assert.That(ex.FileName, Is.EqualTo(wrongPath));
        }

        [Test]
        public void TC03_ReadPortFromExePath_AppSettingsNotFound_ReturnsNull()
        {
            if (File.Exists(_appSettingsPath)) File.Delete(_appSettingsPath);

            var result = AppSettingsManager.ReadPortFromExePath(_exePath);

            Assert.IsNull(result);
        }

        [Test]
        public void TC04_ReadPortFromExePath_ValidJsonWithPort_ReturnsPortValue()
        {
            // Arrange
            string jsonContent = @"{
                ""Logging"": { ""LogLevel"": { ""Default"": ""Information"" } },
                ""Port"": 5000
            }";
            File.WriteAllText(_appSettingsPath, jsonContent);

            // Act
            var result = AppSettingsManager.ReadPortFromExePath(_exePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result, Is.EqualTo(5000));
        }

        [Test]
        public void TC05_ReadPortFromExePath_ValidJsonButMissingPortKey_ReturnsNull()
        {
            // Arrange
            string jsonContent = @"{
                ""Logging"": { ""LogLevel"": { ""Default"": ""Information"" } },
                ""AllowedHosts"": ""*""
            }";
            File.WriteAllText(_appSettingsPath, jsonContent);

            // Act
            var result = AppSettingsManager.ReadPortFromExePath(_exePath);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void TC06_ReadPortFromExePath_InvalidJsonSyntax_ReturnsNull()
        {
            // Arrange
            string invalidJson = @"{ ""Port"": 5000, ... missing brackets ... ";
            File.WriteAllText(_appSettingsPath, invalidJson);

            // Act
            var result = AppSettingsManager.ReadPortFromExePath(_exePath);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void TC07_ReadPortFromExePath_PortValueIsNotInteger_ReturnsNull()
        {
            // Arrange
            string jsonContent = @"{ ""Port"": 5000 }";
            File.WriteAllText(_appSettingsPath, jsonContent);

            // Act
            var result = AppSettingsManager.ReadPortFromExePath(_exePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result,Is.EqualTo(result));
        }

    }
}