using Common.Helper.Kernel32API.Interface;
using System.Runtime.InteropServices;

namespace Common.Helper.Kernel32API.Implement
{
    public class ProcessWaiter : IProcessWaiter
    {
        [DllImport("kernel32.dll")]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        public void WaitForProcess(IntPtr hProcess)
        {
            WaitForSingleObject(hProcess, Constants.INFINITE);
        }
    }
}
