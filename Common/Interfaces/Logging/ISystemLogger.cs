namespace Common.Interfaces.Logging
{
    public interface ISystemLogger
    {
        void LogInfomation(string message);

        void LogDebug(string message);

        void LogCritical(string message);

        void LogError(string message);

        void LogWarning(string message);
    }
}
