using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RevitBlockchain.Core;

namespace RevitBlockchain.MCP
{
    /// <summary>
    /// Connector for MCP blockchain server integration
    /// </summary>
    public class McpBlockchainConnector
    {
        private readonly string _serverUrl;
        private readonly string _wsUrl;
        private ClientWebSocket _websocket;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly Queue<object> _messageQueue;
        private bool _isConnected;

        public event EventHandler<BlockchainEvent> OnBlockchainEvent;
        public event EventHandler<ConnectionStatus> OnConnectionStatusChanged;

        public McpBlockchainConnector(string serverUrl)
        {
            _serverUrl = serverUrl;
            _wsUrl = serverUrl.Replace("http://", "ws://").Replace("https://", "wss://") + "/ws";
            _messageQueue = new Queue<object>();
        }

        /// <summary>
        /// Connect to MCP blockchain server
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                _websocket = new ClientWebSocket();
                _cancellationTokenSource = new CancellationTokenSource();

                await _websocket.ConnectAsync(new Uri(_wsUrl), _cancellationTokenSource.Token);
                _isConnected = true;

                OnConnectionStatusChanged?.Invoke(this, new ConnectionStatus 
                { 
                    IsConnected = true, 
                    Message = "Connected to MCP blockchain server" 
                });

                // Start listening for messages
                _ = Task.Run(ListenForMessages);

                // Send authentication
                await SendAuthenticationAsync();

