using Common.Helper;
using Common.Logging;
using System.Net;
using System.Net.Sockets;
using System.Text;
using static Common.Models.Entities.MiddlewareModel;

namespace Middleware.Services
{

    public class MiddlewareStart
    {
        private static readonly Lazy<MiddlewareStart> _instance = new(() => new MiddlewareStart());
        public static MiddlewareStart Instance => _instance.Value;
        private MiddlewareStart() { }
        public bool IsRunning => _isSessionRunning;
        #region Fields

        private CancellationTokenSource _cts;
        private bool _isSessionRunning;
        private HttpListener _httpListener;
        private TcpListener _tcpListener;
        private int _proxyPort;
        private int _serverPort;

        #endregion

        #region Public Methods

        /// <summary>
        /// Get the server port that the middleware is forwarding to
        /// </summary>
        /// <returns>The server port number</returns>
        public int GetServerPort()
        {
            return _serverPort;
        }

        /// <summary>
        /// Get the proxy port that the middleware is listening on
        /// </summary>
        /// <returns>The proxy port number</returns>
        public int GetProxyPort()
        {
            return _proxyPort;
        }

        #endregion

        #region Events
        // Event raised when network request is captured (Client → Server)
        public event Action<NetworkRequest> OnRequestCaptured;

        // Event raised when network response is captured (Server → Client)
        public event Action<NetworkResponse> OnResponseCaptured;

        // Event raised when complete network transaction is captured
        public event Action<NetworkTransaction> OnTransactionCompleted;

        #endregion

        #region Start/Stop

        public async Task StartAsync(int proxyPort, int serverPort, bool useHttp = true)
        {
            if (_isSessionRunning)
            {
                LogManager.Instance.LogWarning("Middleware already running");
                return;
            }

            _proxyPort = proxyPort;
            _serverPort = serverPort;
            _cts = new CancellationTokenSource();
            _isSessionRunning = true;

            LogManager.Instance.LogInfomation($" Starting {(useHttp ? "HTTP" : "TCP")} middleware - Proxy:{proxyPort}, Server:{serverPort}");

            if (useHttp)
            {
                StartHttpProxy(_cts.Token);
            }
            else
            {
                StartTcpProxy(_cts.Token);
            }

            await Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (!_isSessionRunning) return;

            try
            {
                LogManager.Instance.LogInfomation("Stopping middleware...");

                _isSessionRunning = false;
                _cts?.Cancel();
                await Task.Delay(1000);

                if (_httpListener != null)
                {
                    try
                    {
                        if (_httpListener.IsListening)
                        {
                            _httpListener.Stop();
                            LogManager.Instance.LogDebug("HTTP Listener stopped");
                        }
                        _httpListener.Close();
                        _httpListener = null;
                    }
                    catch (ObjectDisposedException)
                    {
                        LogManager.Instance.LogDebug("HTTP Listener already disposed");
                    }
                    catch (Exception ex)
                    {
                        LogManager.Instance.LogWarning($"Error stopping HTTP Listener: {ex.Message}");
                    }
                }

                if (_tcpListener != null)
                {
                    try
                    {
                        _tcpListener.Stop();
                        _tcpListener = null;
                        LogManager.Instance.LogDebug("TCP Listener stopped");
                    }
                    catch (Exception ex)
                    {
                        LogManager.Instance.LogWarning($"Error stopping TCP Listener: {ex.Message}");
                    }
                }
                try
                {
                    _cts?.Dispose();
                    _cts = null;
                }
                catch (Exception ex)
                {
                    LogManager.Instance.LogWarning($"Error disposing CTS: {ex.Message}");
                }

                LogManager.Instance.LogInfomation(" Middleware stopped");
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Error stopping middleware: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        #endregion

        #region HTTP Proxy
        private void StartHttpProxy(CancellationToken token)
        {
            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://localhost:{_proxyPort}/");
                _httpListener.Start();

                Task.Run(() => ListenForHttpRequests(token), token);
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"HTTP Proxy start error: {ex.Message}");
                throw;
            }
        }

