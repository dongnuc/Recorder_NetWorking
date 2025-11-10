using Common.Helper.Kernel32API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Interfaces.Services
{
    public interface IProcessManager : IDisposable
    {
        #region event
        event Action<string> OnClientOutput;
        event Action<string> OnServerOutput;
        event Action<string, string> OnUserInput;
        #endregion


        #region ProcessManager
        Task<(ChildProcess child, IntPtr mutex, CancellationTokenSource cts)> StartSingleWithPollingAsync(
            string exePath,
            string name,
            bool isClient = true,
            bool showConsoleMessages = false);
        Task StopSingleAsync(
            ChildProcess child,
            IntPtr mutex,
            CancellationTokenSource cts,
            string name,
            bool showConsoleMessages = false);
        (ChildProcess child, IntPtr mutex) StartSingle(
            string exePath,
            string name,
            bool showConsoleMessages = false);
        void CloseSingle(
            ChildProcess child,
            IntPtr mutex,
            bool showConsoleMessages = false);

        Task CloseClientAsync();
        Task CloseServerAsync();
        #endregion
    }
}