                return true;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                OnConnectionStatusChanged?.Invoke(this, new ConnectionStatus 
                { 
                    IsConnected = false, 
                    Message = $"Connection failed: {ex.Message}" 
                });
                return false;
            }
        }

        /// <summary>
        /// Disconnect from server
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (_websocket?.State == WebSocketState.Open)
            {
                await _websocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, 
                    "Closing connection", 
                    CancellationToken.None
                );
            }

            _cancellationTokenSource?.Cancel();
            _websocket?.Dispose();
            _isConnected = false;

            OnConnectionStatusChanged?.Invoke(this, new ConnectionStatus 
            { 
                IsConnected = false, 
                Message = "Disconnected from MCP blockchain server" 
            });
        }

        /// <summary>
        /// Send transaction to blockchain
        /// </summary>
        public async Task<string> SendTransactionAsync(Dictionary<string, object> transactionData)
        {
            var message = new
            {
                type = "submit_transaction",
                data = transactionData,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000,
                id = Guid.NewGuid().ToString()
            };

            if (_isConnected && _websocket?.State == WebSocketState.Open)
            {
                var json = JsonConvert.SerializeObject(message);
                var bytes = Encoding.UTF8.GetBytes(json);
                await _websocket.SendAsync(
                    new ArraySegment<byte>(bytes), 
                    WebSocketMessageType.Text, 
                    true, 
                    CancellationToken.None
                );
                return message.id;
            }
            else
            {
                // Queue for later sending
                _messageQueue.Enqueue(message);
                return message.id;
            }
        }

        /// <summary>
        /// Call smart contract function
        /// </summary>
        public async Task<ContractCallResult> CallContractAsync(
            string contractName, 
            string functionName, 
            object parameters)
        {
            var message = new
            {
                type = "call_contract",
                contract = contractName,
                function = functionName,
                parameters = parameters,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000,
                id = Guid.NewGuid().ToString()
            };

            if (_isConnected && _websocket?.State == WebSocketState.Open)
            {
                var json = JsonConvert.SerializeObject(message);
                var bytes = Encoding.UTF8.GetBytes(json);
                await _websocket.SendAsync(
                    new ArraySegment<byte>(bytes), 
                    WebSocketMessageType.Text, 
                    true, 
                    CancellationToken.None
                );

                // In production, would wait for response
                return new ContractCallResult 
                { 
                    Success = true, 
                    RequestId = message.id 
                };
            }

            return new ContractCallResult 
            { 
                Success = false, 
                Error = "Not connected to blockchain server" 
            };
        }

        /// <summary>
        /// Query blockchain data
        /// </summary>
        public async Task<QueryResult> QueryAsync(string queryType, object parameters)
        {
            using (var client = new HttpClient())
            {
                var url = $"{_serverUrl}/api/query/{queryType}";
                var response = await client.PostAsJsonAsync(url, parameters);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return new QueryResult
                    {
                        Success = true,
                        Data = JsonConvert.DeserializeObject(content)
                    };
                }

                return new QueryResult
                {
                    Success = false,
                    Error = $"Query failed: {response.StatusCode}"
                };
            }
        }

        /// <summary>
        /// Listen for messages from server
        /// </summary>
        private async Task ListenForMessages()
        {
            var buffer = new ArraySegment<byte>(new byte[4096]);
            var messageBuilder = new StringBuilder();

            while (_websocket?.State == WebSocketState.Open && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await _websocket.ReceiveAsync(buffer, _cancellationTokenSource.Token);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        messageBuilder.Append(Encoding.UTF8.GetString(buffer.Array, 0, result.Count));

                        if (result.EndOfMessage)
                        {
                            var message = messageBuilder.ToString();
                            messageBuilder.Clear();
                            ProcessMessage(message);
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await DisconnectAsync();
                        break;
                    }
                }
                catch (Exception ex)
                {
                    OnBlockchainEvent?.Invoke(this, new BlockchainEvent
                    {
                        Type = "error",
                        Data = new { error = ex.Message }
                    });
                }
            }
        }

        /// <summary>
        /// Process received message
        /// </summary>
        private void ProcessMessage(string message)
        {
            try
            {
                var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(message);
                var eventType = data.ContainsKey("type") ? data["type"].ToString() : "unknown";

                OnBlockchainEvent?.Invoke(this, new BlockchainEvent
                {
                    Type = eventType,
                    Data = data
                });

                // Handle specific message types
                switch (eventType)
                {
                    case "block_created":
                        HandleNewBlock(data);
                        break;
                    case "transaction_confirmed":
                        HandleTransactionConfirmation(data);
                        break;
                    case "contract_event":
                        HandleContractEvent(data);
                        break;
                }
            }
            catch (Exception ex)
            {
                OnBlockchainEvent?.Invoke(this, new BlockchainEvent
                {
                    Type = "parse_error",
                    Data = new { error = ex.Message, raw = message }
                });
            }
        }

        /// <summary>
        /// Send authentication to server
        /// </summary>
        private async Task SendAuthenticationAsync()
        {
            var authMessage = new
            {
                type = "authenticate",
                client_type = "revit_addon",
                version = "1.0.0",
                instance_id = Environment.MachineName,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000
            };

            var json = JsonConvert.SerializeObject(authMessage);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _websocket.SendAsync(
                new ArraySegment<byte>(bytes), 
                WebSocketMessageType.Text, 
                true, 
                CancellationToken.None
            );
        }

        /// <summary>
        /// Process queued messages
        /// </summary>
        public async Task ProcessQueuedMessagesAsync()
        {
            while (_messageQueue.Count > 0 && _isConnected)
            {
                var message = _messageQueue.Dequeue();
                var json = JsonConvert.SerializeObject(message);
                var bytes = Encoding.UTF8.GetBytes(json);
                await _websocket.SendAsync(
                    new ArraySegment<byte>(bytes), 
                    WebSocketMessageType.Text, 
                    true, 
                    CancellationToken.None
                );
                await Task.Delay(100); // Rate limiting
            }
        }

        private void HandleNewBlock(Dictionary<string, object> data)
        {
            // Process new block notification
        }

        private void HandleTransactionConfirmation(Dictionary<string, object> data)
        {
            // Process transaction confirmation
        }

        private void HandleContractEvent(Dictionary<string, object> data)
        {
            // Process smart contract event
        }
    }

    // Event and result classes
    public class BlockchainEvent
    {
        public string Type { get; set; }
        public object Data { get; set; }
    }

    public class ConnectionStatus
    {
        public bool IsConnected { get; set; }
        public string Message { get; set; }
    }

    public class ContractCallResult
    {
        public bool Success { get; set; }
        public string RequestId { get; set; }
        public object Result { get; set; }
        public string Error { get; set; }
    }

    public class QueryResult
    {
        public bool Success { get; set; }
        public object Data { get; set; }
        public string Error { get; set; }
    }
}
