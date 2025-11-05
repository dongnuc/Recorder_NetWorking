using System.Runtime.InteropServices;

namespace Common.Helper.Kernel32API
{
    public class MutexManager : IMutexManager
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr CreateMutex(IntPtr lpMutexAttributes, bool InitialOwner, string MutexName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReleaseMutex(IntPtr hMutex);

        [DllImport("kernel32.dll")]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        static extern uint GetLastError();

        public IntPtr Create(string mutexName)
        {
            IntPtr mutex = CreateMutex(IntPtr.Zero, false, mutexName);
            if (mutex == IntPtr.Zero)
            {
                throw new Exception("Failed to create mutex: " + GetLastError());
            }
            return mutex;
        }

        public void Wait(IntPtr mutex)
        {
            WaitForSingleObject(mutex, Constants.INFINITE);
        }

        public void Release(IntPtr mutex)
        {
            ReleaseMutex(mutex);
        }

        public void Close(IntPtr mutex)
        {
            CloseHandle(mutex);
        }
    }
}
