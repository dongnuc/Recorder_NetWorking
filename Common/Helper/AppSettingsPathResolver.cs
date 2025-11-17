using Common.Logging;

namespace Common.Helper
{
    public class AppSettingsPathResolver
    {
        public static string GetAppSettingsPath(string exePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    LogManager.Instance.LogError("Executable path is null or empty");
                    return null;
                }

                if (!File.Exists(exePath))
                {
                    LogManager.Instance.LogError($"Executable not found: {exePath}");
                    return null;
                }

                // Get directory containing the exe
                string exeDirectory = Path.GetDirectoryName(exePath);

                // Search strategy 1: Same directory as exe
                string appSettingsPath = Path.Combine(exeDirectory, "appsettings.json");
                if (File.Exists(appSettingsPath))
                {
                    LogManager.Instance.LogDebug($" Found appsettings.json: {appSettingsPath}");
                    return appSettingsPath;
                }
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Error resolving appsettings path: {ex.Message}");
                return null;
            }
            LogManager.Instance.LogWarning($"appsettings.json not found for: {exePath}");
            return null;
        }
    }
}
