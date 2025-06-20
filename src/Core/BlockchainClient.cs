using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace RevitBlockchain.Core
{
    /// <summary>
    /// Client for interacting with the MCP blockchain server
    /// </summary>
    public class BlockchainClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _serverUrl;
        private readonly string _instanceId;
        private readonly Queue<Transaction> _offlineQueue;
        private bool _isConnected;

        public BlockchainClient()
        {
            _httpClient = new HttpClient();
            _serverUrl = GetServerUrl();
            _instanceId = GenerateInstanceId();
            _offlineQueue = new Queue<Transaction>();
            
            // Test connection
            Task.Run(async () => await TestConnection());
        }

        /// <summary>
        /// Submit a transaction to the blockchain
        /// </summary>
        public async Task<TransactionResult> SubmitTransaction(Transaction transaction)
        {
            if (!_isConnected)
            {
                // Queue for later submission
                _offlineQueue.Enqueue(transaction);
                return new TransactionResult 
                { 
                    Success = false, 
                    Message = "Queued for offline sync",
                    TransactionId = transaction.Id
                };
            }

            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    $"{_serverUrl}/api/submit_transaction",
                    transaction
                );

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<TransactionResult>();
                }
                else
                {
                    return new TransactionResult
                    {
                        Success = false,
                        Message = $"Server error: {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new TransactionResult
                {
                    Success = false,
                    Message = $"Connection error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Call a smart contract function
        /// </summary>
        public async Task<ContractResult> CallContract(string contractName, string functionName, object parameters)
        {
            var contractCall = new ContractCall
            {
                ContractName = contractName,
                FunctionName = functionName,
                Parameters = parameters,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000, // Microseconds
                Caller = GetCurrentUser()
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    $"{_serverUrl}/api/call_contract",
                    contractCall
                );

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ContractResult>();
                }
                else
                {
                    return new ContractResult
                    {
                        Success = false,
                        Error = $"Contract call failed: {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new ContractResult
                {
                    Success = false,
                    Error = $"Contract call error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Log an event to the blockchain
        /// </summary>
        public void LogEvent(string eventType, string subject, string details)
        {
            var transaction = new Transaction
            {
                Id = GenerateTransactionId(),
                Type = "EventLog",
                Data = new Dictionary<string, object>
                {
                    ["eventType"] = eventType,
                    ["subject"] = subject,
                    ["details"] = details,
                    ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000,
                    ["user"] = GetCurrentUser()
                }
            };

            Task.Run(async () => await SubmitTransaction(transaction));
        }

        /// <summary>
        /// Get element history from blockchain
        /// </summary>
        public async Task<List<ElementHistory>> GetElementHistory(string elementId)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"{_serverUrl}/api/element_history/{elementId}"
                );

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<ElementHistory>>();
                }
                
                return new List<ElementHistory>();
            }
            catch
            {
                return new List<ElementHistory>();
            }
        }

        /// <summary>
        /// Sync offline queue when connection restored
        /// </summary>
        public async Task SyncOfflineQueue()
        {
            while (_offlineQueue.Count > 0 && _isConnected)
            {
                var transaction = _offlineQueue.Dequeue();
                await SubmitTransaction(transaction);
                await Task.Delay(100); // Rate limiting
            }
        }

        public void Disconnect()
        {
            _httpClient?.Dispose();
            _isConnected = false;
        }

        private async Task TestConnection()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_serverUrl}/api/health");
                _isConnected = response.IsSuccessStatusCode;
                
                if (_isConnected)
                {
                    // Sync any queued transactions
                    await SyncOfflineQueue();
                }
            }
            catch
            {
                _isConnected = false;
            }
        }

        private string GetServerUrl()
        {
            // TODO: Read from config file
            return Environment.GetEnvironmentVariable("BLOCKCHAIN_SERVER_URL") 
                   ?? "http://localhost:3000";
        }

        private string GenerateInstanceId()
        {
            return $"RevitClient-{Environment.MachineName}-{Guid.NewGuid():N}.Substring(0, 8)";
        }

        private string GenerateTransactionId()
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
            var hash = GenerateHash(Guid.NewGuid().ToString()).Substring(0, 8);
            return $"{timestamp}-{_instanceId}-{hash}";
        }

        private string GenerateHash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }

        private string GetCurrentUser()
        {
            return Environment.UserName;
        }
    }

    // Data models
    public class Transaction
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public Dictionary<string, object> Data { get; set; }
    }

    public class TransactionResult
    {
        public bool Success { get; set; }
        public string TransactionId { get; set; }
        public string BlockHash { get; set; }
        public string Message { get; set; }
    }

    public class ContractCall
    {
        public string ContractName { get; set; }
        public string FunctionName { get; set; }
        public object Parameters { get; set; }
        public long Timestamp { get; set; }
        public string Caller { get; set; }
    }

    public class ContractResult
    {
        public bool Success { get; set; }
        public object Result { get; set; }
        public string Error { get; set; }
        public string TransactionId { get; set; }
    }

    public class ElementHistory
    {
        public string ElementId { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime Timestamp { get; set; }
        public string ChangeType { get; set; }
        public Dictionary<string, object> Changes { get; set; }
        public string TransactionId { get; set; }
    }
}
