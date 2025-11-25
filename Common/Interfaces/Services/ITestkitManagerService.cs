using Common.Models.Entities;
using NetworkMonitor.Models;
using static Common.Models.Entities.MiddlewareModel;

namespace Common.Interfaces.Services
{
    public interface ITestkitManagerService
    {
        #region Data Reception from ProcessManager
        void ReceiveUserInput(string input, string dataType);
        void ReceiveClientOutput(string output);
        void ReceiveServerOutput(string output);
        #endregion
        #region Data Reception from Middleware


        #endregion

        #region Stage Management
        void CreateInitialStage(string action);
        void IncrementStage();

        Dictionary<int, TestStage> GetCurrentTestStages();

        int GetCurrentStageIndex();
        void DeleteStage(int stageKey);

        #endregion

        #region Events for UI Binding
        event Action<string, string> OnUserInputReceived;
        event Action<string> OnClientOutputReceived;
        event Action<string> OnServerOutputReceived;
        //event Action<NetworkTransaction> OnTransactionReceived;
        /// <summary>
        /// Event khi stages thay đổi (để UI refresh toàn bộ)
        /// </summary>
        event Action<Dictionary<int, TestStage>> OnStagesChanged;
        /// <summary>
        /// Event khi tạo stage mới (để UI thêm vào ComboBox)
        /// </summary>
        event Action<int> OnStageCreated;

        /// <summary>
        /// Event khi update stage (để UI refresh)
        /// </summary>
        event Action<int> OnStageUpdated;
        #endregion

        #region Monitor Networking
        event Action<int, HttpNetworkFlow> OnNewHttpFlow;
        event Action<int> OnQueueCountChanged;
        event Action<int, TcpNetworkFlow> OnNewTcpFlow;
        void IngestHttpTransaction(HttpNetworkFlow httpFlow);
        void IngestTcpTransaction(TcpNetworkFlow tcpFlow);
        void FlushNetworkQueue();
        #endregion

    }
}
