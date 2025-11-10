using Common.Helper;
using Common.Logging;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Middleware.Services
{
    #region Data Models

    public class NetworkTransaction
    {
        public string Protocol { get; set; }
        public NetworkRequest Request { get; set; }
        public NetworkResponse Response { get; set; }
    }

    public class NetworkRequest
    {
        public string Method { get; set; }
        public string Url { get; set; }
        public string Body { get; set; }
        public string DataType { get; set; }
        public string ByteSize { get; set; }
    }

    public class NetworkResponse
    {
        public string StatusCode { get; set; }
        public string Body { get; set; }
        public string DataType { get; set; }
        public string ByteSize { get; set; }
    }

    #endregion

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
                _cts?.Dispose();

                if (_httpListener != null && _httpListener.IsListening)
                {
                    _httpListener.Stop();
                    _httpListener.Close();
                    _httpListener = null;
                }

                if (_tcpListener != null)
                {
                    _tcpListener.Stop();
                    _tcpListener = null;
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

                // 🔔 Raise event: Request captured
                OnRequestCaptured?.Invoke(transaction.Request);

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

                // 🔔 Raise event: Response captured
                OnResponseCaptured?.Invoke(transaction.Response);

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

            try
            {
                using (client)
                using (var server = new TcpClient())
                {
                    await server.ConnectAsync(IPAddress.Loopback, _serverPort, token);

                    using var clientStream = client.GetStream();
                    using var serverStream = server.GetStream();

                    var c2s = RelayTcpDataAsync(clientStream, serverStream, transaction.Request, token);
                    var s2c = RelayTcpDataAsync(serverStream, clientStream, transaction.Response, token);

                    await Task.WhenAny(c2s, s2c);
                }

                // 🔔 Raise event: Complete transaction
                OnTransactionCompleted?.Invoke(transaction);
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"TCP connection error: {ex.Message}");
                transaction.Response.Body = $"TCP Error: {ex.Message}";
                OnTransactionCompleted?.Invoke(transaction);
            }
        }

        private async Task RelayTcpDataAsync(NetworkStream from, NetworkStream to, object dataTarget, CancellationToken token)
        {
            var buffer = new byte[8192];
            int read;

            while ((read = await from.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
            {
                await to.WriteAsync(buffer, 0, read, token);

                var data = Encoding.UTF8.GetString(buffer, 0, read);
                var dataType = DataInspector.DetecDataType(buffer.Take(read).ToArray());

                if (dataTarget is NetworkRequest request)
                {
                    // Client → Server
                    request.Body = (request.Body ?? "") + data;
                    request.DataType = dataType;
                    request.ByteSize += read;

                    OnRequestCaptured?.Invoke(request);
                }
                else if (dataTarget is NetworkResponse response)
                {
                    // Server → Client
                    response.Body = (response.Body ?? "") + data;
                    response.DataType = dataType;
                    response.ByteSize += read;

                    OnResponseCaptured?.Invoke(response);
                }
            }
        }

        #endregion
    }
}