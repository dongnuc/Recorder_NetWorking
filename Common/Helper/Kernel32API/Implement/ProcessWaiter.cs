using Common.Helper.Kernel32API.Interface;
using System.Runtime.InteropServices;

namespace Common.Helper.Kernel32API.Implement
{
    public class ProcessWaiter : IProcessWaiter
    {
        [DllImport("kernel32.dll")]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        private const uint WAIT_TIMEOUT = 0x00000102;
        private const uint WAIT_OBJECT_0 = 0x00000000;
        private const uint INFINITE = 0xFFFFFFFF;
        public void WaitForProcess(IntPtr hProcess)
        {
           uint result = WaitForSingleObject(hProcess, 1000);
            if (result == WAIT_TIMEOUT)
            {
                TerminateProcess(hProcess, 1);
                WaitForSingleObject(hProcess, 1000);
            }
        }
    }
}