        private async Task ListenForHttpRequests(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    _ = Task.Run(() => ProcessHttpRequest(context), token);
                }
                catch (HttpListenerException) { break; }
                catch (Exception ex)
                {
                    LogManager.Instance.LogError($"HTTP listener error: {ex.Message}");
                }
            }
        }

        private async Task ProcessHttpRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var transaction = new NetworkTransaction
            {
                Protocol = "HTTP"
            };

            try
            {
                // 1. Capture Request (Client → Server)
                string requestBody = string.Empty;
                if (request.ContentLength64 > 0)
                {
                    using var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding);
                    requestBody = await reader.ReadToEndAsync();
                }

                var requestBytes = request.ContentEncoding.GetBytes(requestBody);

                transaction.Request = new NetworkRequest
                {
                    Method = request.HttpMethod,
                    Url = request.Url?.ToString(),
                    Body = requestBody,
                    DataType = DataInspector.DetecDataType(requestBytes),
                    ByteSize = requestBytes.Length.ToString()
                };

                LogManager.Instance.LogDebug($" HTTP Request: {request.HttpMethod} {request.Url}");

                // 2. Forward to real server
                var realServerUrl = $"http://localhost:{_serverPort}{request.Url?.AbsolutePath}";
                using var client = new HttpClient();
                var forwardRequest = new HttpRequestMessage(new HttpMethod(request.HttpMethod), realServerUrl);

                if (!string.IsNullOrEmpty(requestBody))
                {
                    var contentType = request.ContentType != null
                        ? System.Net.Http.Headers.MediaTypeHeaderValue.Parse(request.ContentType)
                        : null;
                    forwardRequest.Content = new StringContent(requestBody, contentType);
                }

                var responseMessage = await client.SendAsync(forwardRequest);
                var responseBytes = await responseMessage.Content.ReadAsByteArrayAsync();
                string responseBody = Encoding.UTF8.GetString(responseBytes);

                // 3. Capture Response (Server → Client)
                transaction.Response = new NetworkResponse
                {
                    StatusCode = (responseMessage.StatusCode).ToString(),
                    Body = responseBody,
                    DataType = DataInspector.DetecDataType(responseBytes),
                    ByteSize = responseBytes.Length.ToString()
                };

                LogManager.Instance.LogDebug($" HTTP Response: {responseMessage.StatusCode}");

                // 4. Send response back to client
                var response = context.Response;
                response.StatusCode = (int)responseMessage.StatusCode;
                response.ContentType = responseMessage.Content.Headers.ContentType?.ToString();
                response.ContentLength64 = responseBytes.Length;

                await response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                response.Close();

                // 🔔 Raise event: Complete transaction
                OnTransactionCompleted?.Invoke(transaction);
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"HTTP request processing error: {ex.Message}");
                transaction.Response = new NetworkResponse
                {
                    StatusCode = "500",
                    Body = $"Proxy Error: {ex.Message}"
                };
                OnTransactionCompleted?.Invoke(transaction);

                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch { }
            }
        }

        #endregion

        #region TCP Proxy

        private void StartTcpProxy(CancellationToken token)
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Loopback, _proxyPort);
                _tcpListener.Start();

                Task.Run(() => ListenForTcpConnections(token), token);
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"TCP Proxy start error: {ex.Message}");
                throw;
            }
        }

        private async Task ListenForTcpConnections(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var client = await _tcpListener.AcceptTcpClientAsync(token);
                    _ = Task.Run(() => HandleTcpConnection(client, token), token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    LogManager.Instance.LogError($"TCP accept error: {ex.Message}");
                }
            }
        }

        private async Task HandleTcpConnection(TcpClient client, CancellationToken token)
        {
            var transaction = new NetworkTransaction
            {
                Protocol = "TCP",
                Request = new NetworkRequest { Method = "TCP" },
                Response = new NetworkResponse { StatusCode = "OK" }
            };

            var transactionLock = new object();
            bool transactionCompleted = false;
            TcpClient server = null;

            try
            {
                server = new TcpClient();
                server.NoDelay = true; // Disable Nagle's algorithm for lower latency

                LogManager.Instance.LogDebug($"🔌 Middleware: Client connected, attempting to connect to server at localhost:{_serverPort}");

                try
                {
                    //  Connect to actual server
                    await server.ConnectAsync(IPAddress.Loopback, _serverPort, token);
                    LogManager.Instance.LogInfomation($"✅ Middleware: Successfully connected to server at localhost:{_serverPort}");
                }
                catch (SocketException ex)
                {
                    LogManager.Instance.LogError($"❌ Middleware: Failed to connect to server at localhost:{_serverPort}");
                    LogManager.Instance.LogError($"   Error: {ex.Message} (SocketErrorCode: {ex.SocketErrorCode})");

                    // Send error back to client
                    try
                    {
                        using var errorStream = client.GetStream();
                        var errorMsg = Encoding.UTF8.GetBytes($"ERROR: Cannot connect to server on port {_serverPort}\r\n");
                        await errorStream.WriteAsync(errorMsg, 0, errorMsg.Length, token);
                        await errorStream.FlushAsync(token);
                    }
                    catch { }

                    return; // Exit if cannot connect to server
                }

                //  Both client and server connected - start relay
                using (client)
                using (server)
                {
                    LogManager.Instance.LogDebug($"🔄 Middleware: Starting bidirectional relay");

                    using var clientStream = client.GetStream();
                    using var serverStream = server.GetStream();

                    // Use CancellationTokenSource to coordinate shutdown between both relay tasks
                    using var relayCts = CancellationTokenSource.CreateLinkedTokenSource(token);

                    //  Start Client → Server relay task
                    var c2s = RelayTcpDataAsync(
                        clientStream,
                        serverStream,
                        transaction.Request,
                        relayCts.Token,
                        isRequest: true,
                        onStreamClosed: () =>
                        {
                            LogManager.Instance.LogDebug("📭 Client closed connection");
                            relayCts.Cancel(); // Signal the other relay to stop
                        }
                    );

                    // Start Server → Client relay task
                    var s2c = RelayTcpDataAsync(
                        serverStream,
                        clientStream,
                        transaction.Response,
                        relayCts.Token,
                        isRequest: false,
                        onStreamClosed: () =>
                        {
                            LogManager.Instance.LogDebug("📭 Server closed connection");
                            relayCts.Cancel(); // Signal the other relay to stop
                        }
                    );

                    // Wait for both relay tasks to complete
                    await Task.WhenAll(c2s, s2c);

                    LogManager.Instance.LogDebug($"✅ Middleware: Both relay tasks completed");

                    // Raise transaction completed event after both relays finish
                    lock (transactionLock)
                    {
                        if (!transactionCompleted)
                        {
                            transactionCompleted = true;

                            bool hasRequestData = !string.IsNullOrWhiteSpace(transaction.Request?.Body);
                            bool hasResponseData = !string.IsNullOrWhiteSpace(transaction.Response?.Body);

                            if (hasRequestData && hasResponseData)
                            {
                                LogManager.Instance.LogInfomation("🎉 Transaction completed - Request & Response received");
                                OnTransactionCompleted?.Invoke(transaction);
                            }
                            else
                            {
                                LogManager.Instance.LogDebug($"⚠️ Incomplete transaction - Request: {hasRequestData}, Response: {hasResponseData}");
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LogManager.Instance.LogDebug($"🛑 Middleware: TCP connection cancelled");
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"❌ Middleware: TCP connection error: {ex.Message}");
                LogManager.Instance.LogError($"   Stack trace: {ex.StackTrace}");

                lock (transactionLock)
                {
                    if (!transactionCompleted && !string.IsNullOrWhiteSpace(transaction.Request?.Body))
                    {
                        transaction.Response.Body = $"TCP Error: {ex.Message}";
                        OnTransactionCompleted?.Invoke(transaction);
                    }
                }
            }
            finally
            {
                try
                {
                    server?.Close();
                    server?.Dispose();
                    client?.Close();
                    client?.Dispose();
                }
                catch { }
            }
        }

        private async Task RelayTcpDataAsync(
            NetworkStream from,
            NetworkStream to,
            object dataTarget,
            CancellationToken token,
            bool isRequest,
            Action onStreamClosed = null)
        {
            var buffer = new byte[8192];
            int read;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    read = await from.ReadAsync(buffer, 0, buffer.Length, token);

                    if (read == 0)
                    {
                        // Stream closed gracefully
                        LogManager.Instance.LogDebug($"📭 {(isRequest ? "Client" : "Server")} stream closed gracefully");
                        onStreamClosed?.Invoke();
                        break;
                    }

                    // Write data to the other side
                    await to.WriteAsync(buffer, 0, read, token);
                    await to.FlushAsync(token); // Ensure data is sent immediately

                    // Capture data for logging
                    var data = Encoding.UTF8.GetString(buffer, 0, read);
                    var dataType = DataInspector.DetecDataType(buffer.Take(read).ToArray());

                    if (dataTarget is NetworkRequest request)
                    {
                        // Client → Server
                        request.Body = (request.Body ?? "") + data;
                        request.DataType = dataType;
                        request.ByteSize += read;
                    }
                    else if (dataTarget is NetworkResponse response)
                    {
                        // Server → Client
                        response.Body = (response.Body ?? "") + data;
                        response.DataType = dataType;
                        response.ByteSize += read;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LogManager.Instance.LogDebug($"🛑 {(isRequest ? "Client→Server" : "Server→Client")} relay cancelled");
            }
            catch (IOException ex) when (ex.InnerException is SocketException socketEx)
            {
                // Connection forcibly closed - this is expected when one side disconnects
                LogManager.Instance.LogDebug($"🔌 {(isRequest ? "Client→Server" : "Server→Client")} connection closed: {socketEx.SocketErrorCode}");
                onStreamClosed?.Invoke();
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"❌ {(isRequest ? "Client→Server" : "Server→Client")} relay error: {ex.Message}");
                throw;
            }
        }
        #endregion
    }
}