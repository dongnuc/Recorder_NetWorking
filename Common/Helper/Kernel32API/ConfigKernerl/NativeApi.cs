using System.Runtime.InteropServices;
using System.Text;

namespace Common.Helper.Kernel32API
{
    public static class NativeApi
    {
        public static class Kernel32
        {
            private const string KERNEL32 = "kernel32.dll";

            [DllImport("kernel32.dll")]
            public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

            [DllImport(KERNEL32, SetLastError = true)]
            public static extern bool SetConsoleCtrlHandler(
                ConsoleCtrlDelegate HandlerRoutine,
                bool Add);

            [DllImport(KERNEL32, SetLastError = true)]
            public static extern uint GetProcessId(IntPtr hProcess);
        }

        public static class User32
        {
            private const string USER32 = "user32.dll";

            [DllImport(USER32, SetLastError = true)]
            public static extern short GetAsyncKeyState(int vKey);

            [DllImport(USER32, SetLastError = true)]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            [DllImport(USER32, SetLastError = true)]
            public static extern IntPtr GetForegroundWindow();
        }

        public delegate bool ConsoleCtrlDelegate(uint ctrlType);
    }
}