namespace Common.Helper.Kernel32API
{
    public interface IConsoleManager
    {
        void Free();
        void Alloc();
        void SendCtrlC(uint processId, IntPtr mutex, IMutexManager mutexManager);
        void CloseHandles(ChildProcess child);
    }
}
