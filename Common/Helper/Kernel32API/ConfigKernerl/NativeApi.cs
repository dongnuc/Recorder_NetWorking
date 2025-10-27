using System.Runtime.InteropServices;
using System.Text;

namespace Common.Helper.Kernel32API
{
    public static class NativeApi
    {
        public static class Kernel32
        {
            private const string KERNEL32 = "kernel32.dll";

            [DllImport(KERNEL32, SetLastError = true)]
            public static extern bool CreateProcess(
                string lpApplicationName,
                string lpCommandLine,
                IntPtr lpProcessAttributes,
                IntPtr lpThreadAttributes,
                bool bInheritHandles,
                uint dwCreationFlags,
                IntPtr lpEnvironment,
                string lpCurrentDirectory,
                ref STARTUPINFO lpStartupInfo,
                out PROCESS_INFORMATION lpProcessInformation);

            [DllImport(KERNEL32, SetLastError = true)]
            public static extern uint GetLastError();

            [DllImport(KERNEL32, SetLastError = true)]
            public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

            [DllImport(KERNEL32, SetLastError = true)]
            public static extern bool AttachConsole(uint dwProcessId);

            [DllImport(KERNEL32, SetLastError = true)]
            public static extern bool FreeConsole();

            [DllImport(KERNEL32, SetLastError = true)]
            public static extern bool AllocConsole();

            [DllImport(KERNEL32, SetLastError = true)]
            public static extern IntPtr GetStdHandle(int nStdHandle);

            [DllImport(KERNEL32, SetLastError = true)]
            public static extern bool GetConsoleScreenBufferInfo(
                IntPtr hConsoleOutput,
                out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

            [DllImport(KERNEL32, SetLastError = true)]
            public static extern bool SetConsoleScreenBufferSize(
                IntPtr hConsoleOutput,
                COORD dwSize);

            [DllImport(KERNEL32, SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool ReadConsoleOutputCharacter(
                IntPtr hConsoleOutput,
                [Out] StringBuilder lpCharacter,
                uint nLength,
                COORD dwReadCoord,
                out uint lpNumberOfCharsRead);

            [DllImport(KERNEL32, SetLastError = true)]
            public static extern bool GenerateConsoleCtrlEvent(
                uint ctrlEvent,
                uint dwProcessGroupId);

            [DllImport(KERNEL32, SetLastError = true)]
            public static extern bool CloseHandle(IntPtr hObject);

            [DllImport(KERNEL32, SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr CreateMutex(
                IntPtr lpMutexAttributes,
                bool InitialOwner,
                string MutexName);

            [DllImport(KERNEL32, SetLastError = true)]
            public static extern bool ReleaseMutex(IntPtr hMutex);

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