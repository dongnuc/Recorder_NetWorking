namespace Common.Helper.Kernel32API
{
    public interface IMutexManager
    {
        IntPtr Create(string mutexName);
        void Wait(IntPtr mutex);
        void Release(IntPtr mutex);
        void Close(IntPtr mutex);
    }
}
