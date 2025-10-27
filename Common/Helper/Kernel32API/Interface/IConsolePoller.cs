namespace Common.Helper.Kernel32API
{
    public interface IConsolePoller
    {
        Task<string> PollAsync(ChildProcess child, IntPtr mutex);
        
        /// <summary>
        /// Poll console buffer once and return new output immediately (non-blocking)
        /// </summary>
        Task<string> PollOnceAsync(ChildProcess child, IntPtr mutex);
    }
}
