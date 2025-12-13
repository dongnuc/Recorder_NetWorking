using Common.Interfaces.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Common.Helper
{
    public class AppSettingsManager
    {
        private readonly string _filePath;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ISystemLogger _logger;

        #region Constructor
        public AppSettingsManager(string filePath, ISystemLogger logger)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"AppSettings file not found: {filePath}");

            _filePath = filePath;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
        }

        #endregion

        #region Update Port
        private bool IsFileLocked(string filePath)
        {
            try
            {
                using (FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    return false;
                }
            }
            catch (IOException)
            {
                return true;
            }
        }
        /// <summary>
        /// Update the Port value in appsettings.json
        /// </summary>
        /// <param name="newPort">New port number (1-65535)</param>
        /// <param name="createBackup">Create backup before updating</param>
        /// <returns>True if successful</returns>
        public bool UpdatePort(int newPort, bool createBackup = true)
        {
            try
            {
                if (IsFileLocked(_filePath))
                {
                    _logger.LogError($"   File is locked: {_filePath}");
                    _logger.LogError($"   Close any applications using this file");
                    return false;
                }
                // Validate port
                if (newPort < 1 || newPort > 65535)
                {
                    _logger.LogError($"Invalid port number: {newPort}. Must be between 1-65535.");
                    return false;
                }

                // Create backup if requested
                if (createBackup)
                {
                    CreateBackup();
                }

                // Read JSON
                string jsonContent = File.ReadAllText(_filePath);
                var jsonNode = JsonNode.Parse(jsonContent);

                if (jsonNode == null)
                {
                    _logger.LogError("Failed to parse JSON content");
                    return false;
                }

                // Update Port value
                if (jsonNode["Port"] != null)
                {
                    jsonNode["Port"] = newPort.ToString();
                }
                else
                {
                    _logger.LogWarning("Port field not found in JSON, adding it");
                    jsonNode["Port"] = newPort.ToString();
                }

                // Write back to file
                string updatedJson = jsonNode.ToJsonString(_jsonOptions);
                File.WriteAllText(_filePath, updatedJson);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to update Port: {ex.Message}");
                return false;
            }
        }
        #endregion
        public static bool UpdateAppSettings(string clientPath, int clientPort, string serverPath, int serverPort, ISystemLogger logger)
        {
            try
            {
                // Get or create appsettings.json for client
                string clientAppSettings = AppSettingsPathResolver.GetAppSettingsPath(clientPath, logger);
                if (string.IsNullOrEmpty(clientAppSettings))
                {
                    logger.LogWarning("Client appsettings.json not found, creating default...");
                }
                else
                {
                    var clientManager = new AppSettingsManager(clientAppSettings, logger);
                    clientManager.UpdatePort(clientPort, createBackup: true);
                    logger.LogInfomation($" Client appsettings updated - Port: {clientPort}");
                }

                // Get or create appsettings.json for server
                string serverAppSettings = AppSettingsPathResolver.GetAppSettingsPath(serverPath, logger);
                if (string.IsNullOrEmpty(serverAppSettings))
                {
                    logger.LogWarning("Server appsettings.json not found, creating default...");
                }
                else
                {
                    var serverManager = new AppSettingsManager(serverAppSettings, logger);
                    serverManager.UpdatePort(serverPort, createBackup: false);
                    logger.LogInfomation($"Server appsettings updated - Port: {serverPort}");
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to update appsettings: {ex.Message}");
                return false;
            }
        }

        public static int? ReadPortFromExePath(string exePath, ISystemLogger logger)
        {
            if (!File.Exists(exePath)) throw new FileNotFoundException("Exe not found", exePath);

            try
            {
                string exeDirectory = Path.GetDirectoryName(exePath);

                string appSettingsPath = Path.Combine(exeDirectory, "appsettings.json");

                if (!File.Exists(appSettingsPath))
                {
                    logger.LogWarning($"appsettings.json not found at: {appSettingsPath}");
                    return null;
                }

                // Read and parse JSON file
                string jsonContent = File.ReadAllText(appSettingsPath);

                using var jsonDocument = JsonDocument.Parse(jsonContent);

                // Try to get Port value from JSON
                if (jsonDocument.RootElement.TryGetProperty("Port", out JsonElement portElement))
                {
                    int port = 0;
                    bool isParsed = false;

                    if (portElement.ValueKind == JsonValueKind.Number)
                    {
                        if (portElement.TryGetInt32(out port))
                        {
                            isParsed = true;
                        }
                    }
                    else if (portElement.ValueKind == JsonValueKind.String)
                    {
                        string portStr = portElement.GetString();
                        if (int.TryParse(portStr, out port))
                        {
                            isParsed = true;
                        }
                    }

                    if (isParsed)
                    {
                        logger.LogInfomation($"Port value read from {appSettingsPath}: {port}");
                        return port;
                    }
                    else
                    {
                        logger.LogWarning($"Port property exists but is not a valid integer. Value: {portElement}");
                        return null;
                    }
                }

                logger.LogWarning($"Port property not found in {appSettingsPath}");
                return null;
            }
            catch (JsonException jsonEx)
            {
                logger.LogError($"Failed to parse JSON: {jsonEx.Message}");
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to read port from appsettings: {ex.Message}");
                return null;
            }
        }

        #region Backup & Restore

        /// <summary>
        /// Create backup of current appsettings.json
        /// </summary>
        public string CreateBackup()
        {
            try
            {
                string backupPath = $"{_filePath}.backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                File.Copy(_filePath, backupPath, overwrite: true);

                _logger.LogInfomation($" Backup created: {Path.GetFileName(backupPath)}");
                return backupPath;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create backup: {ex.Message}");
                return null;
            }
        }
        #endregion

    }
}
