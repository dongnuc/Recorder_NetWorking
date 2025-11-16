using System.Runtime.InteropServices;

namespace Common.Helper.Kernel32API
{
    public class ConsoleManager : IConsoleManager
    {
        [DllImport("kernel32.dll")]
        static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll")]
        static extern bool GenerateConsoleCtrlEvent(uint ctrlEvent, uint dwProcessGroupId);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr hObject);

        public void Free()
        {
            FreeConsole();
        }

        public void Alloc()
        {
            AllocConsole();
        }

        public void SendCtrlC(uint processId, IntPtr mutex, IMutexManager mutexManager)
        {
            mutexManager.Wait(mutex);
            if (AttachConsole(processId))
            {
                NativeApi.Kernel32.SetConsoleCtrlHandler(null, true);
                try
                {
                    GenerateConsoleCtrlEvent(Constants.CTRL_C_EVENT, 0);

                    System.Threading.Thread.Sleep(500);
                }
                finally
                {
                    NativeApi.Kernel32.SetConsoleCtrlHandler(null, false);
                    FreeConsole();
                }
            }
            mutexManager.Release(mutex);
        }

        public void CloseHandles(ChildProcess child)
        {
            CloseHandle(child.hProcess);
            CloseHandle(child.hThread);
        }
    }
}
