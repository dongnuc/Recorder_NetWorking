using System.Runtime.InteropServices;

namespace Common.Helper.Kernel32API
{
    public class ProcessStarter : IProcessStarter
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CreateProcess(
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

        [DllImport("kernel32.dll")]
        static extern uint GetLastError();

        public ChildProcess Start(string exePath, string name)
        {
            STARTUPINFO si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(si);

            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

            bool success = CreateProcess(
                exePath,
                null,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                Constants.CREATE_NEW_CONSOLE | Constants.CREATE_NEW_PROCESS_GROUP,
                IntPtr.Zero,
                null,
                ref si,
                out pi);

            if (!success)
                throw new Exception($"CreateProcess failed for {name}: {GetLastError()}");

            return new ChildProcess
            {
                hProcess = pi.hProcess,
                hThread = pi.hThread,
                processId = pi.dwProcessId,
                name = name
            };
        }
    }
}
