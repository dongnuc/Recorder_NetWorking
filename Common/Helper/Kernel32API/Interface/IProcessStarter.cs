namespace Common.Helper.Kernel32API
{
    public interface IProcessStarter
    {
        ChildProcess Start(string exePath, string name);
    }
}
