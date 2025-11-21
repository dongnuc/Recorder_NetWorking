using Common.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Common.Helper
{
    public class AppSettingsManager
    {
        private readonly string _filePath;
        private readonly JsonSerializerOptions _jsonOptions;

        #region Constructor
        public AppSettingsManager(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"AppSettings file not found: {filePath}");

            _filePath = filePath;

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
                    LogManager.Instance.LogError($"   File is locked: {_filePath}");
                    LogManager.Instance.LogError($"   Close any applications using this file");
                    return false;
                }
                // Validate port
                if (newPort < 1 || newPort > 65535)
                {
                    LogManager.Instance.LogError($"Invalid port number: {newPort}. Must be between 1-65535.");
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
                    LogManager.Instance.LogError("Failed to parse JSON content");
                    return false;
                }

                // Update Port value
                if (jsonNode["Port"] != null)
                {
                    jsonNode["Port"] = newPort.ToString();
                }
                else
                {
                    LogManager.Instance.LogWarning("Port field not found in JSON, adding it");
                    jsonNode["Port"] = newPort.ToString();
                }

                // Write back to file
                string updatedJson = jsonNode.ToJsonString(_jsonOptions);
                File.WriteAllText(_filePath, updatedJson);

                LogManager.Instance.LogInfomation($"Updated Port to {newPort} in {Path.GetFileName(_filePath)}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Failed to update Port: {ex.Message}");
                return false;
            }
        }
        #endregion
        public static bool UpdateAppSettings(string clientPath, int clientPort, string serverPath, int serverPort)
        {
            try
            {
                // Get or create appsettings.json for client
                string clientAppSettings = AppSettingsPathResolver.GetAppSettingsPath(clientPath);
                if (string.IsNullOrEmpty(clientAppSettings))
                {
                    LogManager.Instance.LogWarning("Client appsettings.json not found, creating default...");
                }
                else
                {
                    var clientManager = new AppSettingsManager(clientAppSettings);
                    clientManager.UpdatePort(clientPort, createBackup: true);
                    LogManager.Instance.LogInfomation($" Client appsettings updated - Port: {clientPort}");
                }

                // Get or create appsettings.json for server
                string serverAppSettings = AppSettingsPathResolver.GetAppSettingsPath(serverPath);
                if (string.IsNullOrEmpty(serverAppSettings))
                {
                    LogManager.Instance.LogWarning("Server appsettings.json not found, creating default...");
                }
                else
                {
                    var serverManager = new AppSettingsManager(serverAppSettings);
                    serverManager.UpdatePort(serverPort, createBackup: false);
                    LogManager.Instance.LogInfomation($"✅ Server appsettings updated - Port: {serverPort}");
                }

                return true;
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Failed to update appsettings: {ex.Message}");
                return false;
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

                LogManager.Instance.LogInfomation($" Backup created: {Path.GetFileName(backupPath)}");
                return backupPath;
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Failed to create backup: {ex.Message}");
                return null;
            }
        }
        #endregion

    }
}
