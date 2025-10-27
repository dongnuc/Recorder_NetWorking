namespace Common.Helper.Kernel32API.Interface
{
    public interface IProcessWaiter
    {
        void WaitForProcess(IntPtr hProcess);
    }
}
