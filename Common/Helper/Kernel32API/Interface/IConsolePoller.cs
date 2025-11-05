namespace Common.Helper.Kernel32API
{
    public interface IConsolePoller
    {
        Task<string> CaptureCurrentConsoleAsync(
           ChildProcess child,
           IntPtr mutex,
           bool expandBuffer = true);
        Task<string> CaptureCurrentConsoleWithRetryAsync(
            ChildProcess child,
            IntPtr mutex,
            int maxRetries = 3,
            int retryDelayMs = 200,
            bool expandBuffer = true);
    }
}
