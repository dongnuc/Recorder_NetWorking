using Common.Helper;
using Common.Interfaces.Logging;
using Moq;

namespace Common.Test
{
    [TestFixture]
    public class ReadPortFromExePathTest
    {
        private string _basePath;
        private string _exePath;
        private string _appSettingsPath;
        private Mock<ISystemLogger> _mockLogger;
        private ISystemLogger _logger;

        [SetUp]
        public void Setup()
        {
            // Setup mock logger
            _mockLogger = new Mock<ISystemLogger>();
            _logger = _mockLogger.Object;

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
        public void TC01_ReadPortFromExePath_InputNullOrEmpty_ThrowsException(string inputPath)
        {
            // Act & Assert
            var ex = Assert.Throws<FileNotFoundException>(() =>
            {
                AppSettingsManager.ReadPortFromExePath(inputPath, _logger);
            });

            Assert.That(ex.Message, Does.Contain("Exe not found"));
        }

        [Test]
        public void TC02_ReadPortFromExePath_ExeFileNotFound_ThrowsException()
        {
            // Arrange
            string wrongPath = Path.Combine(_basePath, "non_existent.exe");

            // Act & Assert
            var ex = Assert.Throws<FileNotFoundException>(() =>
            {
                AppSettingsManager.ReadPortFromExePath(wrongPath, _logger);
            });
            
            Assert.That(ex.Message, Does.Contain("Exe not found"));
            Assert.That(ex.FileName, Is.EqualTo(wrongPath));
        }

        [Test]
        public void TC03_ReadPortFromExePath_AppSettingsNotFound_ReturnsNull()
        {
            // Arrange
            if (File.Exists(_appSettingsPath)) 
                File.Delete(_appSettingsPath);

            // Act
            var result = AppSettingsManager.ReadPortFromExePath(_exePath, _logger);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(
                x => x.LogWarning(It.Is<string>(s => s.Contains("appsettings. json not found"))), 
                Times.Once);
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
            var result = AppSettingsManager.ReadPortFromExePath(_exePath, _logger);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result, Is.EqualTo(5000));
            _mockLogger.Verify(
                x => x.LogInfomation(It.Is<string>(s => s.Contains("Port value read") && s.Contains("5000"))), 
                Times.Once);
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
            var result = AppSettingsManager.ReadPortFromExePath(_exePath, _logger);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(
                x => x.LogWarning(It.Is<string>(s => s.Contains("Port property not found"))), 
                Times.Once);
        }

        [Test]
        public void TC06_ReadPortFromExePath_InvalidJsonSyntax_ReturnsNull()
        {
            // Arrange
            string invalidJson = @"{ ""Port"": 5000, ... missing brackets ... ";
            File.WriteAllText(_appSettingsPath, invalidJson);

            // Act
            var result = AppSettingsManager.ReadPortFromExePath(_exePath, _logger);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(
                x => x.LogError(It.Is<string>(s => s.Contains("Failed to parse JSON"))), 
                Times.Once);
        }

        [Test]
        public void TC07_ReadPortFromExePath_PortValueIsInteger_ReturnsValue()
        {
            // Arrange
            string jsonContent = @"{ ""Port"": 5000 }";
            File.WriteAllText(_appSettingsPath, jsonContent);

            // Act
            var result = AppSettingsManager.ReadPortFromExePath(_exePath, _logger);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result, Is.EqualTo(5000));
            _mockLogger.Verify(
                x => x.LogInfomation(It.Is<string>(s => s.Contains("Port value read") && s.Contains("5000"))), 
                Times.Once);
        }

        [Test]
        public void TC08_ReadPortFromExePath_PortValueIsString_ReturnsNull()
        {
            // Arrange
            string jsonContent = @"{ ""Port"": ""not_a_number"" }";
            File.WriteAllText(_appSettingsPath, jsonContent);

            // Act
            var result = AppSettingsManager.ReadPortFromExePath(_exePath, _logger);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(
                x => x.LogError(It.Is<string>(s => s.Contains("Failed to read port from appsettings"))), 
                Times.Once);
        }

        [Test]
        public void TC09_ReadPortFromExePath_MultiplePortsInJson_ReturnsFirstPort()
        {
            // Arrange
            string jsonContent = @"{
                ""Port"": 3000,
                ""ServerPort"": 4000,
                ""ClientPort"": 5000
            }";
            File.WriteAllText(_appSettingsPath, jsonContent);

            // Act
            var result = AppSettingsManager.ReadPortFromExePath(_exePath, _logger);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result, Is.EqualTo(3000));
            _mockLogger.Verify(
                x => x.LogInfomation(It.Is<string>(s => s.Contains("Port value read") && s.Contains("3000"))), 
                Times.Once);
        }

        [Test]
        public void TC10_ReadPortFromExePath_EmptyJsonFile_ReturnsNull()
        {
            // Arrange
            string jsonContent = @"{}";
            File.WriteAllText(_appSettingsPath, jsonContent);

            // Act
            var result = AppSettingsManager.ReadPortFromExePath(_exePath, _logger);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(
                x => x.LogWarning(It.Is<string>(s => s.Contains("Port property not found"))), 
                Times.Once);
        }
    }
}