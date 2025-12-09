using Common.Interfaces.Logging;

namespace Common.Helper
{
    public class AppSettingsPathResolver
    {
        public static string GetAppSettingsPath(string exePath, ISystemLogger logger)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    logger.LogError("Executable path is null or empty");
                    return null;
                }

                if (!File.Exists(exePath))
                {
                    logger.LogError($"Executable not found: {exePath}");
                    return null;
                }

                // Get directory containing the exe
                string exeDirectory = Path.GetDirectoryName(exePath);

                // Search strategy 1: Same directory as exe
                string appSettingsPath = Path.Combine(exeDirectory, "appsettings.json");
                if (File.Exists(appSettingsPath))
                {
                    return appSettingsPath;
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error resolving appsettings path: {ex.Message}");
                return null;
            }
            logger.LogWarning($"appsettings.json not found for: {exePath}");
            return null;
        }
    }
}
