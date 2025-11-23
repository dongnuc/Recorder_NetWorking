using Common.Interfaces.Services;
using Common.Logging;
using Common.Models.Entities;
using Common.Resources;
using Middleware.Services;
using static Common.Models.Entities.MiddlewareModel;

namespace TestKitManagement.Services
{
    public class TestkitManagerService : ITestkitManagerService
    {

        #region Fields
        private readonly Dictionary<int, TestStage> _testStages = new();
        private int _currentStageIndex = 0;
        private readonly Queue<NetworkTransaction> _pendingTransactions = new();
        private readonly object _lock = new object();
        private string _testCaseName;
        #endregion

        public event Action<string, string> OnUserInputReceived;
        public event Action<string> OnClientOutputReceived;
        public event Action<string> OnServerOutputReceived;
        public event Action<MiddlewareModel.NetworkTransaction> OnTransactionReceived;
        public event Action<Dictionary<int, TestStage>> OnStagesChanged;
        public event Action<int> OnStageCreated;
        public event Action<int> OnStageUpdated;


        #region Method Helper
        private TestStage CreateNewStage(int stageIndex,string input)
        {
            var newStage = new TestStage();
            var inputClient = new User
            {
                Stage = stageIndex,
                Input = input,
                Action = ActionKeywords.INPUT,
            };
            newStage.User = inputClient;
            _testStages[_currentStageIndex] = newStage;
            return newStage;
        }

        private TestStage GetOrCreateCurrentStage()
        {
            if (!_testStages.TryGetValue(_currentStageIndex, out var stage))
            {
                stage = new TestStage();
                _testStages[_currentStageIndex] = stage;
            }

            return stage;
        }

        private void ProcessTransactionWithoutEvents(NetworkTransaction transaction)
        {
            var currentStage = GetOrCreateCurrentStage();

            var network = new Network
            {
                Stage = _currentStageIndex,
                Url = transaction.Request.Url,
                HttpMethod = transaction.Request.Method,
                REQ_Payload = transaction.Request.Body,
                RES_Payload = transaction.Response.Body
            };

            currentStage.Network = network;
            
            // Store connection traces if available
            if (transaction.ConnectionTraces != null)
            {
                currentStage.ConnectionTraces = transaction.ConnectionTraces;
            }
            // Notify UI
        }
        /// Flush tất cả pending transactions vào current stage
        private void FlushPendingTransactions()
        {
            List<NetworkTransaction> transactionsToProcess;
            lock (_lock)
            {
                int count = _pendingTransactions.Count;
                if (count == 0)
                {
                    LogManager.Instance.LogDebug("No pending transactions to flush");
                    return;
                }

                LogManager.Instance.LogInfomation($"Flushing {count} pending transaction(s) to Stage {_currentStageIndex}");

                transactionsToProcess = new List<NetworkTransaction>(_pendingTransactions);
                _pendingTransactions.Clear();

                LogManager.Instance.LogInfomation($"Flushed {count} transaction(s) to Stage {_currentStageIndex}");
            }
            foreach (var transaction in transactionsToProcess)
            {
                try
                {
                    ProcessTransactionWithoutEvents(transaction);
                }
                catch (Exception ex)
                {
                    LogManager.Instance.LogError($"❌ Error processing buffered transaction: {ex.Message}");
                }
            }
            OnStagesChanged?.Invoke(_testStages);
        }
        #endregion

        public TestkitManagerService()
        {
            SubscribeToMiddleware();
        }

        private void SubscribeToMiddleware()
        {
            try
            {
                MiddlewareStart.Instance.OnTransactionCompleted += OnMiddlewareTransactionReceived;
            }
            catch (Exception ex)
            {
                throw;
            }
        }


        private void UnsubscribeFromMiddleware()
        {

            try
            {
                MiddlewareStart.Instance.OnTransactionCompleted -= OnMiddlewareTransactionReceived;

            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Failed to unsubscribe from Middleware: {ex.Message}");
            }
        }

