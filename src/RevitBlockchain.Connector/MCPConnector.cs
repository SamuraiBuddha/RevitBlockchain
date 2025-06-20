using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RevitBlockchain.Core;

namespace RevitBlockchain.Connector
{
    /// <summary>
    /// Connects Revit blockchain operations to MCP server
    /// </summary>
    public class MCPConnector : IMCPConnector
    {
        private readonly HttpClient _httpClient;
        private readonly string _mcpEndpoint;
        private readonly string _instanceId;
        private readonly ILogger _logger;

        public MCPConnector(string mcpEndpoint, string instanceId, ILogger logger)
        {
            _mcpEndpoint = mcpEndpoint ?? throw new ArgumentNullException(nameof(mcpEndpoint));
            _instanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_mcpEndpoint)
            };
            _httpClient.DefaultRequestHeaders.Add("X-Instance-ID", _instanceId);
        }

        /// <summary>
        /// Submit a Revit change transaction to the blockchain
        /// </summary>
        public async Task<BlockchainTransaction> SubmitChangeTransaction(RevitChangeData changeData)
        {
            try
            {
                _logger.Log($"Submitting change transaction for element {changeData.ElementId}");

                var transaction = new BlockchainTransaction
                {
                    Id = CryptoHelper.GenerateUniqueId(),
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Type = "revit_change",
                    Data = changeData,
                    InstanceId = _instanceId,
                    Hash = CryptoHelper.CalculateSHA256(JsonConvert.SerializeObject(changeData))
                };

                // Sign the transaction
                var signature = CryptoHelper.SignData(transaction.Hash, GetPrivateKey());
                transaction.Signature = signature;

                // Submit to MCP server
                var response = await PostToMCP("submit_transaction", transaction);
                
                if (response.Success)
                {
                    _logger.Log($"Transaction {transaction.Id} submitted successfully");
                    return transaction;
                }
                else
                {
                    throw new Exception($"Failed to submit transaction: {response.Error}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error submitting transaction: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Query blockchain for element history
        /// </summary>
        public async Task<List<BlockchainTransaction>> GetElementHistory(int elementId)
        {
            try
            {
                var response = await PostToMCP("query_element_history", new { elementId });
                
                if (response.Success)
                {
                    return JsonConvert.DeserializeObject<List<BlockchainTransaction>>(response.Data.ToString());
                }
                
                throw new Exception($"Failed to query element history: {response.Error}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error querying element history: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Verify blockchain integrity for a project
        /// </summary>
        public async Task<BlockchainVerificationResult> VerifyProjectIntegrity(string projectGuid)
        {
            try
            {
                var response = await PostToMCP("verify_project_integrity", new { projectGuid });
                
                if (response.Success)
                {
                    return JsonConvert.DeserializeObject<BlockchainVerificationResult>(response.Data.ToString());
                }
                
                throw new Exception($"Failed to verify project integrity: {response.Error}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error verifying project integrity: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get current blockchain status
        /// </summary>
        public async Task<BlockchainStatus> GetBlockchainStatus()
        {
            try
            {
                var response = await PostToMCP("get_status", null);
                
                if (response.Success)
                {
                    return JsonConvert.DeserializeObject<BlockchainStatus>(response.Data.ToString());
                }
                
                throw new Exception($"Failed to get blockchain status: {response.Error}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error getting blockchain status: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Create a workset smart contract
        /// </summary>
        public async Task<string> CreateWorksetContract(WorksetContractData contractData)
        {
            try
            {
                var response = await PostToMCP("create_workset_contract", contractData);
                
                if (response.Success)
                {
                    return response.Data.ToString(); // Contract address
                }
                
                throw new Exception($"Failed to create workset contract: {response.Error}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error creating workset contract: {ex.Message}");
                throw;
            }
        }

        private async Task<MCPResponse> PostToMCP(string method, object data)
        {
            var request = new MCPRequest
            {
                Method = method,
                Data = data,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpResponse = await _httpClient.PostAsync("/mcp", content);
            var responseJson = await httpResponse.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<MCPResponse>(responseJson);
        }

        private string GetPrivateKey()
        {
            // In production, this would be securely stored
            // For now, generate based on instance ID
            return CryptoHelper.CalculateSHA256($"{_instanceId}-private-key");
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public class MCPRequest
    {
        public string Method { get; set; }
        public object Data { get; set; }
        public long Timestamp { get; set; }
    }

    public class MCPResponse
    {
        public bool Success { get; set; }
        public object Data { get; set; }
        public string Error { get; set; }
    }

    public class BlockchainTransaction
    {
        public string Id { get; set; }
        public long Timestamp { get; set; }
        public string Type { get; set; }
        public object Data { get; set; }
        public string InstanceId { get; set; }
        public string Hash { get; set; }
        public string Signature { get; set; }
        public string BlockHash { get; set; }
        public int BlockNumber { get; set; }
    }

    public class BlockchainStatus
    {
        public int BlockHeight { get; set; }
        public string LatestBlockHash { get; set; }
        public int PendingTransactions { get; set; }
        public List<string> ConnectedNodes { get; set; }
        public bool IsSynced { get; set; }
    }

    public class BlockchainVerificationResult
    {
        public bool IsValid { get; set; }
        public int TotalTransactions { get; set; }
        public int ValidTransactions { get; set; }
        public List<string> Errors { get; set; }
        public string MerkleRoot { get; set; }
    }
}