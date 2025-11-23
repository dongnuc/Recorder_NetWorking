using System.Collections.Concurrent;

namespace Common.Models.Entities
{
    /// <summary>
    /// Manages connection traces for client and server during a request/response transaction
    /// </summary>
    public class ConnectionTraceManager
    {
        private readonly List<ConnectionTrace> _clientTraces = new();
        private readonly List<ConnectionTrace> _serverTraces = new();
        private readonly object _lock = new object();
        private int _currentStage;

        public IReadOnlyList<ConnectionTrace> ClientTraces => _clientTraces.AsReadOnly();
        public IReadOnlyList<ConnectionTrace> ServerTraces => _serverTraces.AsReadOnly();

        public ConnectionTraceManager(int stage)
        {
            _currentStage = stage;
        }

        /// <summary>
        /// Add a trace for client connection state
        /// </summary>
        public void AddClientTrace(ConnectionState state, string note = "")
        {
            lock (_lock)
            {
                _clientTraces.Add(new ConnectionTrace
                {
                    Stage = _currentStage,
                    State = state,
                    Timestamp = DateTime.Now,
                    Note = note
                });
            }
        }

        /// <summary>
        /// Add a trace for server connection state
        /// </summary>
        public void AddServerTrace(ConnectionState state, string note = "")
        {
            lock (_lock)
            {
                _serverTraces.Add(new ConnectionTrace
                {
                    Stage = _currentStage,
                    State = state,
                    Timestamp = DateTime.Now,
                    Note = note
                });
            }
        }

        /// <summary>
        /// Get the current state of the client connection
        /// </summary>
        public ConnectionState GetCurrentClientState()
        {
            lock (_lock)
            {
                return _clientTraces.Count > 0 
                    ? _clientTraces[_clientTraces.Count - 1].State 
                    : ConnectionState.New;
            }
        }

        /// <summary>
        /// Get the current state of the server connection
        /// </summary>
        public ConnectionState GetCurrentServerState()
        {
            lock (_lock)
            {
                return _serverTraces.Count > 0 
                    ? _serverTraces[_serverTraces.Count - 1].State 
                    : ConnectionState.New;
            }
        }

        /// <summary>
        /// Get all traces as a formatted string for debugging
        /// </summary>
        public string GetTraceSummary()
        {
            lock (_lock)
            {
                var summary = new System.Text.StringBuilder();
                summary.AppendLine($"Stage {_currentStage} Connection Traces:");
                summary.AppendLine("Client Traces:");
                foreach (var trace in _clientTraces)
                {
                    summary.AppendLine($"  [{trace.Timestamp:HH:mm:ss.fff}] {trace.State} - {trace.Note}");
                }
                summary.AppendLine("Server Traces:");
                foreach (var trace in _serverTraces)
                {
                    summary.AppendLine($"  [{trace.Timestamp:HH:mm:ss.fff}] {trace.State} - {trace.Note}");
                }
                return summary.ToString();
            }
        }
    }
}