        private void OnMiddlewareTransactionReceived(NetworkTransaction transaction)
        {
            if (transaction == null)
            {
                LogManager.Instance.LogWarning("Received null transaction from Middleware");
                return;
            }
            
            lock (_lock)
            {
                _pendingTransactions.Enqueue(transaction);
                LogManager.Instance.LogInfomation($"Transaction queued (Queue size: {_pendingTransactions.Count})");
            }

            OnTransactionReceived?.Invoke(transaction);
        }

        public int GetPendingTransactionCount()
        {
            lock (_lock)
            {
                return _pendingTransactions.Count;
            }
        }

        public void InitializeTestCase(string testCaseName)
        {
            _testCaseName = testCaseName;
            _testStages.Clear();
            _currentStageIndex = 0;

            lock (_lock)
            {
                _pendingTransactions.Clear();
            }

            LogManager.Instance.LogInfomation($"TestCase initialized: {testCaseName}");
        }

        public Dictionary<int, TestStage> GetCurrentTestStages()
        {
            return _testStages;
        }

        public int GetCurrentStageIndex()
        {
            return _currentStageIndex;
        }

        public void CreateInitialStage(string action)
        {
            if(_currentStageIndex <= 0)
            {
                _currentStageIndex = 1;
            }
            else
            {
                _currentStageIndex++;
            }

                var intitalStage = new TestStage();
            var initialInput = new User
            {
                Stage = _currentStageIndex,
                Action = action,
                Input = string.Empty,
            };
            intitalStage.User = initialInput;
            _testStages[_currentStageIndex] = intitalStage;
            // Notify UI
            OnStageCreated?.Invoke(_currentStageIndex);
            OnStagesChanged?.Invoke(_testStages);
        }

        public void IncrementStage()
        {
           if(_currentStageIndex >= 1)
            {
                _currentStageIndex++;
            }
            else
            {
                _currentStageIndex = 1;
            }
        }

        public void ReceiveClientOutput(string output)
        {
            var currentStage = GetOrCreateCurrentStage();

            if (currentStage.Client != null)
            {
                currentStage.Client.Console = output;
            }
            else
            {
                currentStage.Client = new Client
                {
                    Stage = _currentStageIndex,
                    Console = output ?? string.Empty
                };
            }

            OnClientOutputReceived?.Invoke(output);
            OnStageUpdated?.Invoke(_currentStageIndex);
            OnStagesChanged?.Invoke(_testStages);

        }

        public void ReceiveServerOutput(string output)
        {
            var currentStage = GetOrCreateCurrentStage();

            if (currentStage.Server != null)
            {
                currentStage.Server.Console = output;
            }
            else
            {
                currentStage.Server = new Server
                {
                    Stage = _currentStageIndex,
                    Console = output ?? string.Empty
                };
            } 
            // Notify UI
            OnServerOutputReceived?.Invoke(output);
            OnStageUpdated?.Invoke(_currentStageIndex);
            OnStagesChanged?.Invoke(_testStages);
        }

        public void ReceiveTransaction(NetworkTransaction transaction)
        {
            if(transaction == null)
            {
                return;
            }
            if (_currentStageIndex == 0)
            {
                lock (_lock)
                {
                    _pendingTransactions.Enqueue(transaction);
                }
                return;
            }

            ProcessTransactionWithoutEvents(transaction);
        }

        public void ReceiveUserInput(string input, string dataType)
        {
            _currentStageIndex++;
            var newStage = CreateNewStage(_currentStageIndex, input);

            FlushPendingTransactions();

            OnUserInputReceived?.Invoke(input, dataType);
            OnStageCreated?.Invoke(_currentStageIndex);
            OnStagesChanged?.Invoke(_testStages);
        }

        public void Dispose()
        {
            UnsubscribeFromMiddleware();
            lock (_lock)
            {
                _pendingTransactions.Clear();
            }
            _testStages?.Clear();
        }

    }
}
