using Common.Interfaces.Services;
using Common.Logging;
using Common.Models.Entities;
using Common.Resources;
using NetworkMonitor.Models;
using System.Collections.ObjectModel;

namespace TestKitManagement.Services
{
    public class TestkitManagerService : ITestkitManagerService
    {

        #region Fields
        private readonly Dictionary<int, TestStage> _testStages = new();
        private int _currentStageIndex = 0;
        private readonly Queue<HttpNetworkFlow> _pendingHttpNetwork = new();
        private readonly Queue<TcpNetworkFlow> _pendingTcpNetwork = new();

        private readonly object _lock = new object();
        #endregion

        public event Action<string, string> OnUserInputReceived;
        public event Action<string> OnClientOutputReceived;
        public event Action<string> OnServerOutputReceived;
        public event Action<Dictionary<int, TestStage>> OnStagesChanged;
        public event Action<int> OnStageCreated;
        public event Action<int> OnStageUpdated;
        public bool isCaptureClient = false;
        public bool isCaptureServer = false;

        public event Action<int, HttpNetworkFlow> OnNewHttpFlow;
        public event Action<int, TcpNetworkFlow> OnNewTcpFlow;
        public event Action<int> OnQueueCountChanged;
        #region Method Helper
        private TestStage CreateNewStage(int stageIndex, string input)
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


        #endregion

        private void NotifyQueueCount()
        {
            int total = _pendingHttpNetwork.Count + _pendingTcpNetwork.Count;
            LogManager.Instance.LogInfomation(total.ToString());
            OnQueueCountChanged?.Invoke(total);
        }

        public void FlushNetworkQueue()
        {
            List<HttpNetworkFlow> httpToFlush;
            List<TcpNetworkFlow> tcpToFlush;

            lock (_lock)
            {
                if (_pendingHttpNetwork.Count == 0 && _pendingTcpNetwork.Count == 0) return;

                var currentStage = GetOrCreateCurrentStage();

                lock (_lock)
                {
                    if (_pendingHttpNetwork.Count == 0 && _pendingTcpNetwork.Count == 0) return;

                    httpToFlush = _pendingHttpNetwork.ToList();
                    tcpToFlush = _pendingTcpNetwork.ToList();

                    _pendingHttpNetwork.Clear();
                    _pendingTcpNetwork.Clear();
                    NotifyQueueCount();
                }

                foreach (var flow in httpToFlush)
                {
                    if(currentStage.NetworkHttpFlows == null)
                    {
                        currentStage.NetworkHttpFlows = new ObservableCollection<HttpNetworkFlow>();
                    }
                    flow.Stage = _currentStageIndex;
                    currentStage.NetworkHttpFlows.Add(flow); 
                }

                foreach (var flow in tcpToFlush)
                {
                    if (currentStage.NetworkTcpFlows == null)
                    {
                        currentStage.NetworkTcpFlows = new ObservableCollection<TcpNetworkFlow>();
                    }
                    flow.Stage = _currentStageIndex;
                    currentStage.NetworkTcpFlows.Add(flow); 
                }

                OnStageUpdated?.Invoke(_currentStageIndex);
            }

        }

        public void IngestHttpTransaction(HttpNetworkFlow httpFlow)
        {
            if (httpFlow == null) return;
            lock (_lock)
            {
                _pendingHttpNetwork.Enqueue(httpFlow);
            }
            NotifyQueueCount();
        }

        public void IngestTcpTransaction(TcpNetworkFlow tcpFlow)
        {
            if (tcpFlow == null) return;
            lock (_lock)
            {
                _pendingTcpNetwork.Enqueue(tcpFlow);
            }
            NotifyQueueCount();
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
            if (_currentStageIndex <= 0)
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

            FlushNetworkQueue();
            OnStageCreated?.Invoke(_currentStageIndex);
            OnStagesChanged?.Invoke(_testStages);
        }

        public void IncrementStage()
        {
            if (_currentStageIndex >= 1)
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
                if (!isCaptureClient)
                {
                    foreach (var testStage in _testStages.Values)
                    {
                        if (testStage.User.Action == ActionKeywords.START_CLIENT && testStage.User.Stage == _currentStageIndex)
                        {
                            testStage.Client = new Client
                            {
                                Stage = testStage.User.Stage,
                                Console = output,
                            };
                            isCaptureClient = true;
                            OnClientOutputReceived?.Invoke(output);
                            OnStageUpdated?.Invoke(_currentStageIndex);
                            OnStagesChanged?.Invoke(_testStages);

                            return;
                        }
                    }
                }
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

                if (!isCaptureServer)
                {
                    foreach (var testStage in _testStages.Values)
                    {
                        if (testStage.User!.Action.Equals(ActionKeywords.START_SERVER) && testStage.User.Stage == _currentStageIndex)
                        {
                            testStage.Server = new Server
                            {
                                Stage = testStage.User.Stage,
                                Console = output
                            };
                            isCaptureServer = true;

                            OnClientOutputReceived?.Invoke(output);
                            OnStageUpdated?.Invoke(_currentStageIndex);
                            OnStagesChanged?.Invoke(_testStages);

                            return;
                        }

                    }
                }
               
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


        public void ReceiveUserInput(string input, string dataType)
        {
            _currentStageIndex++;
            var newStage = CreateNewStage(_currentStageIndex, input);

            OnUserInputReceived?.Invoke(input, dataType);
            OnStageCreated?.Invoke(_currentStageIndex);
            OnStagesChanged?.Invoke(_testStages);
        }


        public void DeleteStage(int stageKey)
        {
            lock (_lock)
            {
                if (_testStages.ContainsKey(stageKey))
                {
                    _testStages.Remove(stageKey);

                    if (_testStages.Count > 0)
                    {
                        _currentStageIndex = _testStages.Keys.Max();
                    }
                    else
                    {
                        _currentStageIndex = 0;
                    }

                    LogManager.Instance.LogInfomation($"Stage {stageKey} deleted. Current index updated to: {_currentStageIndex}");
                }
            }

            OnStagesChanged?.Invoke(_testStages);
        }

        public void Dispose()
        {
            lock (_lock)
            {
            }
            _testStages?.Clear();
        }

    }
}
